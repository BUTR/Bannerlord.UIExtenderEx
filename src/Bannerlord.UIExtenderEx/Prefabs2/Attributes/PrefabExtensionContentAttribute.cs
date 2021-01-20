using System;
using System.Collections.Generic;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    public abstract partial class PrefabExtensionInsertPatch
    {
        /// <summary>
        /// Used on a single Property or Method in <see cref="PrefabExtensionInsertPatch"/> to flag it is containing the patch information.<br/>
        /// For brevity's sake, the value of the target Property or the return type of the target Method will be hereinafter referred to as the "Content".<br/><br/>
        /// <b>Supported Types:</b>
        /// <list type="bullet">
        /// <item>
        ///		<term><see cref="string"/></term>
        ///		<description>Represents either the name of a file, or xml. Can be used with <see cref="RemoveRootNode"/>. See <see cref="IsFileName"/> for more info.</description>
        /// </item>
        /// <item>
        ///		<term><see cref="XmlDocument"/></term>
        ///		<description>When Content is of this type, the root node of the document, as well as all of its children will be inserted.
        ///         Can be used with <see cref="RemoveRootNode"/>.</description>
        /// </item>
        /// <item>
        ///		<term><see cref="XmlNode"/></term>
        ///		<description>When Content is of this type, the node and all of its children will be inserted.
        ///         Can be used with <see cref="RemoveRootNode"/>.</description>
        /// </item>
        /// <item>
        ///		<term><see cref="IEnumerable{T}"/> of type <see cref="XmlNode"/></term>
        ///		<description>When content is of this type, nodes will be inserted in the same order that they appear in this list.
        ///         <see cref="RemoveRootNode"/> will be ignored if Content is of this type.</description>
        /// </item>
        /// </list>
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
        protected class PrefabExtensionContentAttribute : Attribute
        {
            /// <summary>
            /// <inheritdoc cref="PrefabExtensionContentAttribute"/>
            /// </summary>
            /// <param name="isFileName">
            /// Used when Content is of type string.<br/>
            /// If set to true, Content should be the name of a file contained in the GUI folder of your module.<br/>
            /// If set to false, Content should be the markup representation (xml) of your patch.
            /// </param>
            /// <param name="removeRootNode">
            /// Used when Content is of any type other than <see cref="IEnumerable{T}"/>
            /// If set to true, the root node of Content will be removed.<br/>
            /// This is useful when you wish to insert multiple nodes at the same level (as siblings) from a single patch.
            /// </param>
            public PrefabExtensionContentAttribute(bool isFileName = false, bool removeRootNode = false)
            {
                IsFileName = isFileName;
                RemoveRootNode = removeRootNode;
            }

            /// <summary>
            /// Used when Content is of type string.<br/>
            /// If set to true, Content should be the name of a file contained in the GUI folder of your module.<br/>
            /// If set to false, Content should be the markup representation (xml) of your patch.
            /// </summary>
            public bool IsFileName { get; }

            /// <summary>
            /// Used when Content is of any type other than <see cref="IEnumerable{T}"/><br/>
            /// If set to true, the root node of Content will be removed.<br/>
            /// This is useful when you wish to insert multiple nodes at the same level (as siblings) from a single patch.
            /// </summary>
            public bool RemoveRootNode { get; }
        }
    }
}