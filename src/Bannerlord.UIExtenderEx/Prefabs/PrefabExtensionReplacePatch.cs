using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs
{
    /// <summary>
    /// Patch that replaces node specified by XPath with node from prefab extension
    /// </summary>
    public abstract class PrefabExtensionReplacePatch : IPrefabPatch
    {
        /// <summary>
        /// Name of the extension snippet, without `.xml`
        /// </summary>
        public abstract string Id { get; }

        public abstract XmlDocument GetPrefabExtension();
    }
}