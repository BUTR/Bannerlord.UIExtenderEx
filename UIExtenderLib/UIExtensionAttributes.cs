using System;

namespace UIExtenderLib
{
    /**
     * Base class for extensions attribte
     */
    public class UIExtenderLibExtension: Attribute { }

    /**
     * Attribute for mixin methods to be added to view models
     */
    public class DataSourceMethod : Attribute { }

    /**
     * Attribute to mark view model mixins
     */
    public class ViewModelMixin : UIExtenderLibExtension { } 
    
    /**
     * Attribute for gauntlet XML file extensions
     */
    public class PrefabExtension : UIExtenderLibExtension
    {
        // movie to extend
        public string Movie { get;  }
        // xpath of node to operate against (optional)
        public string XPath { get;  }
        
        public PrefabExtension(string movie, string xpath = null) : base()
        {
            Movie = movie;
            XPath = xpath;
        }
    }
}