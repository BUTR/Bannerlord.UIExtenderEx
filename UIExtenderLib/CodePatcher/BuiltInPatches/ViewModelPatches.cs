using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using SandBox.GauntletUI;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.ViewModelCollection.Multiplayer;

namespace UIExtenderLib.CodePatcher.BuiltInPatches
{
    /// <summary>
    /// Set of view model specific patches.
    /// Some of them may fail because of outdated library, but given that the mod in question
    /// doesn't use that specific view model classes it should still function just fine (albeit
    /// user warning will be displayed on the main menu).
    /// </summary>
    internal class ViewModelPatches
    {
        internal enum Result: int
        {
            Success = 1,
            Failure = 2,
            Partial = 3,
        }
        
        internal static Result AddTo(CodePatcherComponent comp)
        {
            int value = 0;

            value |= (int)ThreeMapVMPatches(comp);
            value |= (int)PartyVMPatch(comp);
            value |= (int)MissionAgentStatusVMPatch(comp);
            value |= (int)MapVMPatch(comp);
            value |= (int)CharacterVMPatch(comp);

            if (Enum.IsDefined(typeof(Result), value))
            {
                return (Result) value;
            }
            else
            {
                Debug.Fail($"Invalid enum usage!");
                return Result.Failure;
            }
        }

        private static Result ThreeMapVMPatches(CodePatcherComponent comp)
        {
            var callsite = typeof(MapVM).GetConstructor(new Type[]
            {
                typeof(INavigationHandler), typeof(IMapStateHandler), typeof(MapBarShortcuts), typeof(Action)
            });
            
            if (callsite == null)
            {
                return Result.Failure;
            }

            var dependentRefreshMethods = new[]
            {
                typeof(MapInfoVM).GetMethod(nameof(MapInfoVM.Refresh)),
                typeof(MapTimeControlVM).GetMethod(nameof(MapTimeControlVM.Refresh)),
                typeof(MapNavigationVM).GetMethod(nameof(MapNavigationVM.Refresh)),
            };

            if (dependentRefreshMethods.Any(e => e == null))
            {
                return Result.Failure;
            }
                
            comp.AddViewModelInstantiationPatch(callsite);
            foreach (var method in dependentRefreshMethods)
            {
                comp.AddViewModelRefreshPatch(method);
            }

            return Result.Success;
        }
        
        private static Result PartyVMPatch(CodePatcherComponent comp)
        {
            Type type = typeof(GauntletPartyScreen);
            MethodInfo interfaceMethod = type.GetInterface(nameof(IGameStateListener)).GetMethod("OnActivate");
            if (interfaceMethod == null)
            {
                return Result.Failure;
            }

            InterfaceMapping map = type.GetInterfaceMap(interfaceMethod.DeclaringType ?? throw new NullReferenceException("Cannot find GauntletPartyScreen IGameStateListener.OnActivate method"));
            var callsite = map.TargetMethods[Array.IndexOf(map.InterfaceMethods, interfaceMethod)];
            if (callsite == null)
            {
                return Result.Failure;
            }

            comp.AddViewModelInstantiationPatch(callsite);
            return Result.Success;
        }

        private static Result MissionAgentStatusVMPatch(CodePatcherComponent comp)
        {
            var callsite = typeof(MissionGauntletAgentStatus).GetMethod(nameof(MissionGauntletAgentStatus.EarlyStart));
            if (callsite == null)
            {
                return Result.Failure;
            }

            var refresh = typeof(MissionAgentStatusVM).GetMethod(nameof(MissionAgentStatusVM.Tick));
            if (refresh == null)
            {
                return Result.Failure;
            }
            
            comp.AddViewModelInstantiationPatch(callsite);
            comp.AddViewModelRefreshPatch(refresh);
            return Result.Success;
        }

        private static Result MapVMPatch(CodePatcherComponent comp)
        {
            var callsite = typeof(GauntlerMapBarGlobalLayer).GetMethod(nameof(GauntlerMapBarGlobalLayer.Initialize));
            if (callsite == null)
            {
                return Result.Failure;
            }

            var refresh = typeof(MapVM).GetMethod(nameof(MapVM.OnRefresh));
            if (refresh == null)
            {
                return Result.Failure;
            }

            comp.AddViewModelInstantiationPatch(callsite);
            comp.AddViewModelRefreshPatch(refresh);
            return Result.Success;
        }

        private static Result CharacterVMPatch(CodePatcherComponent comp)
        {
            var callsite = typeof(CharacterDeveloperVM).GetConstructor(new Type[] {typeof(Action)});
            if (callsite == null)
            {
                return Result.Failure;
            }

            var refresh = typeof(CharacterVM).GetMethod(nameof(CharacterVM.RefreshValues));
            if (refresh == null)
            {
                return Result.Failure;
            }

            comp.AddViewModelInstantiationPatch(callsite);
            comp.AddViewModelRefreshPatch(refresh);
            return Result.Success;
        }
    }
}