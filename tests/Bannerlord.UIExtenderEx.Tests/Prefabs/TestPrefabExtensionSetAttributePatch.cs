using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs
{
    [PrefabExtension("SetAttribute", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='SetAttribute']")]
    internal class TestPrefabExtensionSetAttributePatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "SetAttribute";
        public override string Attribute => "CustomAttribute";
        public override string Value => "Value";
    }
}