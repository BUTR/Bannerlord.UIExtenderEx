using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.Core;

namespace UIExtenderLibModule.ViewModel.Patches
{
    //  Harmony cannot resolve the interface methods by iself, so this route has to be used
    [HarmonyPatch]
    public static class GauntletPartyScreenOnActivateCallsite
    {
        public static MethodBase TargetMethod()
        {
            Type type = typeof(GauntletPartyScreen);
            MethodInfo interfaceMethod = type.GetInterface(nameof(IGameStateListener)).GetMethod("OnActivate");
            if (interfaceMethod == null) return null;

            InterfaceMapping map = type.GetInterfaceMap(interfaceMethod.DeclaringType ?? throw new NullReferenceException("Cannot find GauntletPartyScreen IGameStateListener.OnActivate method"));
            return map.TargetMethods[Array.IndexOf(map.InterfaceMethods, interfaceMethod)];
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input) => ViewModelPatchUtil.TranspilerForVMInstantiation(input);
    }
}
