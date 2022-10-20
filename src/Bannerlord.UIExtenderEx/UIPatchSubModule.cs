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
        private sealed class ClanPartiesPrefabExtensionPre180_1 : PrefabExtensionSetAttributePatch
        {
            public override List<Attribute> Attributes => new()
            {
                new Attribute("IsEnabled", "@CanUseActions")
            };
        }
        [PrefabExtension("ClanParties", "descendant::Prefab/Window/Widget/Children/ListPanel/Children/Widget/Children/Widget/Children/Widget[2]/Children/ListPanel[1]/Children/ListPanel[3]/Children/Standard.DropdownWithHorizontalControl")]
        private sealed class ClanPartiesPrefabExtensionPre180_2 : PrefabExtensionSetAttributePatch
        {
            public override List<Attribute> Attributes => new()
            {
                new Attribute("Parameter.IsEnabled", "true")
            };
        }

        [PrefabExtension("ClanParties", "descendant::Prefab/Window/Widget/Children/ListPanel/Children/Widget/Children/Widget/Children/Widget/Children/ListPanel/Children/ListPanel")]
        private sealed class ClanPartiesPrefabExtensionPost180_1 : PrefabExtensionSetAttributePatch
        {
            public override List<Attribute> Attributes => new()
            {
                new Attribute("IsEnabled", "@CanUseActions")
            };
        }
        [PrefabExtension("ClanParties", "descendant::Prefab/Window/Widget/Children/ListPanel/Children/Widget/Children/Widget/Children/Widget/Children/ListPanel/Children/ListPanel/Children/Standard.DropdownWithHorizontalControl")]
        [PrefabExtension("ClanParties", "descendant::Prefab/Window/Widget/Children/ListPanel/Children/Widget/Children/Widget/Children/Widget/Children/ListPanel/Children/ListPanel/Children/Standard.DropdownWithHorizontalControl")]
        private sealed class ClanPartiesPrefabExtensionPost180_2 : PrefabExtensionSetAttributePatch
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
                if (gameVersion.Major >= 1 && gameVersion.Minor >= 7)
                {
                    Harmony.TryPatch(
                        AccessTools2.DeclaredMethod("SandBox.SandBoxSubModule:OnSubModuleLoad"),
                        postfix: AccessTools2.DeclaredMethod("Bannerlord.UIExtenderEx.UIPatchSubModule:SandBoxSubModuleOnSubModuleLoadPostfix"));
                }
            }
        }

        private static void SandBoxSubModuleOnSubModuleLoadPostfix()
        {
            if (ApplicationVersionHelper.GameVersion() is { } gameVersion)
            {
                if (gameVersion.Major >= 1 && gameVersion.Minor >= 8)
                {
                    Extender.Register(new[]
                    {
                        typeof(ClanPartiesPrefabExtensionPost180_1),
                        typeof(ClanPartiesPrefabExtensionPost180_2),
                    });
                }
                else
                {
                    Extender.Register(new[]
                    {
                        typeof(ClanPartiesPrefabExtensionPre180_1),
                        typeof(ClanPartiesPrefabExtensionPre180_2),
                    });
                }
            }

            Extender.Enable();
        }
    }
}