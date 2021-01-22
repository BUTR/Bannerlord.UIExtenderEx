using System.Xml;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
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

		[PrefabExtensionXmlDocument()]
		public virtual XmlDocument GetPrefabExtension() => XmlDocument;
	}
}