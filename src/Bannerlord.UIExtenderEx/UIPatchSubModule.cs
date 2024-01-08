using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx;

public class UIPatchSubModule : MBSubModuleBase
{
    [PrefabExtension("ClanParties", "descendant::Prefab/Window/Widget/Children/ListPanel/Children/Widget/Children/Widget/Children/Widget/Children/ListPanel/Children/ListPanel")]
    private sealed class ClanPartiesPrefabExtensionPoste180_1 : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => new()
        {
            new Attribute("IsEnabled", "@CanUseActions")
        };
    }
    [PrefabExtension("ClanParties", "descendant::Prefab/Window/Widget/Children/ListPanel/Children/Widget/Children/Widget/Children/Widget/Children/ListPanel/Children/ListPanel/Children/Standard.DropdownWithHorizontalControl")]
    private sealed class ClanPartiesPrefabExtensionPoste180_2 : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => new()
        {
            new Attribute("Parameter.IsEnabled", "true")
        };
    }

    private static readonly UIExtender Extender = UIExtender.Create("Bannerlord.UIExtenderEx.UIPatch");
    private static readonly Harmony Harmony = new("Bannerlord.UIExtenderEx.UIPatch");

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        Harmony.TryPatch(
            AccessTools2.DeclaredMethod("SandBox.SandBoxSubModule:OnSubModuleLoad"),
            postfix: AccessTools2.DeclaredMethod(typeof(UIPatchSubModule), nameof(SandBoxSubModuleOnSubModuleLoadPostfix)));
    }

    private static void SandBoxSubModuleOnSubModuleLoadPostfix()
    {
        Extender.Register(new[]
        {
            typeof(ClanPartiesPrefabExtensionPoste180_1),
            typeof(ClanPartiesPrefabExtensionPoste180_2),
        });

        Extender.Enable();
    }
}