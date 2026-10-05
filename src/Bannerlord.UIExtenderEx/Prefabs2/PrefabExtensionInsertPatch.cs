using Bannerlord.UIExtenderEx.Attributes;

namespace Bannerlord.UIExtenderEx.Prefabs2;

/// <summary>
/// Inserts content relative to the target node specified by <see cref="PrefabExtensionAttribute.XPath"/>.
/// A single property or method must be decorated with <see cref="PrefabExtensionContentAttribute"/>.
/// <para>
/// Supported content attribute types:
/// <list type="bullet">
/// <item><see cref="PrefabExtensionFileNameAttribute"/></item>
/// <item><see cref="PrefabExtensionTextAttribute"/></item>
/// <item><see cref="PrefabExtensionXmlNodeAttribute"/></item>
/// <item><see cref="PrefabExtensionXmlNodesAttribute"/></item>
/// </list>
/// </para>
/// </summary>
public abstract partial class PrefabExtensionInsertPatch
{
    /// <summary>
    /// Gets the placement strategy for the extension content relative to the target node.
    /// </summary>
    public abstract InsertType Type { get; }

    /// <summary>
    /// Gets the zero-based insertion index used when <see cref="Type"/> is <see cref="InsertType.Child"/> or <see cref="InsertType.ReplaceKeepChildren"/>.
    /// </summary>
    public virtual int Index { get; } = 0;
}