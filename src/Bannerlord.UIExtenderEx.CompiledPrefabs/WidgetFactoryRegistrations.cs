using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.Runtimes;

using System;
using System.Diagnostics.CodeAnalysis;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.ResourceManager;

/// <summary>
/// Exposes dynamically registered widget types and prefabs (<see cref="PrefabSource"/>) to the Gauntlet code generator.
/// <para>
/// Intercepts type queries during code generation to resolve dynamically registered built-in widget types and custom prefabs
/// before falling back to the engine's <see cref="WidgetFactory"/>, matching the runtime resolution behavior of XML patches.
/// </para>
/// </summary>
public sealed class WidgetFactoryRegistrations : IWidgetRegistrations
{
    /// <summary>Attempts to resolve a dynamically registered built-in widget <see cref="Type"/> by name.</summary>
    public bool TryGetRegisteredBuiltinType(string name, [MaybeNullWhen(false)] out Type type) =>
        PrefabSource.TryGetRegisteredBuiltinType(name, out type);

    /// <summary>Determines whether a custom prefab type with the specified name has been registered dynamically.</summary>
    public bool IsRegisteredCustomType(string name) =>
        PrefabSource.IsRegisteredCustomType(name);

    /// <summary>Attempts to resolve a dynamically registered <see cref="WidgetPrefab"/> by name.</summary>
    public bool TryGetRegisteredCustomType(WidgetFactory widgetFactory, string name, [MaybeNullWhen(false)] out WidgetPrefab prefab) =>
        PrefabSource.TryGetRegisteredCustomType(widgetFactory, name, out prefab);
}