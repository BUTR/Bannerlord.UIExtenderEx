﻿using System;

namespace UIExtenderLib.Interface
{
    /// <summary>
    /// Base class for extensions attributes
    /// </summary>
    public class UIExtenderLibExtensionAttribute : Attribute { }

    /// <summary>
    /// Attribute for mixin methods to be added to view models.
    /// Only methods specified by this attribute will actually end up in extended view model
    /// </summary>
    public class DataSourceMethodAttribute : Attribute { }

    public class CodePatcherAttribute : UIExtenderLibExtensionAttribute { }

    /// <summary>
    /// Attribute to mark view model mixins.
    /// Mixin classes should extend from `BaseViewModelMixin<T>` and should be marked with this attribute
    /// </summary>
    public class ViewModelMixinAttribute : UIExtenderLibExtensionAttribute { } 
    
    /// <summary>
    /// Attribute for prefab XML extensions.
    /// Extension classes should inherit from one of the `IPrefabPatch` base classes and should be marked with this attribute
    /// </summary>
    public class PrefabExtensionAttribute : UIExtenderLibExtensionAttribute
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