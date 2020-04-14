using System;

namespace UIExtenderLib.Interface
{
    /// <summary>
    /// Base class for extensions attributes
    /// </summary>
    public class UIExtenderLibExtension: Attribute { }

    /// <summary>
    /// Attribute for mixin methods to be added to view models.
    /// Only methods specified by this attribute will actually end up in extended view model
    /// </summary>
    public class DataSourceMethod : Attribute { }

    /// <summary>
    /// Attribute to mark view model mixins.
    /// Mixin classes should extend from `BaseViewModelMixin<T>` and should be marked with this attribute
    /// </summary>
    public class ViewModelMixin : UIExtenderLibExtension { } 
    
    /// <summary>
    /// Attribute for prefab XML extensions.
    /// Extension classes should inherit from one of the `IPrefabPatch` base classes and should be marked with this attribute
    /// </summary>
    public class PrefabExtension : UIExtenderLibExtension
    {
        /// <summary>
        /// Gauntlet Movie name to extend
        /// </summary>
        public string Movie { get;  }
        
        /// <summary>
        /// XPath of the node to operate against (optional)
        /// </summary>
        public string XPath { get;  }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="movie">Gauntlet Movie name to extend</param>
        /// <param name="xpath">XPath of the node to operate against (optional)</param>
        public PrefabExtension(string movie, string xpath = null) : base()
        {
            Movie = movie;
            XPath = xpath;
        }
    }
}