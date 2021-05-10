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
        public static PrefabExtensionInsertPatch ConstructInsertPatchPath(InsertType insertType, string path, int index = 0, bool removeRootNode = false)
        {
            var patch = CreatePatchPath(path, removeRootNode);

            patch!.Index.Returns(index);
            patch.Type.Returns(insertType);

            return patch;
        }

        public static PrefabExtensionInsertPatch ConstructInsertPatch<T>(InsertType insertType, T contentValue, int index = 0, bool removeRootNode = false)
        {
            PrefabExtensionInsertPatch? patch;

            // To pass XmlDocument as XmlNode
            if (typeof(T) == typeof(XmlNode) && contentValue is XmlNode nodeContent)
            {
                patch = CreatePatch(nodeContent, removeRootNode);
            }
            else
            {
                switch (contentValue)
                {
                    case string textPatchContent:
                        patch = CreatePatch(textPatchContent, removeRootNode);
                        break;
                    case XmlDocument documentPatchContent:
                        patch = CreatePatch(documentPatchContent, removeRootNode);
                        break;
                    case XmlNode nodePatchContent:
                        patch = CreatePatch(nodePatchContent, removeRootNode);
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

        private static PrefabExtensionInsertPatch CreatePatch(XmlNode patchContent, bool removeRootNode)
        {
            if (removeRootNode)
            {
                var removeRootNodePatch = Substitute.ForPartsOf<TestPrefabExtensionInsertXmlNodePatchRemoveRootNode>();
                removeRootNodePatch.GetPrefabExtension().Returns(patchContent);
                return removeRootNodePatch;
            }

            var patch = Substitute.ForPartsOf<TestPrefabExtensionInsertXmlNodePatch>();
            patch.GetPrefabExtension().Returns(patchContent);
            return patch;
        }

        private static PrefabExtensionInsertPatch CreatePatch(XmlDocument patchContent, bool removeRootNode)
        {
            if (removeRootNode)
            {
                var removeRootNodePatch = Substitute.ForPartsOf<TestPrefabExtensionInsertXmlDocumentPatchRemoveRootNode>();
                removeRootNodePatch.GetPrefabExtension().Returns(patchContent);
                return removeRootNodePatch;
            }

            var patch = Substitute.ForPartsOf<TestPrefabExtensionInsertXmlDocumentPatch>();
            patch.GetPrefabExtension().Returns(patchContent);
            return patch;
        }

        private static PrefabExtensionInsertPatch CreatePatch(string patchContent, bool removeRootNode)
        {
            if (removeRootNode)
            {
                var removeRootNodePatch = Substitute.ForPartsOf<TestPrefabExtensionInsertTextPatchRemoveRootNode>();
                removeRootNodePatch.GetPrefabExtension().Returns(patchContent);
                return removeRootNodePatch;
            }

            var patch = Substitute.ForPartsOf<TestPrefabExtensionInsertTextPatch>();
            patch.GetPrefabExtension().Returns(patchContent);
            return patch;
        }

        private static PrefabExtensionInsertPatch CreatePatchPath(string patchPath, bool removeRootNode)
        {
            if (removeRootNode)
            {
                var removeRootNodePatch = Substitute.ForPartsOf<TestPrefabExtensionInsertFileNamePatchRemoveRootNode>();
                removeRootNodePatch.GetPrefabExtension().Returns(patchPath);
                return removeRootNodePatch;
            }

            var patch = Substitute.ForPartsOf<TestPrefabExtensionInsertFileNamePatch>();
            patch.GetPrefabExtension().Returns(patchPath);
            return patch;
        }
    }
}