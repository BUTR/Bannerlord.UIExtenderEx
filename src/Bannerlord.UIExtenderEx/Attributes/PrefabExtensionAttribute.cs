using System;

namespace Bannerlord.UIExtenderEx.Attributes;

/// <summary>
/// Marks a class as a prefab XML extension patch targeting a specific Gauntlet movie.
/// <para>
/// Patch classes must inherit from one of the prefab patch base types (such as <see cref="Prefabs2.PrefabExtensionInsertPatch"/>
/// or <see cref="Prefabs2.PrefabExtensionSetAttributePatch"/>).
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PrefabExtensionAttribute : BaseUIExtenderAttribute
{
    /// <summary>Gets the name of the target Gauntlet movie or prefab to patch.</summary>
    public string Movie { get; }

    /// <summary>Gets the optional XPath expression selecting the target XML node within the movie.</summary>
    public string? XPath { get; }

    /// <summary>Legacy property preserved for binary backwards compatibility. Has no runtime effect.</summary>
    [Obsolete("Legacy property, not used anymore.")]
    public string? AutoGenWidgetName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PrefabExtensionAttribute"/> for a movie and optional target XPath.
    /// </summary>
    /// <param name="movie">The target Gauntlet movie name to extend.</param>
    /// <param name="xpath">The optional XPath expression selecting the target node.</param>
    public PrefabExtensionAttribute(string movie, string? xpath = null)
    {
        Movie = movie;
        XPath = xpath;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PrefabExtensionAttribute"/> with legacy widget name parameter.
    /// </summary>
    /// <param name="movie">The target Gauntlet movie name to extend.</param>
    /// <param name="xpath">The optional XPath expression selecting the target node.</param>
    /// <param name="autoGenWidgetName">Legacy widget name parameter.</param>
    [Obsolete("Legacy constructor, not used anymore.")]
    public PrefabExtensionAttribute(string movie, string? xpath = null, string? autoGenWidgetName = null)
    {
        Movie = movie;
        XPath = xpath;
        AutoGenWidgetName = autoGenWidgetName;
    }
}