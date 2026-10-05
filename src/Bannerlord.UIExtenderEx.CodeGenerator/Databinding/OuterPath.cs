using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Encapsulates operations and path transformations for outer databinding paths climbing above a class root (e.g. <c>^2\Hint</c>).
/// <para>
/// When a nested prefab or item template binds to a data source outside its own scope, outer paths identify the number
/// of hierarchy levels to ascend and the remaining property path to traverse.
/// </para>
/// </summary>
internal static class OuterPath
{
    private const char Marker = '^';

    /// <summary>
    /// Determines whether the specified path represents an outer path ascending above the class root.
    /// </summary>
    public static bool IsOuter(BindingPath path) => path.Nodes.Length > 0 && path.Nodes[0].Length > 0 && path.Nodes[0][0] == Marker;

    /// <summary>Creates an outer binding path ascending the specified number of levels above the root.</summary>
    public static BindingPath Create(int levels, IEnumerable<string> rest) =>
        new(new[] { Marker + levels.ToString(CultureInfo.InvariantCulture) }.Concat(rest));

    /// <summary>Deconstructs an outer binding path into its ascended level count and remaining path nodes.</summary>
    public static (int Levels, string[] Nodes) Parse(BindingPath path)
    {
        if (!IsOuter(path))
            throw new ArgumentException($"'{path.Path}' is not a path above the root.", nameof(path));
        return (int.Parse(path.Nodes[0].Substring(1), CultureInfo.InvariantCulture), path.Nodes.Skip(1).ToArray());
    }

    /// <summary>
    /// Converts an internal outer path representation back into relative Gauntlet loader syntax using <c>".."</c> segments.
    /// </summary>
    public static BindingPath ToRaw(BindingPath path)
    {
        if (!IsOuter(path))
            return path;
        var (levels, rest) = Parse(path);
        return new(Enumerable.Repeat("..", levels - 1).Concat(rest));
    }

    /// <summary>
    /// Appends an outer path relative to a base host path.
    /// </summary>
    public static BindingPath Below(BindingPath rootRaw, BindingPath path)
    {
        var (levels, rest) = Parse(path);
        return rootRaw.Append(new BindingPath(Enumerable.Repeat("..", levels).Concat(rest)));
    }

    /// <summary>
    /// Computes the hierarchy depth offset represented by a relative path with <c>".."</c> ascending segments.
    /// </summary>
    public static int Depth(BindingPath raw)
    {
        var climbs = raw.Nodes.Count(x => x == "..");
        // Less one for Root, written or cancelled
        return raw.Nodes.Length - 2 * climbs - 1;
    }

    /// <summary>
    /// Computes the minimum ascending level count at which Gauntlet XML resolution yields null.
    /// </summary>
    public static int NullFrom(int rootDepth) => rootDepth + 3;
}