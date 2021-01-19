using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("SetAttribute2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='SetAttribute']")]
    internal class TestPrefabExtensionSetAttributePatch : PrefabExtensionSetAttributePatch
    {
        public override string Attribute => "CustomAttribute";
        public override string Value => "Value";
    }
}