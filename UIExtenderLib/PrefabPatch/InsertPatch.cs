using System.Xml;

namespace UIExtenderLib.Interface
{
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

        public abstract string Id { get; }

        /// <summary>
        /// Position to insert snippet at
        /// </summary>
        public abstract int Position { get; }

        public abstract XmlDocument GetPrefabExtension();
    }
}