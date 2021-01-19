using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("InsertAsSiblingPrepend2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='InsertAsSibling']")]
    internal class TestPrefabExtensionInsertAsSiblingPrependPatch : PrefabExtensionInsertAsSiblingPatch
    {
        public override InsertType Type => InsertType.Prepend;
        private XmlDocument XmlDocument { get; } = new();

        public TestPrefabExtensionInsertAsSiblingPrependPatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"InsertAsSiblingPrepend\" />");
        }

        public override XmlNode GetPrefabExtension() => XmlDocument.DocumentElement!;
    }
}