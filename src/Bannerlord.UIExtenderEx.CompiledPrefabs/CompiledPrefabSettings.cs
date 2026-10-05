using Bannerlord.UIExtenderEx.Runtimes;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>Provides runtime configuration properties declared within <c>SubModule.xml</c> for compiled prefab execution.</summary>
public static class CompiledPrefabSettings
{
    /// <summary>Configuration key that toggles compiled prefab execution.</summary>
    public const string CompiledPrefabsKey = "CompiledPrefabs";

    /// <summary>Configuration key that toggles emitting generated C# source code files to disk.</summary>
    public const string DumpGeneratedCodeKey = "DumpGeneratedCode";

    /// <summary>Configuration key that toggles recording performance metrics to disk.</summary>
    public const string RecordTimingsKey = "RecordTimings";

    /// <summary>Gets or sets a value indicating whether patched UI movies are compiled to C# and executed as compiled prefabs instead of interpreted from XML.</summary>
    public static bool CompiledPrefabs
    {
        get => RuntimeSettings.Get(CompiledPrefabsKey, true);
        set => RuntimeSettings.Set(CompiledPrefabsKey, value);
    }

    /// <summary>Gets or sets a value indicating whether generated C# source code is written to disk alongside cached assemblies for debugging.</summary>
    public static bool DumpGeneratedCode
    {
        get => RuntimeSettings.Get(DumpGeneratedCodeKey, false);
        set => RuntimeSettings.Set(DumpGeneratedCodeKey, value);
    }

    /// <summary>Gets or sets a value indicating whether load, compilation, and warm-up timings are recorded to disk across sessions (<see cref="PrefabTimings"/>).</summary>
    public static bool RecordTimings
    {
        get => RuntimeSettings.Get(RecordTimingsKey, false);
        set => RuntimeSettings.Set(RecordTimingsKey, value);
    }
}