using System;
using System.Collections.Generic;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

/// <summary>
/// Identifies prefab constants that depend dynamically on runtime sprite or brush layer dimensions and cannot be baked as compile-time constants.
/// </summary>
/// <remarks>
/// <para>
/// Evaluates constant definition dependencies across XML prefab trees. Most constant definitions evaluate arithmetic over literal XML values.
/// However, constants of type <see cref="ConstantDefinitionType.SpriteWidth"/>, <see cref="ConstantDefinitionType.SpriteHeight"/>,
/// <see cref="ConstantDefinitionType.BrushLayerWidth"/>, and <see cref="ConstantDefinitionType.BrushLayerHeight"/> query runtime texture sheets and brush configurations.
/// Baking these dimensions into compiled code would prevent visual updates when texture assets are replaced by mods or resolution packs.
/// </para>
/// <para>
/// Resource-dependent constants are resolved dynamically at widget instantiation time via the widget's active <c>UIContext</c>,
/// while independent constants are emitted as inline literals.
/// </para>
/// </remarks>
public static class ConstantResourceAnalysis
{
    /// <summary>
    /// Determines whether the specified XML attribute value references a resource-dependent constant.
    /// </summary>
    /// <remarks>
    /// Follows <c>!</c> constant references matching <see cref="ConstantDefinition.GetActualValueOf"/> behavior.
    /// Parameter references (<c>*</c>) are treated as literal text values.
    /// </remarks>
    public static bool ValueDependsOnResources(string? value, IReadOnlyDictionary<string, ConstantDefinition> constants) =>
        ValueDependsOnResources(value, constants, new(StringComparer.Ordinal));

    /// <summary>
    /// Determines whether the specified constant definition transitively depends on runtime sprite or brush dimensions.
    /// </summary>
    public static bool DependsOnResources(ConstantDefinition definition, IReadOnlyDictionary<string, ConstantDefinition> constants) =>
        DependsOnResources(definition, constants, new(StringComparer.Ordinal));

    /// <summary>
    /// Detects circular references within constant definitions that would cause infinite recursion during evaluation.
    /// </summary>
    /// <param name="definition">The constant definition to analyze.</param>
    /// <param name="constants">The dictionary of available constant definitions in scope.</param>
    /// <returns><see langword="true"/> if a cyclic reference is detected; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Traverses all dependency branches to guard against StackOverflowException crashes during code emission.
    /// </remarks>
    public static bool ReferencesItself(ConstantDefinition definition, IReadOnlyDictionary<string, ConstantDefinition> constants) =>
        ReferencesItself(definition, constants, new(StringComparer.Ordinal));

    private static bool ReferencesItself(ConstantDefinition definition, IReadOnlyDictionary<string, ConstantDefinition> constants, HashSet<string> visiting)
    {
        if (!visiting.Add(definition.Name))
            return true;
        try
        {
            foreach (var value in new[] { definition.Value, definition.Additive, definition.OnTrueValue, definition.OnFalseValue })
            {
                if (value is { Length: > 1 } && value[0] == '!' && constants.TryGetValue(value.Substring(1), out var referenced) && ReferencesItself(referenced, constants, visiting))
                    return true;
            }
            return false;
        }
        finally
        {
            visiting.Remove(definition.Name);
        }
    }

    private static bool ValueDependsOnResources(string? value, IReadOnlyDictionary<string, ConstantDefinition> constants, HashSet<string> visiting)
    {
        if (value is null || value.Length == 0 || value[0] != '!')
            return false;

        var name = value.Substring(1);
        return constants.TryGetValue(name, out var referenced) && DependsOnResources(referenced, constants, visiting);
    }

    private static bool DependsOnResources(ConstantDefinition definition, IReadOnlyDictionary<string, ConstantDefinition> constants, HashSet<string> visiting)
    {
        switch (definition.Type)
        {
            case ConstantDefinitionType.SpriteWidth:
            case ConstantDefinitionType.SpriteHeight:
            case ConstantDefinitionType.BrushLayerWidth:
            case ConstantDefinitionType.BrushLayerHeight:
                return true;
        }

        // Cycle check prevents infinite recursion across circular constant references.
        if (!visiting.Add(definition.Name))
            return false;

        try
        {
            // Evaluates arithmetic parameters (Value, Additive) and conditional branches (OnTrueValue, OnFalseValue).
            return ValueDependsOnResources(definition.Value, constants, visiting)
                   || ValueDependsOnResources(definition.Additive, constants, visiting)
                   || (definition.Type == ConstantDefinitionType.BooleanCheck
                       && (ValueDependsOnResources(definition.OnTrueValue, constants, visiting)
                           || ValueDependsOnResources(definition.OnFalseValue, constants, visiting)));
        }
        finally
        {
            visiting.Remove(definition.Name);
        }
    }
}