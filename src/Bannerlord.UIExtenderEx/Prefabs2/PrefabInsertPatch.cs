using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Base class for insert patches
    /// </summary>
    public abstract class PrefabInsertPatch : IPrefabPatch
    {
        /// <summary>
        /// Constant that will insert snippet at the very beginning
        /// </summary>
        public const int PositionFirst = 0;

        /// <summary>
        /// Constant that will insert snippet at the very end
        /// </summary>
        public const int PositionLast = int.MaxValue;

        /// <summary>
        /// Position to insert snippet at
        /// </summary>
        public abstract int Position { get; }

        public abstract XmlNode GetPrefabExtension();
    }
}