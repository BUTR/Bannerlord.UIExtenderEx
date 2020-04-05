using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Xml;

namespace UIExtenderLib
{
    public class UIExtender
    {
        // global list containing all extension types
        public static List<Type> Extensions = new List<Type>();

        /**
         * Register extension types from calling assembly
         */
        public static void Register()
        {
            Register(Assembly.GetCallingAssembly());
        }

        /**
         * Register extension types from assembly
         */
        public static void Register(Assembly assembly)
        {
            var types = assembly
                .GetTypes()
                .Where(t => t.CustomAttributes.Any(a => a.AttributeType.IsSubclassOf(typeof(UIExtenderLibExtension))));
            
            Extensions.AddRange(types);
        }
    }
}