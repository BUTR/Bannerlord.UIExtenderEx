using System.Xml;

namespace UIExtenderLib.Prefab
{
    /**
     * General interface for XML patch object
     */
    public interface IPrefabPatch
    {
    }
    
    /**
     * Custom patch on either whole XmlDocument (if T is XmlDocument) or Xpath specified node (if XmlNode is the generic argument)
     */
    public abstract class CustomPatch<T> : IPrefabPatch where T: XmlNode
    {
        public abstract void Apply(T document);
    }
    
    /**
     * Base class for insert patches
     */
    public abstract class InsertPatch : IPrefabPatch
    {
        public static int PositionFirst = 0;
        public static int PositionLast = int.MaxValue;
        
        public abstract int Position { get; }
    }
    
    /**
     * Patch that inserts prefab extension (specified by `Name`) as a child in XPath specified node, at specific position (`Position` property)
     */
    public abstract class PrefabExtensionInsertPatch: InsertPatch
    {
        public abstract string Name { get; }
    }

    /**
     * Patch that replaces node specified by XPath with node from prefab extension
     */
    public abstract class PrefabExtensionReplacePatch : IPrefabPatch
    {
        public abstract string Name { get; }
    }

    /**
     * Patch that inserts prefab extension as a sibling to node specified by Xpath.
     * Order is controlled by `Type` property.
     */
    public abstract class PrefabExtensionInsertAsSiblingPatch
    {
        public enum InsertType
        {
            Prepend,
            Append,
        }

        public virtual InsertType Type => InsertType.Append;
        public abstract string Name { get; }
    }
}