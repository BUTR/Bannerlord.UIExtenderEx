using System;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using UIExtenderLib.Interface;

namespace UIExtenderLib.CodePatcher.BuiltInPatches
{
    [CodePatcher]
    public class CharacterVMCodePatcher : CustomCodePatcher
    {
        public override CodePatcherResult Apply(CodePatcherComponent comp)
        {
            var callsite = typeof(CharacterDeveloperVM).GetConstructor(new[] { typeof(Action) });
            if (callsite == null)
            {
                return CodePatcherResult.Failure;
            }

            var refresh = typeof(CharacterVM).GetMethod(nameof(CharacterVM.RefreshValues));
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