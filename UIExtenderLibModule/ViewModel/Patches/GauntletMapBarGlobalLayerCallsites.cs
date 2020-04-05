using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace UIExtenderLibModule.ViewModel.Patches
{
	[HarmonyPatch(typeof(GauntlerMapBarGlobalLayer), "Initialize")]
    public static class GauntletMapGlobalLayerInitializeCallsite
    {
	    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
	    {
		    return ViewModelPatchUtil.TranspilerForVMInstantiation(input);
	    }
    }

    [HarmonyPatch(typeof(GauntlerMapBarGlobalLayer), "Refresh")]
    public static class GauntletGlobalLayerRefreshCallsite
    {
	    public static void Postfix()
	    {
            var component = UIExtenderLibModule.SharedInstance.ViewModelComponent;
            component.RefreshMixinsForTypes(new [] {typeof(MapVM)});
	    }
    }
}