using Bannerlord.UIExtenderEx.Attributes;

using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Patch that adds or replaces node's attributes specified by <see cref="PrefabExtensionAttribute.XPath"/> with node from prefab extension
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
    }
}