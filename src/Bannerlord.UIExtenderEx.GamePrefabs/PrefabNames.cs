namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Provides utilities for normalizing Gauntlet prefab identifiers.
/// </summary>
public static class PrefabNames
{
    /// <summary>Normalizes a prefab identifier by replacing periods with underscores to match generated class names.</summary>
    public static string Normalize(string prefabName) => prefabName.Replace('.', '_');
}