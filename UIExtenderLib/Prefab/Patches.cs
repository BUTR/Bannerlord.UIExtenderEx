using System.Xml;

namespace UIExtenderLib.Prefab
{
    /**
     * General interface for XML patch object
     */
    public interface IPrefabPatch
    {
    }
    
    public abstract class CustomPatch<T> : IPrefabPatch where T: XmlNode
    {
        public abstract void Apply(T document);
    }
    
    public abstract class InsertPatch : IPrefabPatch
    {
        public static int PositionFirst = 0;
        public static int PositionLast = int.MaxValue;
        
        public abstract int Position { get; }
    }
    
    public abstract class PrefabExtensionInsertPatch: InsertPatch
    {
        public abstract string Name { get; }
    }
}