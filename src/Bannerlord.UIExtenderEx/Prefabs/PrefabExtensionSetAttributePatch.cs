namespace Bannerlord.UIExtenderEx.Prefabs
{
    /// <summary>
    /// Patch that adds or replaces node's attribute specified by XPath with node from prefab extension
    /// </summary>
    public abstract class PrefabExtensionSetAttributePatch : IPrefabPatch
    {
        /// <summary>
        /// Name of the extension snippet, without `.xml`
        /// </summary>
        public abstract string Id { get; }
        public abstract string Attribute { get; }
        public abstract string Value { get; }
    }
}