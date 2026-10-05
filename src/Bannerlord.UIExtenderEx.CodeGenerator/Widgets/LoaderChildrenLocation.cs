using System;
using System.Collections.Generic;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

/// <summary>
/// Resolves the logical or default insertion target for child widgets within a prefab template hierarchy,
/// mirroring the behavior of <c>WidgetInstantiationResult.GetLogicalOrDefaultChildrenLocation</c>.
/// <para>
/// When a widget instantiates a nested prefab, child widgets declared in the outer template are placed
/// into the designated <c>&lt;LogicalChildrenLocation /&gt;</c> template within the child prefab.
/// If attribute assignment fails along this resolution path, attribute processing terminates for all child nodes.
/// </para>
/// </summary>
internal static class LoaderChildrenLocation
{
    /// <summary>
    /// Computes the template path from the root of the specified prefab down to its logical children insertion target.
    /// Returns <see langword="null"/> if the widget uses its default instantiation root.
    /// <para>
    /// Executes a breadth-first search prioritizing direct children over nested subtrees at each hierarchy level,
    /// excluding the root template itself from consideration.
    /// </para>
    /// </summary>
    /// <param name="widgetFactory">The widget factory used to resolve custom prefab types.</param>
    /// <param name="instance">The widget template instance representing the used prefab.</param>
    /// <returns>The list of templates forming the path to the logical insertion target, or <see langword="null"/> if not found.</returns>
    public static List<WidgetTemplate>? PathOf(WidgetFactory widgetFactory, WidgetTemplate instance) =>
        PathOf(widgetFactory, instance, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Locates the template under <paramref name="root"/> designated as the logical children insertion location.
    /// Returns <see langword="null"/> if no logical children location is defined.
    /// </summary>
    /// <param name="widgetFactory">The widget factory used to resolve custom prefab types.</param>
    /// <param name="root">The root template of the prefab hierarchy.</param>
    /// <returns>The target logical children template, or <see langword="null"/> if none exists.</returns>
    public static WidgetTemplate? LocationIn(WidgetFactory widgetFactory, WidgetTemplate root)
    {
        var path = new List<WidgetTemplate> { root };
        return Find(widgetFactory, root, path, new HashSet<string>(StringComparer.Ordinal)) ? path[path.Count - 1] : null;
    }

    private static List<WidgetTemplate>? PathOf(WidgetFactory widgetFactory, WidgetTemplate instance, HashSet<string> visiting)
    {
        if (!visiting.Add(instance.Type))
            return null;
        try
        {
            if (!widgetFactory.TryGetCustomTypeIncludingRegistered(instance.Type, out var prefab) || prefab.RootTemplate is not { } root)
                return null;
            var path = new List<WidgetTemplate> { root };
            return Find(widgetFactory, root, path, visiting) ? path : null;
        }
        finally
        {
            visiting.Remove(instance.Type);
        }
    }

    private static bool Find(WidgetFactory widgetFactory, WidgetTemplate template, List<WidgetTemplate> path, HashSet<string> visiting)
    {
        var children = ResultChildren(widgetFactory, template, visiting);
        foreach (var child in children)
        {
            if (child.LogicalChildrenLocation)
            {
                path.Add(child);
                return true;
            }
        }
        foreach (var child in children)
        {
            path.Add(child);
            if (Find(widgetFactory, child, path, visiting))
                return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    /// <summary>
    /// Retrieves the child templates of an instantiation target, omitting children if the template delegates
    /// its insertion location to an internal nested prefab.
    /// </summary>
    private static List<WidgetTemplate> ResultChildren(WidgetFactory widgetFactory, WidgetTemplate template, HashSet<string> visiting)
    {
        var children = new List<WidgetTemplate>();
        if (widgetFactory.IsCustomTypeIncludingRegistered(template.Type) && PathOf(widgetFactory, template, visiting) is not null)
            return children;
        for (var i = 0; i < template.ChildCount; i++)
            children.Add(template.GetChildAt(i));
        return children;
    }

    /// <summary>
    /// Determines whether attribute evaluation aborts along <paramref name="path"/> due to an undefined constant reference.
    /// </summary>
    /// <param name="path">The sequence of widget templates to evaluate.</param>
    /// <returns><see langword="true"/> if an undefined constant causes attribute evaluation to terminate; otherwise, <see langword="false"/>.</returns>
    public static bool AbandonsAttributes(IEnumerable<WidgetTemplate> path)
    {
        foreach (var template in path)
        {
            foreach (var attribute in template.AllAttributes)
            {
                if (attribute.KeyType is WidgetAttributeKeyTypeAttribute && attribute.ValueType is WidgetAttributeValueTypeConstant
                    && template.Prefab?.GetConstantValue(attribute.Value) is null)
                    return true;
            }
        }
        return false;
    }
}