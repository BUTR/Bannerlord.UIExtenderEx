using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs;

/// <summary>
/// Replaces the node selected via XPath with an extension snippet.
/// </summary>
public abstract class PrefabExtensionReplacePatch : IPrefabPatch
{
    /// <summary>
    /// Gets the name of the extension snippet without the file extension.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Retrieves the XML document containing the replacement snippet.
    /// </summary>
    public abstract XmlDocument GetPrefabExtension();
}