using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using Vintagestory.GameContent;

namespace CookbookPinyinSupport;

[HarmonyPatch(typeof(GuiDialogHandbook))]
[HarmonyPatch(nameof(GuiDialogHandbook.FilterItems))]
internal class GuiDialogHandbookPatch
{
    delegate string GetTitleCachedDelegate(GuiHandbookTextPage page);
    static GetTitleCachedDelegate CreateGetTitleCachedDelegate()
    {
        var method = new DynamicMethod("GetTitleCached", typeof(string), new Type[] { typeof(GuiHandbookTextPage) }, typeof(GuiHandbookTextPage));
        FieldInfo fieldInfo = typeof(GuiHandbookTextPage).GetField("titleCached", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Field 'titleCached' not found.");
        ILGenerator il = method.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, fieldInfo);
        il.Emit(OpCodes.Ret);

        return (GetTitleCachedDelegate)method.CreateDelegate(typeof(GetTitleCachedDelegate));
    }
    static readonly GetTitleCachedDelegate GetTitleCached = CreateGetTitleCachedDelegate();

    internal static float FuzzyMatch(GuiHandbookPage page, string pattern)
    {
        PinyinMatch.MatchResult res = page switch
        {
            GuiHandbookItemStackPage stackPage => PinyinMatch.Instance.FuzzyMatch(stackPage.TextCacheTitle, pattern),
            GuiHandbookTextPage textPage => PinyinMatch.Instance.FuzzyMatch(GetTitleCached(textPage), pattern),
            _ => default,
        };
        float weightOffset = page switch
        {
            GuiHandbookItemStackPage stackPage => stackPage.searchWeightOffset,
            GuiHandbookPage => 0.5f,
            _ => 0
        };
        if (!res.HasResult)
            return 0f;
        if (res.fullMatchType == PinyinMatch.MatchType.FuzzyMatch)
            res.fullScore *= 1.2f;
        return Math.Max(res.firstLettersScore, res.fullScore) + weightOffset;
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var methodGetTextMatchWeight = AccessTools.Method(typeof(GuiHandbookPage), "GetTextMatchWeight");
        var methodMatch = AccessTools.Method(typeof(GuiDialogHandbookPatch), "FuzzyMatch", new[] { typeof(GuiHandbookPage), typeof(string) });
        var methodMathMax = AccessTools.Method(typeof(Math), "Max", new[] { typeof(float), typeof(float) });
        //var methodWriteLine = AccessTools.Method(typeof(Console), "WriteLine", new[] { typeof(float) });

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand as MethodInfo == methodGetTextMatchWeight)
            {
                var newCodes = new List<CodeInstruction>
                {
                    new(OpCodes.Ldloc_S, 5), // Load 'page'
                    new(OpCodes.Ldloc_S, 1), // Load 'texts'
                    new(OpCodes.Ldloc_S, 8), // Load 'j'
                    new(OpCodes.Ldelem_Ref), // Load 'texts[j]'
                    new(OpCodes.Call, methodMatch), // Call PinyinMatch.Match
                    new(OpCodes.Ldloc_S, 6), // Load 'weight'
                    new(OpCodes.Call, methodMathMax), // Call Math.Max
                    new(OpCodes.Stloc_S, 6), // Store back in 'weight'
                };

                codes.InsertRange(i + 2, newCodes);
                break;
            }
        }

        return codes;
    }
}
