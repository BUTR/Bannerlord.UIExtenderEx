using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs
{
    /// <summary>
    /// Base class for insert patches
    /// </summary>
    public abstract class InsertPatch : IPrefabPatch
    {
        /// <summary>
        /// Constant that will insert snippet at the very beginning
        /// </summary>
        public const int PositionFirst = 0;

        /// <summary>
        /// Constant that will insert snippet at the very end
        /// </summary>
        public const int PositionLast = int.MaxValue;

        public abstract string Id { get; }

        /// <summary>
        /// Position to insert snippet at
        /// </summary>
        public abstract int Position { get; }

        public abstract XmlDocument GetPrefabExtension();
    }
}