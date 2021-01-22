using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("PrependRemoveRootNode", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel")]
    internal class TestPrefabExtensionInsertXmlNodePatchRemoveRootNode : PrefabExtensionInsertPatch
    {
        private XmlDocument XmlDocument { get; } = new();

        public override int Index => 3;

        public override InsertType Type => InsertType.Prepend;

        public TestPrefabExtensionInsertXmlNodePatchRemoveRootNode()
        {
            XmlDocument.LoadXml("<DiscardedRoot>" +
                                "<OptionsTab Id=\"Prepend1\" />" +
                                "<OptionsTab Id=\"Prepend2\" />" +
                                "</DiscardedRoot>");
        }

        [PrefabExtensionXmlNode(true)]
        public virtual XmlNode GetPrefabExtension() => XmlDocument.DocumentElement!;
    }
}