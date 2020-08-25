using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests
{
    [PrefabExtension("Insert", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children")]
    internal class TestPrefabExtensionInsertPatch : PrefabExtensionInsertPatch
    {
        public override string Id => "Insert";
        public override int Position => 3;
        private XmlDocument XmlDocument { get; } = new XmlDocument();

        public TestPrefabExtensionInsertPatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"Insert\" />");
        }

        public override XmlDocument GetPrefabExtension() => XmlDocument;
    }
}