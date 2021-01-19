namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Patch that adds or replaces node's attribute specified by XPath with node from prefab extension
    /// </summary>
    public abstract class PrefabExtensionSetAttributePatch : IPrefabPatch
    {
        public abstract string Attribute { get; }
        public abstract string Value { get; }
    }
}