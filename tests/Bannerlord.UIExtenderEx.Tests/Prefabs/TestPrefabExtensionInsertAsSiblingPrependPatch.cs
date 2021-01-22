using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs
{
    [PrefabExtension("InsertAsSiblingPrepend", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='InsertAsSibling']")]
    internal class TestPrefabExtensionInsertAsSiblingPrependPatch : PrefabExtensionInsertAsSiblingPatch
    {
        public override string Id => "InsertAsSiblingPrepend";
        public override InsertType Type => InsertType.Prepend;
        private XmlDocument XmlDocument { get; } = new();

        public TestPrefabExtensionInsertAsSiblingPrependPatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"InsertAsSiblingPrepend\" />");
        }

        public override XmlDocument GetPrefabExtension() => XmlDocument;
    }
}