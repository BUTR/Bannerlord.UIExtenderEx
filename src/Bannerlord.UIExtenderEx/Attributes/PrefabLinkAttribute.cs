using System;

namespace Bannerlord.UIExtenderEx.Attributes;

/// <summary>
/// Links a prefab patch or standalone mod prefab to its bound ViewModel type and, optionally, to the specific mixin type whose members it binds.
/// <para>
/// Consumed by <c>Bannerlord.UIExtenderEx.Analyzers</c> at build time to perform compile-time type checking, data-binding validation,
/// and diagnostics across XML prefabs, ViewModels, and mixins. Ignored by UIExtenderEx at runtime.
/// </para>
/// <para>
/// For prefab patches, <see cref="ViewModel"/> corresponds to the active data source scope at the target injection node. For standalone
/// prefabs, it corresponds to the root data source scope. If specified, <see cref="Mixin"/> must target the host ViewModel type or a base
/// type when <c>handleDerived: true</c> is configured.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [assembly: PrefabLink(typeof(OptionsPagePatch), typeof(OptionsVM), typeof(OptionsVMMixin))]
/// [assembly: PrefabLink("ModOptionsView_MCM", typeof(ModOptionsVM))]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PrefabLinkAttribute : Attribute
{
    /// <summary>Gets the prefab patch type marked with <see cref="PrefabExtensionAttribute"/>, or <see langword="null"/> when linking a standalone prefab.</summary>
    public Type? Patch { get; }

    /// <summary>
    /// Gets the standalone prefab name (either registered name or file name without extension), or <see langword="null"/> when linking a patch.
    /// </summary>
    public string? Prefab { get; }

    /// <summary>Gets the target ViewModel type bound by the prefab XML at the injection point or root.</summary>
    public Type ViewModel { get; }

    /// <summary>Gets the optional mixin type whose members are exposed to the prefab XML.</summary>
    public Type? Mixin { get; }

    /// <summary>
    /// Links a prefab patch to the ViewModel resolved at its target injection node.
    /// </summary>
    /// <param name="patch">The prefab patch class.</param>
    /// <param name="viewModel">The ViewModel type bound at the injection node.</param>
    public PrefabLinkAttribute(Type patch, Type viewModel)
    {
        Patch = patch;
        ViewModel = viewModel;
    }

    /// <summary>
    /// Links a prefab patch to the ViewModel and mixin resolved at its target injection node.
    /// </summary>
    /// <param name="patch">The prefab patch class.</param>
    /// <param name="viewModel">The ViewModel type bound at the injection node.</param>
    /// <param name="mixin">The mixin type whose members the patch binds.</param>
    public PrefabLinkAttribute(Type patch, Type viewModel, Type mixin)
    {
        Patch = patch;
        ViewModel = viewModel;
        Mixin = mixin;
    }

    /// <summary>
    /// Links a standalone mod prefab to the ViewModel at its root data source scope.
    /// </summary>
    /// <param name="prefab">The prefab registration name or XML file name without extension.</param>
    /// <param name="viewModel">The ViewModel type bound at the prefab root.</param>
    public PrefabLinkAttribute(string prefab, Type viewModel)
    {
        Prefab = prefab;
        ViewModel = viewModel;
    }

    /// <summary>
    /// Links a standalone mod prefab to the ViewModel and mixin at its root data source scope.
    /// </summary>
    /// <param name="prefab">The prefab registration name or XML file name without extension.</param>
    /// <param name="viewModel">The ViewModel type bound at the prefab root.</param>
    /// <param name="mixin">The mixin type whose members the prefab binds.</param>
    public PrefabLinkAttribute(string prefab, Type viewModel, Type mixin)
    {
        Prefab = prefab;
        ViewModel = viewModel;
        Mixin = mixin;
    }
}