using Bannerlord.UIExtenderEx.Prefabs;
using Bannerlord.UIExtenderEx.Utils;

using System;
using System.Xml;

using V2 = Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Components;

/// <summary>
/// Manages registration, filtering, and execution of Gauntlet prefab XML patches.
/// </summary>
/// <remarks>
/// Legacy v1 patch directives are converted internally to equivalent v2 placement directives.
/// </remarks>
internal partial class PrefabComponent
{
    /// <summary>
    /// Registers a legacy snippet insert patch.
    /// </summary>
    /// <param name="movie">The target movie name.</param>
    /// <param name="xpath">The XPath expression selecting the insertion point.</param>
    /// <param name="patch">The legacy insert patch.</param>
    [Obsolete("Use Prefabs2.PrefabExtensionInsertPatch instead.")]
    public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertPatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
    {
        if (GetExtensionNode(movie, patch.GetPrefabExtension()) is { } extensionNode)
            PlaceNodes(movie, node, [extensionNode], V2.InsertType.Child, ToChildIndex(patch.Position));
    });

    /// <summary>
    /// Registers a legacy snippet attribute modification patch.
    /// </summary>
    /// <param name="movie">The target movie name.</param>
    /// <param name="xpath">The XPath expression selecting the target element.</param>
    /// <param name="patch">The legacy set-attribute patch.</param>
    public void RegisterPatch(string movie, string? xpath, PrefabExtensionSetAttributePatch patch) =>
        RegisterPatch(movie, xpath, patch.GetType(), node => SetAttributes(node, [new(patch.Attribute, patch.Value)]));

    /// <summary>
    /// Registers a legacy snippet node replacement patch.
    /// </summary>
    /// <param name="movie">The target movie name.</param>
    /// <param name="xpath">The XPath expression selecting the node to replace.</param>
    /// <param name="patch">The legacy replace patch.</param>
    public void RegisterPatch(string movie, string? xpath, PrefabExtensionReplacePatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
    {
        if (GetExtensionNode(movie, patch.GetPrefabExtension()) is { } extensionNode)
            PlaceNodes(movie, node, [extensionNode], V2.InsertType.Replace, 0);
    });

    /// <summary>
    /// Registers a legacy snippet sibling insertion patch.
    /// </summary>
    /// <param name="movie">The target movie name.</param>
    /// <param name="xpath">The XPath expression selecting the reference sibling node.</param>
    /// <param name="patch">The legacy sibling insert patch.</param>
    public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertAsSiblingPatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
    {
        if (GetExtensionNode(movie, patch.GetPrefabExtension()) is not { } extensionNode)
            return;

        switch (patch.Type)
        {
            case PrefabExtensionInsertAsSiblingPatch.InsertType.Append:
                PlaceNodes(movie, node, [extensionNode], V2.InsertType.Append, 0);
                break;

            case PrefabExtensionInsertAsSiblingPatch.InsertType.Prepend:
                PlaceNodes(movie, node, [extensionNode], V2.InsertType.Prepend, 0);
                break;
        }
    });

    /// <summary>
    /// Retrieves the root <see cref="XmlNode"/> from a legacy patch document after stripping comments.
    /// </summary>
    private static XmlNode? GetExtensionNode(string movie, XmlDocument extension)
    {
        if (extension.DocumentElement is not { } extensionNode)
        {
            MessageUtils.Fail($"XML patch document for {movie} is null!");
            return null;
        }

        if (!TryRemoveComments(extensionNode))
        {
            MessageUtils.Fail($"XML patch document's root node was a comment.");
            return null;
        }

        return extensionNode;
    }

    /// <summary>
    /// Translates legacy v1 0-based insertion positions to v2 child indices.
    /// Preserves <see cref="InsertPatch.PositionLast"/> saturation semantics.
    /// </summary>
    private static int ToChildIndex(int position) => position < InsertPatch.PositionLast ? Math.Max(position, 0) + 1 : InsertPatch.PositionLast;
}