using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using System;
using System.Collections.Generic;

using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

/// <summary>
/// Collects all parameter names referenced via <c>*Name</c> attribute syntax across a prefab's template tree, item templates, and nested prefab dependencies.
/// </summary>
/// <remarks>
/// <para>
/// In Gauntlet UI, databinding propagates enclosing prefab parameters down to nested prefabs (<see cref="PrefabValues.BindingParameters"/>).
/// Filtering propagated parameters to only those actually referenced prevents generating redundant class variants for prefabs reused across different parent scopes.
/// </para>
/// </remarks>
internal static class ReferencedParameters
{
    /// <summary>
    /// Collects the set of parameter names referenced within the specified prefab hierarchy.
    /// </summary>
    /// <param name="widgetFactory">The widget factory resolving prefabs and widgets.</param>
    /// <param name="prefabName">The root prefab name.</param>
    /// <returns>A set of referenced parameter names.</returns>
    public static HashSet<string> Of(WidgetFactory widgetFactory, string prefabName)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        Collect(widgetFactory, prefabName, names, new HashSet<string>(StringComparer.Ordinal));
        return names;
    }

    private static void Collect(WidgetFactory widgetFactory, string prefabName, HashSet<string> names, HashSet<string> visited)
    {
        if (!visited.Add(prefabName) || !widgetFactory.TryGetCustomTypeIncludingRegistered(prefabName, out var prefab) || prefab.RootTemplate is not { } root)
            return;
        CollectTemplate(widgetFactory, root, names, visited);
    }

    private static void CollectTemplate(WidgetFactory widgetFactory, WidgetTemplate template, HashSet<string> names, HashSet<string> visited)
    {
        foreach (var attribute in template.AllAttributes)
        {
            if (attribute.ValueType is WidgetAttributeValueTypeParameter)
                names.Add(attribute.Value);
        }
        if (widgetFactory.IsCustomTypeIncludingRegistered(template.Type))
            Collect(widgetFactory, template.Type, names, visited);
        if (template.GetExtensionData<ItemTemplateUsage>() is { } usage)
        {
            foreach (var itemTemplate in new[] { usage.DefaultItemTemplate, usage.FirstItemTemplate, usage.LastItemTemplate })
            {
                if (itemTemplate is not null)
                    CollectTemplate(widgetFactory, itemTemplate, names, visited);
            }
        }
        for (var i = 0; i < template.ChildCount; i++)
            CollectTemplate(widgetFactory, template.GetChildAt(i), names, visited);
    }
}