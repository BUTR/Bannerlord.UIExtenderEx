using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;
using UIExtenderLib.Interface;

namespace UIExtenderLib.CodePatcher.BuiltInPatches
{
    [CodePatcher]
    public class ThreeMapVMCodePatcher : CustomCodePatcher
    {
        public override CodePatcherResult Apply(CodePatcherComponent comp)
        {
            var callsite = typeof(MapVM).GetConstructor(new[]
            {
                typeof(INavigationHandler), typeof(IMapStateHandler), typeof(MapBarShortcuts), typeof(Action)
            });

            if (callsite == null)
            {
                return CodePatcherResult.Failure;
            }

            var dependentRefreshMethods = new[]
            {
                typeof(MapInfoVM).GetMethod(nameof(MapInfoVM.Refresh)),
                typeof(MapTimeControlVM).GetMethod(nameof(MapTimeControlVM.Refresh)),
                typeof(MapNavigationVM).GetMethod(nameof(MapNavigationVM.Refresh)),
            };

            if (dependentRefreshMethods.Any(e => e == null))
            {
                return CodePatcherResult.Failure;
            }

            comp.AddViewModelInstantiationPatch(callsite);
            foreach (var method in dependentRefreshMethods)
            {
                comp.AddViewModelRefreshPatch(method);
            }

            return CodePatcherResult.Success;
        }
    }
}