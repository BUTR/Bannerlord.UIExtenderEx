using System;
using System.Collections.Generic;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs2;

public abstract partial class PrefabExtensionInsertPatch
{
    /// <summary>
    /// Marks a single property or method in <see cref="PrefabExtensionInsertPatch"/> as providing the patch XML content.
    /// <para>
    /// Supported return types:
    /// <list type="bullet">
    /// <item><see cref="string"/>: File name (with <see cref="PrefabExtensionFileNameAttribute"/>) or raw XML markup (with <see cref="PrefabExtensionTextAttribute"/>).</item>
    /// <item><see cref="XmlNode"/> or <see cref="XmlDocument"/>: Direct node/document insertion (with <see cref="PrefabExtensionXmlNodeAttribute"/>).</item>
    /// <item><see cref="IEnumerable{T}"/> of <see cref="XmlNode"/>: Sequential node insertion (with <see cref="PrefabExtensionXmlNodesAttribute"/>).</item>
    /// </list>
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    protected internal abstract class PrefabExtensionContentAttribute : Attribute;

    /// <summary>
    /// Base class for content attributes supplying a single root XML node.
    /// </summary>
    protected internal abstract class PrefabExtensionSingleContentAttribute : PrefabExtensionContentAttribute
    {
        /// <summary>
        /// Gets a value indicating whether to discard the root node and insert only its child nodes as siblings.
        /// </summary>
        public bool RemoveRootNode { get; }

        /// <param name="removeRootNode">See <see cref="RemoveRootNode"/>.</param>
        protected PrefabExtensionSingleContentAttribute(bool removeRootNode) => RemoveRootNode = removeRootNode;
    }

    /// <summary>
    /// Specifies that the decorated member returns a file name located under the module's GUI folder.
    /// </summary>
    /// <param name="removeRootNode">See <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/>.</param>
    protected internal sealed class PrefabExtensionFileNameAttribute(bool removeRootNode = false) : PrefabExtensionSingleContentAttribute(removeRootNode);

    /// <summary>
    /// Specifies that the decorated member returns raw XML markup string.
    /// </summary>
    /// <param name="removeRootNode">See <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/>.</param>
    protected internal sealed class PrefabExtensionTextAttribute(bool removeRootNode = false) : PrefabExtensionSingleContentAttribute(removeRootNode);

    /// <summary>
    /// Specifies that the decorated member returns an <see cref="XmlNode"/> or <see cref="XmlDocument"/>.
    /// </summary>
    /// <param name="removeRootNode">See <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/>.</param>
    protected internal sealed class PrefabExtensionXmlNodeAttribute(bool removeRootNode = false) : PrefabExtensionSingleContentAttribute(removeRootNode);

    /// <summary>
    /// Specifies that the decorated member returns a collection of <see cref="XmlNode"/> instances inserted sequentially.
    /// </summary>
    protected internal sealed class PrefabExtensionXmlNodesAttribute : PrefabExtensionContentAttribute;

    /// <summary>
    /// Obsolete. Use <see cref="PrefabExtensionXmlNodeAttribute"/>, which also accepts an <see cref="XmlDocument"/>.
    /// </summary>
    /// <param name="removeRootNode">See <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/>.</param>
    [Obsolete("Use PrefabExtensionXmlNodeAttribute, which also accepts an XmlDocument.")]
    protected internal sealed class PrefabExtensionXmlDocumentAttribute(bool removeRootNode = false) : PrefabExtensionSingleContentAttribute(removeRootNode);
}