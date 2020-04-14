using System.Xml;

namespace UIExtenderLib.Interface
{
    /// <summary>
    /// General interface for XML prefab patch
    /// </summary>
    public interface IPrefabPatch
    {
    }
    
    /// <summary>
    /// Custom patch on either whole XmlDocument (if T is XmlDocument) or Xpath specified node (if XmlNode is the generic argument)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class CustomPatch<T> : IPrefabPatch where T: XmlNode
    {
        /// <summary>
        /// Apply this patch to obj
        /// </summary>
        /// <param name="obj"></param>
        public abstract void Apply(T obj);
    }
    
    /// <summary>
    /// Base class for insert patches
    /// </summary>
    public abstract class InsertPatch : IPrefabPatch
    {
        /// <summary>
        /// Constant that will insert snippet at the very beginning
        /// </summary>
        public static int PositionFirst = 0;
        
        /// <summary>
        /// Constant that will insert snippet at the very end
        /// </summary>
        public static int PositionLast = int.MaxValue;
        
        /// <summary>
        /// Position to insert snippet at
        /// </summary>
        public abstract int Position { get; }
    }
    
    /// <summary>
    /// Patch that inserts prefab extension (specified by `Name`) as a child in XPath specified node, at specific position (`Position` property)
    /// Extension snippet should be named as `{Name}.xml` and located at module's `GUI/PrefabExtensions` folder.
    /// </summary>
    public abstract class PrefabExtensionInsertPatch: InsertPatch
    {
        /// <summary>
        /// Name of the extension snippet, without `.xml`
        /// </summary>
        public abstract string Name { get; }
    }

    /// <summary>
    /// Patch that replaces node specified by XPath with node from prefab extension
    /// </summary>
    public abstract class PrefabExtensionReplacePatch : IPrefabPatch
    {
        /// <summary>
        /// Name of the extension snippet, without `.xml`
        /// </summary>
        public abstract string Name { get; }
    }

    /// <summary>
    /// Patch that inserts prefab extension as a sibling to node specified by Xpath.
    /// Order is controlled by `Type` property.
    /// </summary>
    public abstract class PrefabExtensionInsertAsSiblingPatch: IPrefabPatch
    {
        /// <summary>
        /// Insert type enum - Prepend inserts snippet before sibling, Append - after
        /// </summary>
        public enum InsertType
        {
            Prepend,
            Append,
        }

        /// <summary>
        /// Type of the insert
        /// </summary>
        public virtual InsertType Type => InsertType.Append;
        
        /// <summary>
        /// Name of the extension snippet, without `.xml`
        /// </summary>
        public abstract string Name { get; }
    }
}