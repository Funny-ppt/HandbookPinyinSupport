using CookbookPinyinSupport;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace HandbookPinyinSupport;

[HarmonyPatch(typeof(GuiElementItemSlotGridBase))]
[HarmonyPatch(nameof(GuiElementItemSlotGridBase.FilterItemsBySearchText))]
internal unsafe class GuiElementItemSlotGridBasePatch
{
    internal class MatchCtx
    {
        public ItemSlot slot;
        public string pattern;
        public string name;
        //public string cachedText;
        public OrderedDictionary<int, WeightedSlot> dict;
        public KeyValuePair<int, ItemSlot>* val;
    }
    internal static void FuzzyMatch(MatchCtx ctx)
    {
        var res = PinyinMatch.Instance.FuzzyMatch(ctx.name, ctx.pattern);
        if (res.HasResult)
        {
            var weight = Math.Max(res.firstLettersScore, res.fullScore);
            ctx.dict.TryAdd(ctx.val->Key, new WeightedSlot { slot = ctx.slot, weight = -weight + 3f });
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var methodMatchesSearchText = AccessTools.Method(typeof(ItemStack), "MatchesSearchText"); // 用于定位注入点
        var methodCaseInsensitiveContains = AccessTools.Method(typeof(StringExtensions), "CaseInsensitiveContains"); // 用于定位注入点
        int i = 0;
        Label injectHeadPosLabel = new();

        for (; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Call && codes[i].operand as MethodInfo == methodCaseInsensitiveContains)
            {
                codes[i + 1].operand = injectHeadPosLabel;
            }
            if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand as MethodInfo == methodMatchesSearchText)
            {
                break;
            }
        }
        for (; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Pop)
            {
                var newCodes = new List<CodeInstruction>
                {
                    new(OpCodes.Newobj, AccessTools.Constructor(typeof(MatchCtx))) { labels = new List<Label> { injectHeadPosLabel } },
                    new(OpCodes.Dup),
                    new(OpCodes.Ldarg, 0),
                    new(OpCodes.Ldfld, AccessTools.Field(typeof(GuiElementItemSlotGridBase), "searchText")),
                    new(OpCodes.Stfld, AccessTools.Field(typeof(MatchCtx), nameof(MatchCtx.pattern))),
                    new(OpCodes.Dup),
                    new(OpCodes.Ldloc_3),
                    new(OpCodes.Stfld, AccessTools.Field(typeof(MatchCtx), nameof(MatchCtx.slot))),
                    //new(OpCodes.Dup),
                    //new(OpCodes.Ldloc, 4),
                    //new(OpCodes.Stfld, AccessTools.Field(typeof(MatchCtx), nameof(MatchCtx.cachedText))),
                    new(OpCodes.Dup),
                    new(OpCodes.Ldloc, 5),
                    new(OpCodes.Stfld, AccessTools.Field(typeof(MatchCtx), nameof(MatchCtx.name))),
                    new(OpCodes.Dup),
                    new(OpCodes.Ldloc_0),
                    new(OpCodes.Stfld, AccessTools.Field(typeof(MatchCtx), nameof(MatchCtx.dict))),
                    new(OpCodes.Dup),
                    new(OpCodes.Ldloca_S, 2),
                    new(OpCodes.Stfld, AccessTools.Field(typeof(MatchCtx), nameof(MatchCtx.val))),
                    new(OpCodes.Call, AccessTools.Method(typeof(GuiElementItemSlotGridBasePatch), "FuzzyMatch", new[] { typeof(MatchCtx) })),
                    //new(OpCodes.Br, cycleEndLabel),
                };

                codes.InsertRange(i + 1, newCodes);
            }
        }

        return codes;
    }
}
