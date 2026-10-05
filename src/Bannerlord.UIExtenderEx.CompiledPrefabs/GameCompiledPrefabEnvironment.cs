using Bannerlord.BUTR.Shared.Helpers;

using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Runtimes;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Implements <see cref="ICompiledPrefabEnvironment"/> using live TaleWorlds Gauntlet resources, Roslyn compiler services, and module directory caches.
/// </summary>
public sealed class GameCompiledPrefabEnvironment : ICompiledPrefabEnvironment
{
    private readonly Lazy<string?> _cacheDirectory = new(CreateCacheDirectory);

    /// <summary>
    /// Gets a value indicating whether compiled prefabs are enabled and the Gauntlet movie routing switch patch is active.
    /// Prefabs cannot be compiled or preloaded if the movie switch is absent, ensuring unmatched prefabs safely fall back to XML parsing.
    /// </summary>
    public bool IsEnabled => CompiledPrefabSettings.CompiledPrefabs && PrefabRuntimes.IsMovieSwitchInstalled;

    public bool DumpGeneratedCode => CompiledPrefabSettings.DumpGeneratedCode;

    public bool RecordTimings => CompiledPrefabSettings.RecordTimings;

    public string? CacheDirectory => _cacheDirectory.Value;

    public string? CacheGeneration => PrefabFingerprint.ComputeGeneration();

    /// <summary>Retrieves seed cache file paths (<see cref="PrefabCache.FileName"/>) residing at the root of loaded game modules.</summary>
    public IReadOnlyList<string> SeedCaches => FindSeedCaches();

    public ICSharpCompiler Compiler { get; } = new RoslynCompiler();

    public string? ComputeFingerprint(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure) =>
        PrefabFingerprint.Compute(widgetFactory, movieName, viewModelType, out _, out failure);

    public IPrefabCompilationSnapshot? BeginSnapshot(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure) =>
        PrefabCompilationSnapshot.Begin(widgetFactory, movieName, viewModelType, out failure);

    /// <summary>
    /// Generates C# source files using the compilation snapshot's pinned prefabs, ensuring that AST generation and fingerprint hashing reference identical objects.
    /// </summary>
    public IReadOnlyList<GeneratedSource> GenerateSources(IPrefabCompilationSnapshot snapshot)
    {
        var typed = (PrefabCompilationSnapshot) snapshot;
        var context = new PrefabCodeGenerator(CompiledPrefabManager.GeneratedNamespace,
            typed.WidgetFactory, UIResourceManager.SpriteData, UIResourceManager.BrushFactory);
        context.AddMovie(typed.MovieName, typed.ViewModelType.FullName, typed.ViewModelType);
        var sources = context.GenerateInMemory().Select(x => new GeneratedSource(x.Key, x.Value)).ToList();
        // Shown once per build rather than once per load: a cached build is not generated again
        foreach (var warning in context.Warnings)
            Warn(warning);
        return sources;
    }

    /// <summary>
    /// Loads the compiled assembly into the current process. Offloads execution to background worker threads where possible to avoid main-thread assembly load latency.
    /// </summary>
    public Assembly LoadAssembly(byte[] assembly)
    {
        var stopwatch = Stopwatch.StartNew();
        var loaded = Assembly.Load(assembly);
        Trace.TraceInformation("UIExtenderEx: loaded '{0}' in {1} ms on thread {2}", loaded.GetName().Name, stopwatch.ElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId);
        return loaded;
    }

    /// <summary>
    /// Registers generated widget types into <see cref="WidgetInfo"/> and binds the assembly's prefab factory delegate.
    /// <para>
    /// Prefab widget instantiation requires all widget types to be registered in the static <see cref="WidgetInfo"/> lookup table.
    /// Widget types are registered via <see cref="PrefabSource.AddWidgetTypes"/> and validated before returning the creator delegate.
    /// </para>
    /// </summary>
    public Action<GeneratedPrefabContext>? CreateCreator(Assembly assembly)
    {
        var types = assembly.GetTypes();
        // By name, because the generated class deliberately implements no interface of the game's: see PrefabCodeGenerator
        var creatorType = assembly.GetType($"{CompiledPrefabManager.GeneratedNamespace}.{PrefabCodeGenerator.CreatorClassName}", false)
                          ?? types.FirstOrDefault(x => x.Name == PrefabCodeGenerator.CreatorClassName);
        if (creatorType?.GetMethod(PrefabCodeGenerator.CollectMethodName, [typeof(GeneratedPrefabContext)]) is not { } collectMethod)
            return null;

        var widgetTypes = types.Where(x => typeof(Widget).IsAssignableFrom(x)).ToList();
        PrefabSource.AddWidgetTypes(widgetTypes);
        foreach (var widgetType in widgetTypes)
            WidgetInfo.GetWidgetInfo(widgetType);

        if (Activator.CreateInstance(creatorType) is not { } creator)
            return null;

        return (Action<GeneratedPrefabContext>) Delegate.CreateDelegate(typeof(Action<GeneratedPrefabContext>), creator, collectMethod);
    }

    public void RunInBackground(Action action) => Task.Run(action);

    public void Warn(string message) => MessageUtils.DisplayUserWarning(message);

    private static IReadOnlyList<string> FindSeedCaches()
    {
        try
        {
            return [.. ModuleInfoHelper.GetLoadedModules().Select(x => Path.Combine(x.Path, PrefabCache.FileName)).Where(File.Exists)];
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: could not look for compiled prefab seeds: {0}", e.Message);
            return [];
        }
    }

    /// <summary>
    /// Resolves and creates the persistent cache directory at <c>Modules/Bannerlord.UIExtenderEx/CompiledPrefabs</c>.
    /// </summary>
    private static string? CreateCacheDirectory()
    {
        try
        {
            var moduleDirectory = ModuleInfoHelper.GetModuleByType(typeof(CompiledPrefabs.SubModule))?.Path;
            if (moduleDirectory is null)
            {
                var binDirectory = Path.GetDirectoryName(typeof(GameCompiledPrefabEnvironment).Assembly.Location);
                if (binDirectory is null || !binDirectory.TrimEnd(Path.DirectorySeparatorChar).EndsWith(Path.Combine("bin", "Win64_Shipping_Client"), StringComparison.OrdinalIgnoreCase))
                    return null;
                moduleDirectory = Path.GetFullPath(Path.Combine(binDirectory, "..", ".."));
            }

            var directory = Path.Combine(moduleDirectory, "CompiledPrefabs");
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: compiled prefab cache is unavailable, prefabs are recompiled every session: {0}", e.Message);
            return null;
        }
    }
}