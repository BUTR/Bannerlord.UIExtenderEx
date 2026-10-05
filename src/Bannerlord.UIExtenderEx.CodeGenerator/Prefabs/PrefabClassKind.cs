namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

/// <summary>Defines the emission role of a generated prefab class, determining its naming scheme, inheritance hierarchy, and databinding scope.</summary>
internal enum PrefabClassKind
{
    /// <summary>The root movie class registered with <see cref="TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext"/>.</summary>
    Movie,

    /// <summary>A dependent prefab instantiated as a child component by a parent widget.</summary>
    UsedPrefab,

    /// <summary>A base prefab that an inheriting prefab class extends.</summary>
    BasePrefab,

    /// <summary>An item template for collection-bound widgets, instantiated dynamically per collection element.</summary>
    ItemTemplate,
}