namespace Bannerlord.UIExtenderEx.Prefabs;

/// <summary>
/// Sets or updates an attribute on the node selected via XPath.
/// </summary>
public abstract class PrefabExtensionSetAttributePatch : IPrefabPatch
{
    /// <summary>
    /// Gets the unique identifier of the patch.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Gets the name of the attribute to set.
    /// </summary>
    public abstract string Attribute { get; }

    /// <summary>
    /// Gets the value to assign to the attribute.
    /// </summary>
    public abstract string Value { get; }
}