using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("Insert2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children")]
    internal class TestPrefabExtensionInsertPatch : PrefabExtensionInsertPatch
    {
        public override int Position => 3;
        private XmlDocument XmlDocument { get; } = new();

        public TestPrefabExtensionInsertPatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"Insert\" />");
        }

        public override XmlNode GetPrefabExtension() => XmlDocument.DocumentElement!;
    }
}