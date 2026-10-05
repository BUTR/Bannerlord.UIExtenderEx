using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs;

/// <summary>
/// Base class for legacy prefab snippet insertion patches.
/// </summary>
public abstract class InsertPatch : IPrefabPatch
{
    /// <summary>
    /// Inserts the snippet at the first child position.
    /// </summary>
    public const int PositionFirst = 0;

    /// <summary>
    /// Inserts the snippet at the last child position.
    /// </summary>
    public const int PositionLast = int.MaxValue;

    public abstract string Id { get; }

    /// <summary>
    /// Gets the zero-based child position at which to insert the snippet.
    /// </summary>
    public abstract int Position { get; }

    /// <summary>
    /// Retrieves the XML document containing the extension snippet.
    /// </summary>
    public abstract XmlDocument GetPrefabExtension();
}