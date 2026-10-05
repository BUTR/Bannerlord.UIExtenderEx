using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs;

/// <summary>
/// Defines a custom prefab patch operating directly on an <see cref="XmlDocument"/> or an <see cref="XmlNode"/> selected via XPath.
/// </summary>
/// <typeparam name="T">The XML node type targeted by the patch.</typeparam>
public abstract class CustomPatch<T> : IPrefabPatch where T : XmlNode
{
    public abstract string Id { get; }

    /// <summary>
    /// Applies modifications to the targeted XML document or node.
    /// </summary>
    /// <param name="obj">The XML document or node to modify.</param>
    public abstract void Apply(T obj);
}