using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs.IntegrationTests.TestPrefabs;

[PrefabExtension("Insert", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children")]
#pragma warning disable CS0618
internal class TestPrefabExtensionInsertPatch : PrefabExtensionInsertPatch
#pragma warning restore CS0618
{
    public override string Id => "Insert";
    public override int Position => 3;
    private XmlDocument XmlDocument { get; } = new();

    public TestPrefabExtensionInsertPatch()
    {
        XmlDocument.LoadXml("<OptionsTabToggle Id=\"Insert\" />");
    }

    public override XmlDocument GetPrefabExtension() => XmlDocument;
}