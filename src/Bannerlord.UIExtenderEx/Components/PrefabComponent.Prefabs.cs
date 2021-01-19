using Bannerlord.UIExtenderEx.Prefabs;

using System;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Components
{
    /// <summary>
    /// Component that deals with Gauntlet prefab XML files
    /// </summary>
    internal partial class PrefabComponent
    {
        /// <summary>
        /// Register snippet insert patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertPatch patch) => RegisterPatch(movie, xpath, node =>
        {
            if (node.OwnerDocument is not { } ownerDocument)
            {
                Utils.Fail($"XML original document for {movie} is null!");
                return;
            }

            if (patch.GetPrefabExtension().DocumentElement is not { } extensionNode)
            {
                Utils.Fail($"XML patch document for {movie} is null!");
                return;
            }

            var importedExtensionNode = ownerDocument.ImportNode(extensionNode, true);
            var position = Math.Min(patch.Position, node.ChildNodes.Count - 1);
            position = Math.Max(position, 0);
            if (position >= node.ChildNodes.Count)
            {
                Utils.Fail($"Invalid position ({position}) for insert (patching in {patch.Id})");
                return;
            }

            node.InsertAfter(importedExtensionNode, node.ChildNodes[position]);
        });

        /// <summary>
        /// Register snippet set attribute patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionSetAttributePatch patch) => RegisterPatch(movie, xpath, node =>
        {
            if (node.OwnerDocument is not { } ownerDocument)
            {
                return;
            }

            if (node.NodeType != XmlNodeType.Element)
            {
                return;
            }

            if (node.Attributes![patch.Attribute] is null)
            {
                var attribute = ownerDocument.CreateAttribute(patch.Attribute);
                node.Attributes.Append(attribute);
            }

            node.Attributes![patch.Attribute].Value = patch.Value;
        });

        /// <summary>
        /// Register snippet replace patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionReplacePatch patch) => RegisterPatch(movie, xpath, node =>
        {
            if (node.OwnerDocument is not { } ownerDocument)
            {
                Utils.Fail($"XML original document for {movie} is null!");
                return;
            }

            if (node.ParentNode is null)
            {
                Utils.Fail($"XML original document parent node for {movie} is null!");
                return;
            }

            if (patch.GetPrefabExtension().DocumentElement is not { } extensionNode)
            {
                Utils.Fail($"XML patch document for {movie} is null!");
                return;
            }

            var importedExtensionNode = ownerDocument.ImportNode(extensionNode, true);

            node.ParentNode.ReplaceChild(importedExtensionNode, node);
        });

        /// <summary>
        /// Register snippet insert as sibling patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertAsSiblingPatch patch) => RegisterPatch(movie, xpath, node =>
        {
            if (node.OwnerDocument is not { } ownerDocument)
            {
                Utils.Fail($"XML original document for {movie} is null!");
                return;
            }

            if (node.ParentNode is null)
            {
                Utils.Fail($"XML original document parent node for {movie} is null!");
                return;
            }

            if (patch.GetPrefabExtension().DocumentElement is not { } extensionNode)
            {
                Utils.Fail($"XML patch document for {movie} is null!");
                return;
            }

            var importedExtensionNode = ownerDocument.ImportNode(extensionNode, true);

            switch (patch.Type)
            {
                case PrefabExtensionInsertAsSiblingPatch.InsertType.Append:
                    node.ParentNode.InsertAfter(importedExtensionNode, node);
                    break;

                case PrefabExtensionInsertAsSiblingPatch.InsertType.Prepend:
                    node.ParentNode.InsertBefore(importedExtensionNode, node);
                    break;
            }
        });
    }
}