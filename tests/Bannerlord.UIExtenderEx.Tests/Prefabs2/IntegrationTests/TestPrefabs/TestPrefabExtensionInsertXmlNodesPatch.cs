using System.Collections.Generic;
using System.Linq;
using System.Xml;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("Insert2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children")]
    internal class TestPrefabExtensionInsertXmlNodesPatch : PrefabExtensionInsertPatch
    {
        private XmlDocument XmlDocument { get; } = new();

        public override int Index => 3;

        public override InsertType Type => InsertType.Child;

        public TestPrefabExtensionInsertXmlNodesPatch()
        {
            XmlDocument.LoadXml("<DiscardedRoot><OptionsTabToggle Id=\"Insert\"/></DiscardedRoot>");
        }

        [PrefabExtensionXmlNodes]
        public virtual IEnumerable<XmlNode> GetPrefabExtension() => XmlDocument.DocumentElement!.ChildNodes.Cast<XmlNode>();
    }
}
