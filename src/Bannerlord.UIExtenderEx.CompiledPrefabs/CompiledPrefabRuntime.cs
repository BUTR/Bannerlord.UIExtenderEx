using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;
using Bannerlord.UIExtenderEx.Runtimes;

using HarmonyLib;

using System.Reflection;

using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Provides the compiled prefab runtime, executing movies dynamically compiled to C# from patched XML trees via <see cref="CompiledPrefabManager"/>.
/// </summary>
public sealed class CompiledPrefabRuntime : IPrefabRuntime
{
    private static readonly CompiledPrefabRuntime Instance = new();
    private static readonly Harmony Harmony = new("bannerlord.uiextender.ex.compiledprefabs");
    private static readonly object InstallLock = new();
    private static bool _installed;

    /// <summary>
    /// Gets the singleton <see cref="CompiledPrefabManager"/> instance for the process.
    /// </summary>
    /// <remarks>
    /// Lazily initialized on first access rather than during runtime installation, deferring compiler probe and initialization until required.
    /// </remarks>
    public static CompiledPrefabManager Manager => ManagerHolder.Manager;

    /// <summary>
    /// Deferral wrapper ensuring <see cref="CompiledPrefabManager"/> is initialized only upon explicit access rather than during static type initialization of <see cref="CompiledPrefabRuntime"/>.
    /// </summary>
    private static class ManagerHolder
    {
        public static readonly CompiledPrefabManager Manager = CompiledPrefabManager.Create(static () => new GameCompiledPrefabEnvironment());
    }

    /// <summary>
    /// Configures code generator environment hosts, hooks <see cref="PrefabSource"/> lifecycle events, applies Harmony patches, and registers the runtime with <see cref="PrefabRuntimes"/>.
    /// </summary>
    /// <remarks>
    /// Subscribing to <see cref="PrefabSource.Parsed"/> prior to initial prefab parsing ensures all XML documents have recorded hashes for deterministic fingerprint generation.
    /// Idempotent.
    /// </remarks>
    public static void Install()
    {
        lock (InstallLock)
        {
            if (_installed)
                return;
            _installed = true;
        }

        CodeGeneratorEnvironment.Registrations = new WidgetFactoryRegistrations();
        ViewModelMemberResolution.Resolver = new MixinMemberResolver();
        DynamicMember.Host = new DynamicMemberHost();
        // Ensures the code generator emits property path traversal logic conforming to the XML loader's resolution behavior.
        CodeGeneratorEnvironment.DottedPathsResolveCorrectly = PrefabRuntimes.DottedAttributePathsResolve;
        // Determines whether widget methods contain active Harmony patches when inspecting IL property change notifications.
        CodeGeneratorEnvironment.IsPatched = static method => Harmony.GetPatchInfo(method) is { } info && info.Owners.Count > 0;

        PrefabSource.Parsed += PrefabXmlRegistry.Record;
        PrefabXmlDump.Install();
        // Invalidates XML registries and bumps environment versions when new custom prefabs are registered.
        PrefabSource.Registered += static _ =>
        {
            PrefabXmlRegistry.Touch();
            UIEnvironmentVersion.Touch();
        };
        // Invalidates cached prefab sections when prefab reload requests occur.
        PrefabSource.ReloadRequested += PrefabFingerprint.Invalidate;
        PrefabSource.EnvironmentChanged += UIEnvironmentVersion.Touch;

        GeneratedPrefabContextPatch.Patch(Harmony);
        GauntletMovieTimingPatch.Patch(Harmony);
        PrefabRuntimes.Register(Instance);
    }

    /// <summary>
    /// Warms up Roslyn compilers on a background thread during initial loading to eliminate JIT compilation latency during subsequent movie opens.
    /// </summary>
    public static void WarmUp() => Manager.WarmUpCompilers();

    /// <summary>
    /// Attempts to serve a compiled movie variant for the specified movie name and ViewModel data source.
    /// </summary>
    /// <param name="widgetFactory">The widget factory resolving prefabs and widgets.</param>
    /// <param name="movieName">The root movie name requested by the Gauntlet UI system.</param>
    /// <param name="dataSource">The ViewModel instance bound to the movie.</param>
    /// <returns><see langword="true"/> if a compiled movie variant was successfully located or compiled; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Handles uncompiled base game movies, modified XML prefabs, and screens using derived ViewModel types that miss TaleWorlds generated variants.
    /// </remarks>
    public bool TryServe(WidgetFactory widgetFactory, string movieName, IViewModel? dataSource)
    {
        // Synchronizes dotted path resolution state prior to fingerprinting and generation to account for subsequent transpiler reapplications.
        CodeGeneratorEnvironment.DottedPathsResolveCorrectly = PrefabRuntimes.DottedAttributePathsResolve;
        return Manager.TryUseCompiledPrefab(widgetFactory, movieName, dataSource);
    }

    /// <summary>
    /// Determines whether the specified assembly is a dynamically compiled prefab assembly generated by this runtime.
    /// </summary>
    public bool IsOwnVariant(Assembly variantAssembly) => CompiledPrefabManager.IsGeneratedAssembly(variantAssembly);
}