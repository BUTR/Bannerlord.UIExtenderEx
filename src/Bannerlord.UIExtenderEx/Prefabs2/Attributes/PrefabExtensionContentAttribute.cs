using System;
using System.Collections.Generic;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    public abstract partial class PrefabExtensionInsertPatch
    {
        /// <summary>
        /// Used on a single Property or Method in <see cref="PrefabExtensionInsertPatch"/> to flag it as containing the patch information.<br/>
        /// <b>Supported Types:</b>
        /// <list type="bullet">
        /// <item>
        ///	    <term><see cref="string"/></term>
        ///	    <description>Represents either the name of a file (use <see cref="PrefabExtensionFileNameAttribute"/>),
        ///     or xml (use <see cref="PrefabExtensionTextAttribute"/>).</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="XmlDocument"/></term>
        ///	    <description>Use <see cref="PrefabExtensionXmlDocumentAttribute"/>.
        ///     The root node of the document and all of its children will be inserted at the target location.</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="XmlNode"/></term>
        ///	    <description>Use <see cref="PrefabExtensionXmlNodeAttribute"/>.
        ///     The node and all of its children will be inserted at the target location.</description>
        /// </item>
        /// <item>
        ///     <term><see cref="IEnumerable{T}"/> of type <see cref="XmlNode"/></term>
        ///	    <description>Use <see cref="PrefabExtensionXmlNodesAttribute"/>.
        ///     Nodes will be inserted in the same order that they appear in this list at the target location.</description>
        /// </item>
        /// </list>
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
        protected internal abstract class PrefabExtensionContentAttribute : Attribute
        {
        }

        protected internal abstract class PrefabExtensionSingleContentAttribute : PrefabExtensionContentAttribute
        {
            /// <summary>
            /// If set to true, the root node of Content will be removed.<br/>
            /// This is useful when you wish to insert multiple nodes at the same level (as siblings) from a single patch.
            /// </summary>
            public bool RemoveRootNode { get; }

            /// <summary>
            /// <inheritdoc cref="PrefabExtensionContentAttribute"/>
            /// </summary>
            /// <param name="removeRootNode">
            /// If set to true, the root node of will be removed.<br/>
            /// This is useful when you wish to insert multiple nodes at the same level (as siblings) from a single patch.
            /// </param>
			protected PrefabExtensionSingleContentAttribute(bool removeRootNode)
            {
                RemoveRootNode = removeRootNode;
            }
        }

        /// <summary>
        /// Used when Content is of type string and refers to a file name.<br/>
        /// The file should have an extension of type .xml, and be located inside of the GUI folder of your module.<br/>
        /// You can include or omit the extension type. I.e. both of the following will work:<br/>
        /// <list type="bullet">
        /// <item>
        ///	<description>YourPatchFileName</description>
        /// </item>
        /// <item>
        ///	<description>YourPatchFileName.xml</description>
        /// </item>
        /// </list>
        /// See <see cref="PrefabExtensionContentAttribute"/> for info on other attribute types.
        /// </summary>
        protected internal class PrefabExtensionFileNameAttribute : PrefabExtensionSingleContentAttribute
        {
            /// <summary>
            /// <inheritdoc cref="PrefabExtensionFileNameAttribute"/><br/>
            /// </summary>
            /// <param name="removeRootNode"><inheritdoc/></param>
            public PrefabExtensionFileNameAttribute(bool removeRootNode = false) : base(removeRootNode)
            {
            }
        }

        /// <summary>
        /// Use when the property or return of your method is of type <see cref="string"/> and is xml.<br/>
        /// The xml must be properly formatted with a single root node.<br/>
        /// If <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/> is set to false, the root node of the document,
        /// as well as all of its children, will be inserted at the target location.<br/>
        /// If <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/> is set to true, the root node of the document
        /// will be ignored, and all of the root node's children will be placed at the target location instead.<br/>
        /// See <seealso cref="PrefabExtensionContentAttribute"/> for more info.
        /// </summary>
        protected internal class PrefabExtensionTextAttribute : PrefabExtensionSingleContentAttribute
        {
            /// <summary>
            /// <inheritdoc cref="PrefabExtensionTextAttribute"/><br/>
            /// </summary>
            /// <param name="removeRootNode"><inheritdoc/></param>
            public PrefabExtensionTextAttribute(bool removeRootNode = false) : base(removeRootNode)
            {
            }
        }

        /// <summary>
        /// Use when the property or return of your method is of type <see cref="XmlNode"/>.<br/>
        /// The node and all of its children will be inserted at the target location.<br/>
        /// If <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/> is set to false, the root node of the document,
        /// as well as all of its children, will be inserted at the target location.<br/>
        /// If <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/> is set to true, the root node of the document
        /// will be ignored, and all of the root node's children will be placed at the target location instead.<br/>
        /// See <seealso cref="PrefabExtensionContentAttribute"/> for more info.
        /// </summary>
        protected internal class PrefabExtensionXmlNodeAttribute : PrefabExtensionSingleContentAttribute
        {
            /// <summary>
            /// <inheritdoc cref="PrefabExtensionXmlNodeAttribute"/><br/>
            /// </summary>
            /// <param name="removeRootNode"><inheritdoc/></param>
            public PrefabExtensionXmlNodeAttribute(bool removeRootNode = false) : base(removeRootNode)
            {
            }
        }

        /// <summary>
        /// Use when the property or return of your method is of type IEnumerable{<see cref="XmlNode"/>}.<br/>
        /// Nodes will be inserted in the same order that they appear in this list at the target location.<br/>
        /// See <seealso cref="PrefabExtensionContentAttribute"/> for more info.
        /// </summary>
        protected internal class PrefabExtensionXmlNodesAttribute : PrefabExtensionContentAttribute
        {
        }

        /// <summary>
        /// Use when the property or return of your method is of type <see cref="XmlDocument"/>.<br/>
        /// If <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/> is set to false, the root node of the document,
        /// as well as all of its children, will be inserted at the target location.<br/>
        /// If <see cref="PrefabExtensionSingleContentAttribute.RemoveRootNode"/> is set to true, the root node of the document
        /// will be ignored, and all of the root node's children will be placed at the target location instead.<br/>
        /// See <seealso cref="PrefabExtensionContentAttribute"/> for more info.
        /// </summary>
        protected internal class PrefabExtensionXmlDocumentAttribute : PrefabExtensionSingleContentAttribute
        {
            /// <summary>
            /// <inheritdoc cref="PrefabExtensionXmlDocumentAttribute"/><br/>
            /// </summary>
            /// <param name="removeRootNode"><inheritdoc/></param>
            public PrefabExtensionXmlDocumentAttribute(bool removeRootNode = false) : base(removeRootNode)
            {
            }
        }
    }
}