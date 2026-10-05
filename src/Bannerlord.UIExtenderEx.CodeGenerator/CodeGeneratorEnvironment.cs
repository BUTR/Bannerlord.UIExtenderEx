using System;
using System.Diagnostics.CodeAnalysis;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;

/// <summary>
/// Provides access to custom widget classes and prefab templates registered with UIExtenderEx at runtime outside standard <see cref="WidgetFactory"/> discovery.
/// </summary>
/// <remarks>
/// Decouples code generation logic from core module runtime dependencies by querying registrations through an abstract interface.
/// </remarks>
public interface IWidgetRegistrations
{
    bool TryGetRegisteredBuiltinType(string name, [MaybeNullWhen(false)] out Type type);

    bool IsRegisteredCustomType(string name);

    /// <summary>
    /// Attempts to retrieve an instantiated <see cref="WidgetPrefab"/> from runtime registrations for the specified factory.
    /// </summary>
    bool TryGetRegisteredCustomType(WidgetFactory widgetFactory, string name, [MaybeNullWhen(false)] out WidgetPrefab prefab);
}

/// <summary>
/// Configures environmental hooks and runtime capabilities required by the prefab code generator.
/// </summary>
/// <remarks>
/// Bridges the forked Gauntlet code generator with the surrounding UIExtenderEx runtime environment.
/// Ensures generated code matches the exact runtime behavior of the TaleWorlds XML loader, including active Harmony patches and runtime widget registrations.
/// Defaults represent a standalone, unpatched environment for testing and tooling.
/// </remarks>
public static class CodeGeneratorEnvironment
{
    /// <summary>
    /// Gets or sets the runtime widget registration provider. Initialized during compiled runtime startup.
    /// </summary>
    public static IWidgetRegistrations Registrations { get; set; } = NoRegistrations.Instance;

    /// <summary>
    /// Gets or sets a value indicating whether dotted attribute paths with more than two segments resolve successfully.
    /// </summary>
    /// <remarks>
    /// Reflects whether UIExtenderEx's IL transpiler on <c>WidgetExtensions.GetObjectAndProperty</c> is currently active,
    /// ensuring generated property access code mirrors XML loader semantics.
    /// </remarks>
    public static bool DottedPathsResolveCorrectly { get; set; }

    /// <summary>
    /// Gets or sets a predicate determining whether a target method has active runtime patches (such as Harmony patches).
    /// </summary>
    /// <remarks>
    /// Used by <see cref="Widgets.WidgetRaises"/> when analyzing IL bytecode for property change notifications.
    /// Patched methods force conservative notification retention across the widget hierarchy.
    /// </remarks>
    public static Func<System.Reflection.MethodBase, bool> IsPatched { get; set; } = static _ => false;

    private sealed class NoRegistrations : IWidgetRegistrations
    {
        public static readonly NoRegistrations Instance = new();

        public bool TryGetRegisteredBuiltinType(string name, [MaybeNullWhen(false)] out Type type)
        {
            type = null;
            return false;
        }

        public bool IsRegisteredCustomType(string name) => false;

        public bool TryGetRegisteredCustomType(WidgetFactory widgetFactory, string name, [MaybeNullWhen(false)] out WidgetPrefab prefab)
        {
            prefab = null;
            return false;
        }
    }
}