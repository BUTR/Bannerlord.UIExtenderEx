using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using UIExtenderLib.Interface;

namespace UIExtenderLib.CodePatcher.BuiltInPatches
{
    [CodePatcher]
    public class MapVMCodePatcher : CustomCodePatcher
    {
        public override CodePatcherResult Apply(CodePatcherComponent comp)
        {
            var callsite = typeof(GauntlerMapBarGlobalLayer).GetMethod(nameof(GauntlerMapBarGlobalLayer.Initialize));
            if (callsite == null)
            {
                return CodePatcherResult.Failure;
            }

            var refresh = typeof(MapVM).GetMethod(nameof(MapVM.OnRefresh));
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