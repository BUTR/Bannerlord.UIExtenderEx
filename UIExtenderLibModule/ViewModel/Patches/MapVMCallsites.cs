using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;

namespace UIExtenderLibModule.ViewModel.Patches
{
    [HarmonyPatch(typeof(MapVM), MethodType.Constructor, new [] { typeof(INavigationHandler), typeof(IMapStateHandler), typeof(MapBarShortcuts), typeof(Action) })]
    public static class MapVMConstructorCallsite
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
        {
            return ViewModelPatchUtil.TranspilerForVMInstantiation(input);
        }
    }

    [HarmonyPatch(typeof(MapVM), "OnRefresh")]
    public static class MapVMRefreshCallsite
    {
        public static void Prefix()
        {
            var component = UIExtenderLibModule.SharedInstance.ViewModelComponent;
            component.RefreshMixinsForTypes(new [] {typeof(MapInfoVM), typeof(MapTimeControlVM), typeof(MapNavigationVM)});
        }
    }
}