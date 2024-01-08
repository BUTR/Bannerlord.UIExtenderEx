using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2;

[PrefabExtension("AppendRemoveRootNode", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel")]
internal class TestPrefabExtensionInsertXmlDocumentPatchRemoveRootNode : PrefabExtensionInsertPatch
{
    private XmlDocument XmlDocument { get; } = new();

    public override int Index => 3;

    public override InsertType Type => InsertType.Append;

    public TestPrefabExtensionInsertXmlDocumentPatchRemoveRootNode()
    {
        XmlDocument.LoadXml("<DiscardedRoot>" +
                            "<OptionsTab Id=\"Append1\" />" +
                            "<OptionsTab Id=\"Append2\" />" +
                            "</DiscardedRoot>");
    }

    [PrefabExtensionXmlDocument(true)]
    public virtual XmlDocument GetPrefabExtension() => XmlDocument;
}