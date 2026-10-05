using System;
using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

/// <summary>
/// Resolves dotted property navigation paths on widget types, matching the navigation semantics of the TaleWorlds Gauntlet XML loader.
/// </summary>
public static class WidgetPropertyPath
{
    /// <summary>
    /// Resolves a dotted property path starting from the specified CLR type.
    /// </summary>
    /// <param name="type">The initial widget type to traverse from.</param>
    /// <param name="path">The dot-delimited property path.</param>
    /// <returns>The resolved <see cref="PropertyInfo"/>, or <see langword="null"/> if any path segment fails to resolve.</returns>
    public static PropertyInfo? Resolve(Type? type, string path) => type is null ? null : Resolve(type, path, 0);

    /// <summary>
    /// Resolves a dotted property path on the underlying widget type represented by <paramref name="template"/>,
    /// traversing nested prefab hierarchies via <see cref="WidgetFactoryLookup.TryGetWidgetTypeWithinPrefabRoots"/>.
    /// </summary>
    /// <param name="widgetFactory">The widget factory used to resolve custom prefab types.</param>
    /// <param name="template">The widget template defining the starting type.</param>
    /// <param name="path">The dot-delimited property path.</param>
    /// <returns>The resolved <see cref="PropertyInfo"/>, or <see langword="null"/> if the type or path fails to resolve.</returns>
    internal static PropertyInfo? Resolve(WidgetFactory widgetFactory, WidgetTemplate template, string path) =>
        widgetFactory.TryGetWidgetTypeWithinPrefabRoots(template.Type, out var type) ? Resolve(type, path, 0) : null;

    private static PropertyInfo? Resolve(Type type, string path, int segmentStartIndex)
    {
        var separatorIndex = path.IndexOf('.', segmentStartIndex);
        // The length, not the separator's absolute index, which is what the game passes here and in the loader's own copy of
        // this walk. UIExtenderEx's WidgetExtensionsPatch corrects the loader; this follows it rather than leading, because an
        // attribute that resolves compiled and quietly does nothing as XML is worse than one that never resolves. A prefab falls
        // back to XML whenever generation or compilation fails, so the two have to agree even when the patch does not apply,
        // after a game update that changes that method, say.
        var segmentLength = CodeGeneratorEnvironment.DottedPathsResolveCorrectly
            ? separatorIndex - segmentStartIndex
            : separatorIndex;
        var segment = separatorIndex >= 0
            ? path.Substring(segmentStartIndex, segmentLength)
            : path.Substring(segmentStartIndex);
        TypeDependencies.Inspect(type);
        var property = type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
        TypeDependencies.Inspect(property);
        if (property == null || separatorIndex < 0)
        {
            return property;
        }
        return Resolve(property.PropertyType, path, separatorIndex + 1);
    }
}