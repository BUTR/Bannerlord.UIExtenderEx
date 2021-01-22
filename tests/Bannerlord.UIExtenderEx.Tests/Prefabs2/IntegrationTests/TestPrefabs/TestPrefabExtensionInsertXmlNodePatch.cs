using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("Prepend2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel")]
    internal class TestPrefabExtensionInsertXmlNodePatch : PrefabExtensionInsertPatch
    {
        private XmlDocument XmlDocument { get; } = new();

        public override int Index => 3;

        public override InsertType Type => InsertType.Prepend;

        public TestPrefabExtensionInsertXmlNodePatch()
        {
            XmlDocument.LoadXml("<OptionsTab Id=\"Prepend\" />");
        }

        [PrefabExtensionXmlNode]
        public virtual XmlNode GetPrefabExtension() => XmlDocument.DocumentElement!;
    }
}