using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Patch that inserts prefab extension as a sibling to node specified by Xpath.
    /// Order is controlled by `Type` property.
    /// </summary>
    public abstract class PrefabExtensionInsertAsSiblingPatch : IPrefabPatch
    {
        /// <summary>
        /// Insert type enum - Prepend inserts snippet before sibling, Append - after
        /// </summary>
        public enum InsertType { Prepend, Append }

        /// <summary>
        /// Type of the insert
        /// </summary>
        public virtual InsertType Type => InsertType.Append;

        public abstract XmlNode GetPrefabExtension();
    }
}