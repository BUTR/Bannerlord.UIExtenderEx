using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

using System;
using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Defines the external dependencies and environmental services required by <see cref="CompiledPrefabManager"/>.
/// </summary>
public interface ICompiledPrefabEnvironment
{
    /// <summary>Gets a value indicating whether compiled prefabs are enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>Gets a value indicating whether generated C# sources are dumped to disk.</summary>
    bool DumpGeneratedCode { get; }

    /// <summary>Gets a value indicating whether compilation and execution timings are recorded to disk (<see cref="PrefabTimings"/>).</summary>
    bool RecordTimings { get; }

    /// <summary>
    /// Gets the cache directory path for compiled assemblies, logs, and dumped sources, or <see langword="null"/> if caching is unavailable.
    /// </summary>
    string? CacheDirectory { get; }

    /// <summary>
    /// Gets the generation key identifying current engine and UIExtenderEx builds. Assemblies from mismatched generations are purged.
    /// </summary>
    string? CacheGeneration { get; }

    /// <summary>
    /// Gets the file paths of read-only seed cache archives supplied by modules or mod packs.
    /// </summary>
    IReadOnlyList<string> SeedCaches { get; }

    /// <summary>Gets the Roslyn C# compiler instance.</summary>
    ICSharpCompiler Compiler { get; }

    /// <summary>
    /// Computes the cryptographic fingerprint representing the prefab XML tree and its dependencies.
    /// </summary>
    /// <param name="widgetFactory">The active Gauntlet <see cref="WidgetFactory"/>.</param>
    /// <param name="movieName">The name of the movie or prefab.</param>
    /// <param name="viewModelType">The primary ViewModel <see cref="Type"/> bound to the movie.</param>
    /// <param name="failure">When fingerprinting fails, contains the diagnostic error description.</param>
    /// <returns>A hexadecimal fingerprint hash string, or <see langword="null"/> if the movie cannot be fingerprinted.</returns>
    string? ComputeFingerprint(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure);

    /// <summary>
    /// Captures a consistent snapshot of the prefab XML tree and its dependency closure for code generation and hashing.
    /// </summary>
    /// <param name="widgetFactory">The active Gauntlet <see cref="WidgetFactory"/>.</param>
    /// <param name="movieName">The name of the movie or prefab.</param>
    /// <param name="viewModelType">The primary ViewModel <see cref="Type"/> bound to the movie.</param>
    /// <param name="failure">When snapshot creation fails, contains the diagnostic error description.</param>
    /// <returns>An <see cref="IPrefabCompilationSnapshot"/> instance, or <see langword="null"/> if snapshot creation fails.</returns>
    IPrefabCompilationSnapshot? BeginSnapshot(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure);

    /// <summary>Generates C# source files from the specified compilation snapshot.</summary>
    IReadOnlyList<GeneratedSource> GenerateSources(IPrefabCompilationSnapshot snapshot);

    /// <summary>
    /// Loads an in-memory compiled assembly into the current process. Safe to invoke on background threads.
    /// </summary>
    Assembly LoadAssembly(byte[] assembly);

    /// <summary>
    /// Registers generated widget types with the engine and retrieves the prefab factory delegate. Must be called from the main thread.
    /// </summary>
    Action<GeneratedPrefabContext>? CreateCreator(Assembly assembly);

    /// <summary>Dispatches work to execute asynchronously on a background worker thread.</summary>
    void RunInBackground(Action action);

    /// <summary>Logs or displays a warning message to the user.</summary>
    void Warn(string message);
}

/// <summary>
/// Provides a no-op fallback implementation of <see cref="ICompiledPrefabEnvironment"/> when compiled prefabs are disabled or unavailable.
/// </summary>
public sealed class DisabledCompiledPrefabEnvironment : ICompiledPrefabEnvironment
{
    public bool IsEnabled => false;
    public bool DumpGeneratedCode => false;
    public bool RecordTimings => false;
    public string? CacheDirectory => null;
    public string? CacheGeneration => null;
    public IReadOnlyList<string> SeedCaches => [];
    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> because the compiler is not available in disabled mode.
    /// </summary>
    public ICSharpCompiler Compiler => throw new InvalidOperationException("Compiled prefabs are disabled; there is no compiler.");
    public string? ComputeFingerprint(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure)
    {
        failure = null;
        return null;
    }
    public IPrefabCompilationSnapshot? BeginSnapshot(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure)
    {
        failure = null;
        return null;
    }
    public IReadOnlyList<GeneratedSource> GenerateSources(IPrefabCompilationSnapshot snapshot) => [];
    public Assembly LoadAssembly(byte[] assembly) => typeof(DisabledCompiledPrefabEnvironment).Assembly;
    public Action<GeneratedPrefabContext>? CreateCreator(Assembly assembly) => null;
    public void RunInBackground(Action action) { }
    public void Warn(string message) { }
}