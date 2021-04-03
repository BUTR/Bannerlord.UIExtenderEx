using System;

namespace Bannerlord.UIExtenderEx.Attributes
{
    /// <summary>
    /// Attribute for prefab XML extensions.
    /// Extension classes should inherit from one of the `IPrefabPatch` base classes and should be marked with this attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class PrefabExtensionAttribute : BaseUIExtenderAttribute
    {
        /// <summary>
        /// Gauntlet Movie name to extend
        /// </summary>
        public string Movie { get; }

        /// <summary>
        /// XPath of the node to operate against (optional)
        /// </summary>
        public string? XPath { get; }

        /// <summary>
        /// Gauntlet Movie name to prevent from loading as an auto-generated Widget (optional)
        /// </summary>
        public string? AutoGenWidgetName { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="movie">Gauntlet Movie name to extend</param>
        /// <param name="xpath">XPath of the node to operate against (optional)</param>
        public PrefabExtensionAttribute(string movie, string? xpath = null)
        {
            Movie = movie;
            XPath = xpath;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="movie">Gauntlet Movie name to extend</param>
        /// <param name="xpath">XPath of the node to operate against (optional)</param>
        /// <param name="autoGenWidgetName">Gauntlet Movie name to prevent from loading as an auto-generated Widget (optional)</param>
        public PrefabExtensionAttribute(string movie, string? xpath = null, string? autoGenWidgetName = null)
        {
            Movie = movie;
            XPath = xpath;
            AutoGenWidgetName = autoGenWidgetName;
        }
    }
}