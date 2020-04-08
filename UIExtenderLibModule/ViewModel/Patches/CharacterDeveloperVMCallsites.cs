using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;

namespace UIExtenderLibModule.ViewModel.Patches
{    
    [HarmonyPatch(typeof(CharacterDeveloperVM), MethodType.Constructor, new[] { typeof(Action) })]
    public static class CharacterDeveloperVMConstructorCallsite
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
        {
            return ViewModelPatchUtil.TranspilerForVMInstantiation(input);
        }
    }

    [HarmonyPatch(typeof(CharacterDeveloperVM), "RefreshValues")]
    public static class CharacterDeveloperVMRefreshCallsite
    {
        public static void Prefix()
        {
            var component = UIExtenderLibModule.SharedInstance.ViewModelComponent;
            component.RefreshMixinsForTypes(new[] { typeof(CharacterDeveloperVM), typeof(CharacterVM) });
        }
    }
}
