using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("Replace2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='ReplaceKeepChildren']")]
    internal class TestPrefabExtensionReplacePatch : PrefabExtensionReplacePatch
    {
        private XmlDocument XmlDocument { get; } = new();

        public TestPrefabExtensionReplacePatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"Replaced\" />");
        }

        public override XmlNode GetPrefabExtension() => XmlDocument.DocumentElement!;
    }
}