using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Patch that replaces node specified by XPath with node from prefab extension
    /// </summary>
    public abstract class PrefabExtensionReplacePatch : IPrefabPatch
    {
        public abstract XmlNode GetPrefabExtension();
    }
}