using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx
{
    public class UIPatchSubModule : MBSubModuleBase
    {
        [PrefabExtension("ClanParties", "descendant::Prefab/Window/Widget/Children/ListPanel/Children/Widget/Children/Widget/Children/Widget[2]/Children/ListPanel[1]/Children/ListPanel[3]")]
        private sealed class ClanPartiesPrefabExtension1 : PrefabExtensionSetAttributePatch
        {
            public override List<Attribute> Attributes => new()
            {
                new Attribute("IsEnabled", "@CanUseActions")
            };
        }
        [PrefabExtension("ClanParties", "descendant::Prefab/Window/Widget/Children/ListPanel/Children/Widget/Children/Widget/Children/Widget[2]/Children/ListPanel[1]/Children/ListPanel[3]/Children/Standard.DropdownWithHorizontalControl")]
        private sealed class ClanPartiesPrefabExtension2 : PrefabExtensionSetAttributePatch
        {
            public override List<Attribute> Attributes => new()
            {
                new Attribute("Parameter.IsEnabled", "true")
            };
        }
        
        private static readonly UIExtender Extender = new("Bannerlord.UIExtenderEx.UIPatch");
        private static readonly Harmony Harmony = new("Bannerlord.UIExtenderEx.UIPatch");
        
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            if (ApplicationVersionHelper.GameVersion() is { } gameVersion)
            {
                if (gameVersion.Major is 1 && gameVersion.Minor is 7 && gameVersion.Revision is >= 0 and < 2)
                {
                    Harmony.TryPatch(
                        AccessTools2.Method("SandBox.SandBoxSubModule:OnSubModuleLoad"),
                        postfix: AccessTools2.Method(typeof(UIPatchSubModule), nameof(SandBoxSubModuleOnSubModuleLoadPostfix)));
                }
            }
        }
        
        private static void SandBoxSubModuleOnSubModuleLoadPostfix()
        {
            Extender.Register(new[]
            {
                typeof(ClanPartiesPrefabExtension1),
                typeof(ClanPartiesPrefabExtension2),
            });
            Extender.Enable();
        }
    }
}