using HarmonyLib;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CookbookPinyinSupport;

public class HandbookPinyinSupportModSystem : ModSystem
{
    public ICoreClientAPI api;
    public Harmony harmony;
    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Client;
    }
    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Logger.Notification(Lang.Get("handbookpinyinsupport:hello"));

        this.api = api;
        var supportedLocale = new string[] { "zh-cn" };
        if (supportedLocale.Contains(Lang.CurrentLocale) && !Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            try
            {
                harmony = new Harmony(Mod.Info.ModID);
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                api.Logger.Notification(Lang.Get("handbookpinyinsupport:fail_to_load"));
                api.Logger.Error(ex);
            }
        }
    }

    //public override void Start(ICoreAPI api)
    //{
    //    api.Logger.Notification(Lang.Get("handbookpinyinsupport:hello"));

    //    var supportedLocale = new string[] { "zh-cn" };
    //    if (supportedLocale.Contains(Lang.CurrentLocale) && !Harmony.HasAnyPatches(Mod.Info.ModID))
    //    {
    //        try
    //        {
    //            var harmony = new Harmony(Mod.Info.ModID);
    //            harmony.PatchAll();
    //        }
    //        catch (Exception ex)
    //        {
    //            api.Logger.Notification(Lang.Get("handbookpinyinsupport:fail_to_load"));
    //            api.Logger.Error(ex);
    //        }
    //    }
    //}

    public override void Dispose()
    {
        harmony?.UnpatchAll(Mod.Info.ModID);
    }
}
