using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("InsertAsSiblingAppend2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='InsertAsSibling']")]
    internal class TestPrefabExtensionInsertAsSiblingAppendPatch : PrefabExtensionInsertAsSiblingPatch
    {
        public override InsertType Type => InsertType.Append;
        private XmlDocument XmlDocument { get; } = new();

        public TestPrefabExtensionInsertAsSiblingAppendPatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"InsertAsSiblingAppend\" />");
        }

        public override XmlNode GetPrefabExtension() => XmlDocument.DocumentElement!;
    }
}