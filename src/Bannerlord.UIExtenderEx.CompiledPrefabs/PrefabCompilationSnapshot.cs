using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Provides a pinned snapshot of a movie's parsed prefab hierarchy and ViewModel type during compilation preparation.
/// </summary>
public interface IPrefabCompilationSnapshot : IDisposable
{
    string MovieName { get; }

    Type ViewModelType { get; }

    /// <summary>
    /// Collects reference assembly paths and computes the cache fingerprint for this snapshot's pinned prefab tree.
    /// </summary>
    /// <returns>A <see cref="PrefabCompilationInputs"/> containing reference paths and the composite fingerprint.</returns>
    /// <remarks>
    /// Executed after code generation to ensure any dynamically loaded assemblies referenced by generated code are captured.
    /// </remarks>
    PrefabCompilationInputs CollectInputs();
}

/// <summary>
/// Represents the input reference assemblies and fingerprint key for a compilation task.
/// </summary>
/// <param name="ReferencePaths">The collection of reference assembly file paths passed to the compiler.</param>
/// <param name="Fingerprint">The cache fingerprint key identifying this compilation unit.</param>
public sealed record PrefabCompilationInputs(IReadOnlyList<string> ReferencePaths, string Fingerprint);

/// <summary>
/// Manages a single parsed, pinned traversal of a movie's prefab tree across fingerprint calculation and code generation.
/// </summary>
/// <remarks>
/// <para>
/// Prevents cache consistency mismatches caused by multiple parses. Without pinning, <see cref="WidgetFactory"/> parses prefabs on demand
/// and releases unreferenced instances, allowing intervening modifications (such as dynamically registered prefab factories or hot reloads)
/// to produce divergent XML trees between hashing and generation.
/// </para>
/// <para>
/// Acquires a pinning <see cref="WidgetFactoryLookup.PrefabLease"/> across the entire dependency closure, ensuring the generator
/// operates on the exact <see cref="WidgetPrefab"/> instances that produced the fingerprint. Disposing the snapshot releases
/// pinned leases so subsequent movie instantiations can observe new patches or resource updates.
/// </para>
/// </remarks>
public sealed class PrefabCompilationSnapshot : IPrefabCompilationSnapshot
{
    private readonly WidgetFactoryLookup.PrefabLease _lease;

    public WidgetFactory WidgetFactory { get; }
    public string MovieName { get; }
    public Type ViewModelType { get; }
    public PrefabFingerprint.PrefabSection Section { get; }

    private PrefabCompilationSnapshot(WidgetFactory widgetFactory, string movieName, Type viewModelType, WidgetFactoryLookup.PrefabLease lease, PrefabFingerprint.PrefabSection section)
    {
        WidgetFactory = widgetFactory;
        MovieName = movieName;
        ViewModelType = viewModelType;
        _lease = lease;
        Section = section;
    }

    public static PrefabCompilationSnapshot? Begin(WidgetFactory widgetFactory, string movieName, Type viewModelType) =>
        Begin(widgetFactory, movieName, viewModelType, out _);

    /// <summary>
    /// Traverses the movie's prefab hierarchy and creates a pinned snapshot, returning <see langword="null"/> if any referenced prefab lacks recorded XML.
    /// </summary>
    /// <param name="widgetFactory">The widget factory resolving prefabs and widgets.</param>
    /// <param name="movieName">The root movie or prefab identifier.</param>
    /// <param name="viewModelType">The ViewModel type bound to the movie root.</param>
    /// <param name="failure">When returning <see langword="null"/>, outputs the reason snapshot creation failed.</param>
    /// <returns>A new <see cref="PrefabCompilationSnapshot"/> instance, or <see langword="null"/> if compilation inputs are unavailable.</returns>
    public static PrefabCompilationSnapshot? Begin(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure)
    {
        var lease = WidgetFactoryLookup.PrefabLease.Begin(widgetFactory, pin: true);
        try
        {
            var section = PrefabFingerprint.BuildPrefabSection(widgetFactory, movieName, lease, out failure);
            if (section is not null)
                return new(widgetFactory, movieName, viewModelType, lease, section);

            lease.Dispose();
            return null;
        }
        catch (Exception)
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Collects reference assembly paths and computes the fingerprint key for compilation.
    /// </summary>
    /// <returns>A <see cref="PrefabCompilationInputs"/> containing reference paths and the fingerprint string.</returns>
    /// <remarks>
    /// Collected after code generation to capture types dynamically loaded during emission. Reference assemblies themselves are validated post-build via <see cref="PrefabDependencies"/>.
    /// </remarks>
    public PrefabCompilationInputs CollectInputs()
    {
        var assemblies = PrefabReferenceSet.Collect(WidgetFactory, ViewModelType, Section);
        var fingerprint = PrefabFingerprint.Compose(WidgetFactory, ViewModelType, Section, out _);
        return new(PrefabReferenceSet.ToPaths(assemblies), fingerprint);
    }

    /// <summary>Gets the collection of prefab names pinned by this snapshot.</summary>
    public IEnumerable<string> PrefabNames => _lease.Pinned!.Keys;

    public void Dispose()
    {
        try
        {
            _lease.Dispose();
        }
        catch (Exception e)
        {
            // Suppression ensures failure to release pinned lease references does not abort movie instantiation.
            Trace.TraceWarning("UIExtenderEx: could not release the prefabs of '{0}': {1}", MovieName, e.Message);
        }
    }
}