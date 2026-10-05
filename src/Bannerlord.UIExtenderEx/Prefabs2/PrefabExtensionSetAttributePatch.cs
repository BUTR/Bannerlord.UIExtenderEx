using Bannerlord.UIExtenderEx.Attributes;

using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.Prefabs2;

/// <summary>
/// Sets or updates XML attributes on the target node specified by <see cref="PrefabExtensionAttribute.XPath"/>.
/// </summary>
public abstract class PrefabExtensionSetAttributePatch
{
    /// <summary>
    /// Gets the collection of attributes to set on the target node.
    /// </summary>
    public abstract List<Attribute> Attributes { get; }

    /// <summary>
    /// Represents an XML attribute name-value pair.
    /// </summary>
    public readonly struct Attribute
    {
        public Attribute(string name, string value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>Gets the attribute name.</summary>
        public string Name { get; }
        /// <summary>Gets the attribute value.</summary>
        public string Value { get; }
    }
}