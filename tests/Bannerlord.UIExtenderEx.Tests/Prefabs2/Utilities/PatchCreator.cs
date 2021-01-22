using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;

using Bannerlord.UIExtenderEx.Prefabs2;

using NSubstitute;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2.Utilities
{
	public static class PatchCreator
	{
		public static PrefabExtensionInsertPatch ConstructInsertPatch<T>(InsertType insertType, T contentValue, int index = 0)
		{
			PrefabExtensionInsertPatch? patch;

			// To pass XmlDocument as XmlNode
			if (typeof(T) == typeof(XmlNode) && contentValue is XmlNode nodeContent)
			{
				var nodePatch = Substitute.ForPartsOf<TestPrefabExtensionInsertXmlNodePatch>();
				nodePatch.GetPrefabExtension().Returns(nodeContent);
				patch = nodePatch;
			}
			else
			{
				switch (contentValue)
				{
					case string textPatchContent:
						var textPatch = Substitute.ForPartsOf<TestPrefabExtensionInsertTextPatch>();
						textPatch.GetPrefabExtension().Returns(textPatchContent);
						patch = textPatch;
						break;
					case XmlDocument documentPatchContent:
						var documentPatch = Substitute.ForPartsOf<TestPrefabExtensionInsertXmlDocumentPatch>();
						documentPatch.GetPrefabExtension().Returns(documentPatchContent);
						patch = documentPatch;
						break;
					case XmlNode nodePatchContent:
						var nodePatch = Substitute.ForPartsOf<TestPrefabExtensionInsertXmlNodePatch>();
						nodePatch.GetPrefabExtension().Returns(nodePatchContent);
						patch = nodePatch;
						break;
					case IEnumerable<XmlNode> nodesPatchContent:
						var nodesPatch = Substitute.ForPartsOf<TestPrefabExtensionInsertXmlNodesPatch>();
						nodesPatch.GetPrefabExtension().Returns(nodesPatchContent);
						patch = nodesPatch;
						break;
					default: throw new ArgumentOutOfRangeException();
				}
			}

			patch!.Index.Returns(index);
			patch.Type.Returns(insertType);

			return patch;
		}
	}
}