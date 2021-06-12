using Bannerlord.UIExtenderEx.Attributes;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Patch that inserts Content relative to the target node specified in the <see cref="PrefabExtensionAttribute.XPath"/> property.<br/>
    /// A single Method or Property should be flagged with the <see cref="PrefabExtensionContentAttribute"/>.<br/>
    /// <b>Content Attribute Types:</b>
    /// <list type="bullet">
    /// <item><see cref="PrefabExtensionFileNameAttribute"/></item>
    /// <item><see cref="PrefabExtensionTextAttribute"/></item>
    /// <item><see cref="PrefabExtensionXmlNodeAttribute"/></item>
    /// <item><see cref="PrefabExtensionXmlNodesAttribute"/></item>
    /// <item><see cref="PrefabExtensionXmlDocumentAttribute"/></item>
    /// </list>
    /// </summary>
    public abstract partial class PrefabExtensionInsertPatch
    {
        /// <summary>
        /// InsertType specifies the placement of the content flagged by your <see cref="PrefabExtensionContentAttribute"/>
        /// relative to the target node specified in the <see cref="PrefabExtensionAttribute.XPath"/> property.<br/><br/>
        ///	    <b>Insertion Methods</b>
        /// <list type="bullet">
        /// <item>
        ///	    <term><see cref="InsertType.Prepend"/></term>
        ///	    <description>Content is placed before the target node at the same height (siblings).</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.ReplaceKeepChildren"/></term>
        ///	    <description>Target node is replaced with the new Content. The children of the original node are added as children to the newly inserted Content.
        ///	        If Content represents more than one root node, <see cref="Index"/> will be used to specify which new node should inherit the children.</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.Replace"/></term>
        ///	    <description>Target node and all of its children are replaced with the new Content.</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.Child"/></term>
        ///	    <description>Content is inserted as a child (children) of the target node.
        ///	        If the target node has children, <see cref="Index"/> will be used to place the new nodes relative to the pre-existing children.</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.Append"/></term>
        ///	    <description>Content is placed after the target node at the same height (siblings).</description>
        /// </item>
        /// <item>
        ///	    <term><see cref="InsertType.Remove"/></term>
        ///	    <description>Removes the node.</description>
        /// </item>
        /// </list>
        /// </summary>
        public abstract InsertType Type { get; }

        /// <summary>
        /// Only used when <see cref="Type"/> is set to <see cref="InsertType.Child"/> or <see cref="InsertType.ReplaceKeepChildren"/>.<br/>
        /// See <seealso cref="Type"/> for more details.
        /// </summary>
        public virtual int Index { get; } = 0;
    }
}