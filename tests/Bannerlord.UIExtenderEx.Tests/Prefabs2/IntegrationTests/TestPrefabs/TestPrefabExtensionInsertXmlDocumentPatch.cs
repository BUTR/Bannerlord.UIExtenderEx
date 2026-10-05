using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2;

[PrefabExtension("Append2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel")]
internal class TestPrefabExtensionInsertXmlDocumentPatch : PrefabExtensionInsertPatch
{
    private XmlDocument XmlDocument { get; } = new();

    public override int Index => 3;

    public override InsertType Type => InsertType.Append;

    public TestPrefabExtensionInsertXmlDocumentPatch()
    {
        XmlDocument.LoadXml("<OptionsTab Id=\"Append\" />");
    }

#pragma warning disable CS0618 // Existing patches still use the obsolete attribute
    [PrefabExtensionXmlDocument]
#pragma warning restore CS0618
    public virtual XmlDocument GetPrefabExtension() => XmlDocument;
}