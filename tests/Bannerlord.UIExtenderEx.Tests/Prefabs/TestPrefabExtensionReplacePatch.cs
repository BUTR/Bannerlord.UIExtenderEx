using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs
{
    [PrefabExtension("ReplaceKeepChildren", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='ReplaceKeepChildren']")]
    internal class TestPrefabExtensionReplacePatch : PrefabExtensionReplacePatch
    {
        public override string Id => "ReplaceKeepChildren";
        private XmlDocument XmlDocument { get; } = new();

        public TestPrefabExtensionReplacePatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"Replaced\" />");
        }

        public override XmlDocument GetPrefabExtension() => XmlDocument;
    }
}