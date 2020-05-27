using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.ViewModelCollection.Multiplayer;
using UIExtenderLib.Interface;

namespace UIExtenderLib.CodePatcher.BuiltInPatches
{
    [CodePatcher]
    public class MissionAgentStatusVMCodePatcher : CustomCodePatcher
    {
        public override CodePatcherResult Apply(CodePatcherComponent comp)
        {
            var callsite = typeof(MissionGauntletAgentStatus).GetMethod(nameof(MissionGauntletAgentStatus.EarlyStart));
            if (callsite == null)
            {
                return CodePatcherResult.Failure;
            }

            var refresh = typeof(MissionAgentStatusVM).GetMethod(nameof(MissionAgentStatusVM.Tick));
            if (refresh == null)
            {
                return CodePatcherResult.Failure;
            }

            comp.AddViewModelInstantiationPatch(callsite);
            comp.AddViewModelRefreshPatch(refresh);
            return CodePatcherResult.Success;
        }
    }
}