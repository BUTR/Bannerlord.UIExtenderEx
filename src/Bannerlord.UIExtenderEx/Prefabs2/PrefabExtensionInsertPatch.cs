using System.Collections.Generic;
using System.Xml;

using Bannerlord.UIExtenderEx.Attributes;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Patch that inserts node(s) relative to the target node specified in the <see cref="PrefabExtensionAttribute.XPath"/> property.
    /// </summary>
    public abstract class PrefabExtensionInsertPatch
    {
        /// <summary>
        /// InsertType specifies the placement of the nodes fetched in <see cref="GetPrefabNodes"/> relative to the target node
        /// specified in the <see cref="PrefabExtensionAttribute.XPath"/> property.<br/><br/>
        /// <list type="table">
        /// <listheader>
        ///	    <b>Insertion Methods</b>
        /// </listheader>
        /// <item>
        ///	    <term><see cref="InsertType.Prepend"/></term>
        ///	    <description><see cref="GetPrefabNodes"/> are placed before the target node at the same height (siblings).</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.Replace"/></term>
        ///	    <description>Target node is replaced with <see cref="GetPrefabNodes"/>. The children of the removed node are added as children to the new node.
        ///	    If <see cref="GetPrefabNodes"/> contains more than one child, <see cref="Index"/> will be used to specify which new node should inherit the children.</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.ReplaceAll"/></term>
        ///	    <description>Target node and all of its children are replaced with <see cref="GetPrefabNodes"/>.</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.Child"/></term>
        ///	    <description><see cref="GetPrefabNodes"/> are inserted as children of the target node.
        ///	    If the target node has children, <see cref="Index"/> will be used to place the new nodes relative to the other children.</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.Append"/></term>
        ///	    <description><see cref="GetPrefabNodes"/> are placed after the target node at the same height (siblings).</description>
        /// </item>
        /// </list>
        /// </summary>
        public abstract InsertType Type { get; }

        /// <summary>
        /// Only used when <see cref="Type"/> is set to <see cref="InsertType.Child"/> or <see cref="InsertType.Replace"/>.<br/>
        /// See <seealso cref="Type"/> for more details.
        /// </summary>
        public virtual int Index { get; } = 0;

        /// <summary>
        /// Nodes will be inserted in the same order that they appear in this list.
        /// </summary>
        public abstract IEnumerable<XmlNode> GetPrefabNodes();
    }
}