namespace Bannerlord.UIExtenderEx.Attributes
{
    /// <summary>
    /// Attribute for prefab XML extensions.
    /// Extension classes should inherit from one of the `IPrefabPatch` base classes and should be marked with this attribute
    /// </summary>
    public sealed class PrefabExtensionAttribute : BaseUIExtenderAttribute
    {
        /// <summary>
        /// Gauntlet Movie name to extend
        /// </summary>
        public string Movie { get;  }
        
        /// <summary>
        /// XPath of the node to operate against (optional)
        /// </summary>
        public string? XPath { get;  }
        
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
    }
}