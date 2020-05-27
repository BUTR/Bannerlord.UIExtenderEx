using SandBox.GauntletUI;
using System;
using System.Reflection;
using TaleWorlds.Core;
using UIExtenderLib.Interface;

namespace UIExtenderLib.CodePatcher.BuiltInPatches
{
    [CodePatcher]
    public class PartyVMCodePatcher : CustomCodePatcher
    {
        public override CodePatcherResult Apply(CodePatcherComponent comp)
        {
            Type type = typeof(GauntletPartyScreen);
            MethodInfo interfaceMethod = type.GetInterface(nameof(IGameStateListener)).GetMethod("OnActivate");
            if (interfaceMethod == null)
            {
                return CodePatcherResult.Failure;
            }

            InterfaceMapping map = type.GetInterfaceMap(interfaceMethod.DeclaringType ?? throw new NullReferenceException("Cannot find GauntletPartyScreen IGameStateListener.OnActivate method"));
            var callsite = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, interfaceMethod)];
            if (callsite == null)
            {
                return CodePatcherResult.Failure;
            }

            comp.AddViewModelInstantiationPatch(callsite);
            return CodePatcherResult.Success;
        }
    }
}