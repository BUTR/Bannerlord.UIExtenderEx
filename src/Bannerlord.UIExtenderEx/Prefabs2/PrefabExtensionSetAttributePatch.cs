using System.Collections.Generic;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Patch that adds or replaces node's attributes specified by XPath with node from prefab extension
    /// </summary>
    public abstract class PrefabExtensionSetAttributePatch
    {
        public abstract List<Attribute> Attributes { get; }

        public readonly struct Attribute
        {
            public Attribute(string name, string value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }
            public string Value { get; }
        }

        internal void ModifyAttributes(XmlElement element)
        {
            foreach (Attribute attribute in Attributes)
            {
                element.SetAttribute(attribute.Name, attribute.Value);
            }
        }
    }
}