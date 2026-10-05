namespace Bannerlord.UIExtenderEx.Prefabs2;

/// <summary>
/// Specifies the insertion placement of prefab content relative to the target XML node.
/// </summary>
public enum InsertType
{
    /// <summary>Inserts content before the target node as a sibling.</summary>
    Prepend,
    /// <summary>Replaces the target node while preserving and reattaching its child nodes.</summary>
    ReplaceKeepChildren,
    /// <summary>Replaces the target node and all its child nodes.</summary>
    Replace,
    /// <summary>Inserts content as a child of the target node.</summary>
    Child,
    /// <summary>Inserts content after the target node as a sibling.</summary>
    Append,
    /// <summary>Removes the target node and all its child nodes.</summary>
    Remove,
}