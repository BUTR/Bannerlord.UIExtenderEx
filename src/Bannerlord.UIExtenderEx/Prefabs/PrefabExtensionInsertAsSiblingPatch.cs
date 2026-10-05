using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs;

/// <summary>
/// Inserts an extension snippet as a sibling to the node selected via XPath.
/// </summary>
public abstract class PrefabExtensionInsertAsSiblingPatch : IPrefabPatch
{
    /// <summary>
    /// Defines sibling placement relative to the target node.
    /// </summary>
    public enum InsertType
    {
        /// <summary>Places snippet before the target sibling node.</summary>
        Prepend,
        /// <summary>Places snippet after the target sibling node.</summary>
        Append
    }

    /// <summary>
    /// Gets the sibling insertion placement.
    /// </summary>
    public virtual InsertType Type => InsertType.Append;

    /// <summary>
    /// Gets the name of the extension snippet without the file extension.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Retrieves the XML document containing the extension snippet.
    /// </summary>
    public abstract XmlDocument GetPrefabExtension();
}