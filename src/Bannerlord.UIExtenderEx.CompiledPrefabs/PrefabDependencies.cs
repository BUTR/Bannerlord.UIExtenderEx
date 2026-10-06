using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Tracks and validates assembly dependencies for compiled prefabs against runtime assembly identities.
/// </summary>
/// <remarks>
/// <para>
/// A compiled prefab build depends on two sets of assemblies:
/// types inspected by the code generator during emission (tracked via <c>TypeDependencies</c> and expanded to include base types and interfaces),
/// and assemblies referenced directly by the compiled binary (<c>AssemblyRef</c> metadata table).
/// Generated code uses fully qualified <c>global::</c> type paths without namespace imports, preventing unreferenced assemblies from altering compilation output.
/// Updating unrelated libraries (such as Harmony or MonoMod) does not invalidate cached builds; only modifications to modules providing referenced ViewModels, widgets, or mixins trigger recompilation.
/// </para>
/// <para>
/// Dependencies are formatted as <c>name:description</c>, where the description matches
/// <see cref="PrefabFingerprint.DescribeAssembly(PrefabAssemblyReference, IReadOnlyList{string})"/>: Module Version ID (MVID) for game and mod binaries,
/// or assembly version for framework assemblies. Sorted alphabetically to produce deterministic dependency lists.
/// </para>
/// </remarks>
public static class PrefabDependencies
{
    /// <summary>
    /// Describes all assemblies containing types inspected during code generation, expanding each type across its inheritance hierarchy, interfaces, generic arguments, and element types.
    /// </summary>
    /// <param name="inspected">The collection of types directly inspected by the code generator.</param>
    /// <returns>A dictionary mapping assembly names to their identity descriptors.</returns>
    public static Dictionary<string, string> DescribeInspected(IEnumerable<Type> inspected)
    {
        var shipped = PrefabFingerprint.ShippedDirectories;
        var described = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var assembly in Expand(inspected).Select(x => x.Assembly).Distinct())
        {
            if (assembly.GetName().Name is not { } name || CompiledPrefabManager.IsGeneratedAssemblyName(name) || described.ContainsKey(name))
                continue;
            described[name] = Describe(assembly, shipped);
        }
        return described;
    }

    /// <summary>
    /// Composes the complete dependency list for a compiled assembly from generator-inspected types and direct reference paths.
    /// </summary>
    /// <param name="inspected">The assemblies and descriptors recorded during type inspection.</param>
    /// <param name="usedReferencePaths">The paths of assemblies referenced during compilation.</param>
    /// <returns>A sorted list of formatted dependency entries (<c>name:description</c>).</returns>
    /// <remarks>
    /// Direct compilation references take precedence over inspected assemblies when names collide.
    /// </remarks>
    public static IReadOnlyList<string> Compose(IReadOnlyDictionary<string, string> inspected, IEnumerable<string> usedReferencePaths)
    {
        var shipped = PrefabFingerprint.ShippedDirectories;
        var all = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in inspected)
            all[pair.Key] = pair.Value;
        foreach (var path in usedReferencePaths)
        {
            if (PrefabAssemblyReference.TryRead(path) is not { } reference || CompiledPrefabManager.IsGeneratedAssemblyName(reference.Name))
                continue;
            all[reference.Name] = PrefabFingerprint.DescribeAssembly(reference, shipped);
        }
        return [.. all.Select(x => x.Key + ":" + x.Value)];
    }

    private static readonly object Lock = new();
    /// <summary>Valid dependencies verified against currently loaded assemblies; immutable for the lifetime of the process.</summary>
    private static readonly HashSet<string> HeldByLoaded = new(StringComparer.Ordinal);
    /// <summary>Valid dependencies verified against disk files; cleared when <see cref="UIEnvironmentVersion"/> changes.</summary>
    private static readonly HashSet<string> HeldOnDisk = new(StringComparer.Ordinal);
    private static int _checkedVersion = -1;
    private static IReadOnlyList<string>? _searchDirectories;

    /// <summary>
    /// Identifies the first dependency whose current identity diverges from its recorded identity, returning a mismatch description or <see langword="null"/> if all match.
    /// </summary>
    /// <param name="dependencies">The list of recorded dependency entries to validate.</param>
    /// <returns>A description formatted as <c>name recorded -&gt; current</c> if a dependency mismatch occurs; otherwise, <see langword="null"/>.</returns>
    /// <remarks>
    /// Validated on the main UI thread during movie instantiation. Results are cached in <see cref="HeldByLoaded"/> or <see cref="HeldOnDisk"/>
    /// until invalidation via <see cref="UIEnvironmentVersion"/> to minimize overhead.
    /// </remarks>
    public static string? FindChanged(IReadOnlyList<string> dependencies)
    {
        if (dependencies.Count == 0)
            return null;

        lock (Lock)
        {
            var version = UIEnvironmentVersion.Version;
            if (_checkedVersion != version)
            {
                HeldOnDisk.Clear();
                _searchDirectories = null;
                _checkedVersion = version;
            }

            ILookup<string, Assembly>? loaded = null;
            foreach (var dependency in dependencies)
            {
                if (HeldByLoaded.Contains(dependency) || HeldOnDisk.Contains(dependency))
                    continue;
                loaded ??= LoadedByName();
                if (Check(dependency, loaded, out var heldByLoaded) is { } changed)
                    return changed;
                (heldByLoaded ? HeldByLoaded : HeldOnDisk).Add(dependency);
            }
            return null;
        }
    }

    /// <summary>
    /// Identifies the first dependency that no currently loaded assembly satisfies, or <see langword="null"/> if every one is loaded as recorded.
    /// </summary>
    /// <param name="dependencies">The list of recorded dependency entries to validate.</param>
    /// <returns>The name of the first dependency that is not loaded as recorded; otherwise, <see langword="null"/>.</returns>
    /// <remarks>
    /// Stricter than <see cref="FindChanged"/>: a matching file on disk does not count. Loading a build ahead of need is only
    /// safe once what it references is in the process. A module's own loader may load its assembly later (MCM loads its game
    /// implementation that way), and a disabled module's assembly never arrives. On Mono, the game scanning the types of a build
    /// whose field types cannot be resolved throws during start-up.
    /// </remarks>
    public static string? FindNotLoaded(IReadOnlyList<string> dependencies)
    {
        if (dependencies.Count == 0)
            return null;

        lock (Lock)
        {
            var shipped = PrefabFingerprint.ShippedDirectories;
            ILookup<string, Assembly>? loaded = null;
            foreach (var dependency in dependencies)
            {
                if (HeldByLoaded.Contains(dependency))
                    continue;
                var separator = dependency.IndexOf(':');
                if (separator <= 0)
                    return dependency;
                var name = dependency.Substring(0, separator);
                var recorded = dependency.Substring(separator + 1);
                loaded ??= LoadedByName();
                if (!loaded[name].Any(x => Describe(x, shipped) == recorded))
                    return name;
                HeldByLoaded.Add(dependency);
            }
            return null;
        }
    }

    /// <summary>
    /// Validates a single dependency entry against loaded assemblies and disk references.
    /// </summary>
    /// <param name="dependency">The dependency entry string (<c>name:description</c>).</param>
    /// <param name="loaded">A lookup of currently loaded assemblies grouped by name.</param>
    /// <param name="heldByLoaded">Outputs <see langword="true"/> if verified against an in-memory loaded assembly.</param>
    /// <returns>A discrepancy description if validation fails; otherwise, <see langword="null"/>.</returns>
    private static string? Check(string dependency, ILookup<string, Assembly> loaded, out bool heldByLoaded)
    {
        heldByLoaded = false;
        var separator = dependency.IndexOf(':');
        if (separator <= 0)
            return dependency + " (unreadable)";
        var name = dependency.Substring(0, separator);
        var recorded = dependency.Substring(separator + 1);

        var shipped = PrefabFingerprint.ShippedDirectories;
        var copies = loaded[name].ToList();
        var now = copies.Select(x => Describe(x, shipped)).Distinct().ToList();
        if (now.Contains(recorded))
        {
            heldByLoaded = true;
            return null;
        }
        // When a referenceable loaded copy exists, a new build binds to it; mismatch indicates the recorded build was compiled against a different binary.
        if (copies.Any(PrefabReferenceSet.CanReference))
            return $"{name} {recorded} -> {string.Join(", ", now)}";

        // When not loaded into the current AppDomain, validates disk files in search directories against the recorded descriptor.
        var files = PrefabReferenceSet.FindFiles(name, _searchDirectories ??= PrefabReferenceSet.SearchDirectories());
        var descriptions = files.Select(x => PrefabFingerprint.DescribeAssembly(x, shipped)).Distinct().ToList();
        if (descriptions.Contains(recorded))
            return null;
        var found = now.Concat(descriptions).ToList();
        return $"{name} {recorded} -> {(found.Count == 0 ? "missing" : string.Join(", ", found))}";
    }

    /// <summary>Retrieves all loaded assemblies in the AppDomain excluding generated prefab assemblies, grouped by simple name.</summary>
    private static ILookup<string, Assembly> LoadedByName() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(x => !CompiledPrefabManager.IsGeneratedAssembly(x))
        .ToLookup(x => x.GetName().Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Formats an identity descriptor for a loaded assembly matching the format used by <see cref="PrefabFingerprint.DescribeAssembly(PrefabAssemblyReference, IReadOnlyList{string})"/>.
    /// </summary>
    /// <remarks>
    /// Assemblies loaded from memory or generated dynamically are identified by their MVID.
    /// </remarks>
    private static string Describe(Assembly assembly, IReadOnlyList<string> shipped)
    {
        string mvid;
        // Dynamically emitted modules without an assigned MVID fallback to a process-local identity.
        try { mvid = assembly.ManifestModule.ModuleVersionId.ToString("N"); }
        catch (Exception) { mvid = "process-" + RuntimeHelpers.GetHashCode(assembly).ToString("x8"); }
        string? location;
        try { location = assembly.IsDynamic ? null : assembly.Location; }
        catch (NotSupportedException) { location = null; }
        return string.IsNullOrEmpty(location)
            ? "dynamic-" + mvid
            : PrefabFingerprint.DescribeAssembly(location!, mvid, assembly.GetName().Version ?? new Version(), shipped);
    }

    private static IEnumerable<Type> Expand(IEnumerable<Type> types)
    {
        var visited = new HashSet<Type>();
        var pending = new Stack<Type>(types);
        while (pending.Count > 0)
        {
            var type = pending.Pop();
            if (!visited.Add(type))
                continue;
            yield return type;

            if (type.HasElementType && type.GetElementType() is { } element)
                pending.Push(element);
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                pending.Push(type.GetGenericTypeDefinition());
                foreach (var argument in type.GetGenericArguments())
                    pending.Push(argument);
            }
            if (type.IsGenericParameter)
                continue;
            if (type.DeclaringType is { } declaring)
                pending.Push(declaring);
            if (type.BaseType is { } baseType)
                pending.Push(baseType);
            foreach (var interfaceType in type.GetInterfaces())
                pending.Push(interfaceType);
        }
    }
}