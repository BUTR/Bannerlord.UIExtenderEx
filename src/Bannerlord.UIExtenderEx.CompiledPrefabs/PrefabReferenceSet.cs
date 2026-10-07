using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.ViewModels;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Resolves the transitive closure of assembly references required to compile generated prefab C# code.
/// <para>
/// Gathers all assemblies the compilation could reference and supplies them to the compiler.
/// Explicit dependencies are subsequently recorded (<see cref="PrefabDependencies"/>) and validated before reusing cached builds.
/// </para>
/// </summary>
public static class PrefabReferenceSet
{
    private sealed record Entry(int Version, IReadOnlyList<PrefabAssemblyReference> Assemblies);

    /// <summary>
    /// Identifies a cached set of resolved assembly references for a given <see cref="WidgetFactory"/>, ViewModel type, movie name, and closure hash.
    /// </summary>
    private readonly record struct CacheKey(WidgetFactory? Factory, Type? ViewModelType, string? MovieName, string? Closure);

    private static readonly Dictionary<CacheKey, Entry> Cache = new();
    private static readonly object Lock = new();

    /// <summary>
    /// Resolves seed assemblies and their transitive dependencies into a list of <see cref="PrefabAssemblyReference"/> instances.
    /// Null arguments collect the baseline assembly set and enabled mixin assemblies for compiler warm-up.
    /// <para>
    /// Cached assemblies remain valid until <paramref name="closure"/> changes or <see cref="UIEnvironmentVersion"/> is incremented.
    /// </para>
    /// </summary>
    public static IReadOnlyList<PrefabAssemblyReference> Collect(WidgetFactory? widgetFactory, Type? viewModelType, PrefabFingerprint.PrefabSection? closure)
    {
        var version = UIEnvironmentVersion.Version;
        var key = new CacheKey(widgetFactory, viewModelType, closure?.MovieName, closure?.Key);
        lock (Lock)
        {
            if (Cache.TryGetValue(key, out var cached) && cached.Version == version)
                return cached.Assemblies;

            var collected = Select(Seeds(widgetFactory, viewModelType, closure));
            // One entry per (movie, ViewModel) pair of the factory in use. A pair's older closures are worthless the moment
            // it has a new one, and the game builds a new widget factory on every resource refresh - holding those, and the
            // prefab sections that name them, would keep a refresh's worth of dead objects alive for the session.
            //
            // Only this pair's own older closures, though. Dropping every entry that merely shares the ViewModel type made
            // two movies with one ViewModel - a screen and its tooltip, a list and its item - evict each other on every
            // load, so neither ever saw a cache hit and each paid the walk over every loaded assembly again.
            foreach (var obsolete in Cache.Keys.Where(x => IsObsolete(x, key)).ToList())
                Cache.Remove(obsolete);
            Cache[key] = new(version, collected);
            return collected;
        }
    }

    /// <summary>
    /// Determines whether a cached entry is obsolete compared to the newly collected key, such as when belonging to a replaced <see cref="WidgetFactory"/> or an older closure.
    /// </summary>
    private static bool IsObsolete(CacheKey key, CacheKey collecting)
    {
        if (!ReferenceEquals(key.Factory, collecting.Factory))
            return true;
        if (key.ViewModelType != collecting.ViewModelType || key.Closure == collecting.Closure)
            return false;
        // The same movie saying something else than it did. Movie names identify the pair; a null section is the
        // warm-up's broad set, which no movie replaces.
        return key.MovieName is not null && collecting.MovieName is not null && key.MovieName == collecting.MovieName;
    }

    /// <summary>Flushes all cached assembly reference collections.</summary>
    public static void ClearCache()
    {
        lock (Lock)
            Cache.Clear();
    }

    /// <summary>Extracts file paths from a collection of <see cref="PrefabAssemblyReference"/> instances.</summary>
    public static List<string> ToPaths(IEnumerable<PrefabAssemblyReference> assemblies) =>
        [.. assemblies.Select(x => x.Path)];

    /// <summary>
    /// Resolves assembly references and returns their file paths for compiler consumption.
    /// </summary>
    public static List<string> CollectPaths(WidgetFactory? widgetFactory, Type? viewModelType, PrefabFingerprint.PrefabSection? closure = null) =>
        ToPaths(Collect(widgetFactory, viewModelType, closure));

    private static HashSet<Assembly> Seeds(WidgetFactory? widgetFactory, Type? viewModelType, PrefabFingerprint.PrefabSection? closure)
    {
        var seeds = new HashSet<Assembly>
        {
            typeof(ViewModel).Assembly,
            typeof(Widget).Assembly,
            typeof(WidgetPrefab).Assembly,
            typeof(GauntletMovie).Assembly,
            typeof(SpriteData).Assembly,
            // What generated code names of UIExtenderEx: the by-name binding runtime, and the core for ViewModelMixins.Get<T>.
            // Not this assembly - nothing generated names it, and it would bring the compiler and everything else the
            // compiled runtime uses into every build's references.
            typeof(DynamicMember).Assembly,
            typeof(ViewModelMixins).Assembly,
        };
        if (viewModelType is not null)
            seeds.Add(viewModelType.Assembly);
        if (widgetFactory is not null)
        {
            // The widget classes this movie's own prefab tree names, and only those. Seeding from everything the factory
            // knows meant a mod loading its first screen invalidated every cached prefab in the game, this movie's included.
            // Resolved through PrefabFingerprint, which is also what hashes the resolutions: an assembly is compiled
            // against because a widget name resolves into it, and the hash has to be watching the very same resolution.
            foreach (var widgetTypeName in closure?.WidgetTypes ?? widgetFactory.GetWidgetTypes())
            {
                if (PrefabFingerprint.ResolveWidgetType(widgetFactory, widgetTypeName) is { } widgetType)
                    seeds.Add(widgetType.Assembly);
            }
            seeds.RemoveWhere(CompiledPrefabManager.IsGeneratedAssembly);
        }
        foreach (var mixinType in viewModelType is null ? MixinRegistrations.AllMixinTypes() : MixinRegistrations.ForRoot(viewModelType).SelectMany(x => x.Value).Distinct())
            seeds.Add(mixinType.Assembly);
        return seeds;
    }

    /// <summary>
    /// Traverses the transitive dependency graph from seed assemblies to select compilation references from metadata.
    /// <para>
    /// Resolves potential ambiguities between competing implementations of <c>System.Numerics.Vector2</c> by keeping
    /// the one the game's <c>Vector2PropertyChanged</c> carries.
    /// </para>
    /// </summary>
    public static List<PrefabAssemblyReference> Select(IEnumerable<Assembly> seeds)
    {
        var roots = seeds.Where(CanReference).OrderBy(x => x.GetName().Name, StringComparer.Ordinal).ToList();
        // Reading metadata can load the reader itself; Vector2 can also trigger a framework type forwarder.
        // Settle those before taking the loaded-assembly snapshot used for binding precedence.
        var rootReferences = roots.Select(ReadLoaded).ToList();
        var vector2Home = GameVector2Home();
        var loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!CanReference(assembly))
                continue;
            var name = assembly.GetName().Name;
            if (!loaded.ContainsKey(name))
                loaded[name] = assembly;
        }
        // Not whichever copy of its name loaded first: mods ship System.Numerics.Vectors builds that forward to System.Numerics
        if (CanReference(vector2Home))
            loaded[vector2Home.GetName().Name] = vector2Home;

        var directories = SearchDirectories(roots);

        var selected = new Dictionary<string, PrefabAssemblyReference>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<PrefabAssemblyReference>(rootReferences);
        var discovered = new HashSet<string>(queue.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
        while (queue.Count > 0)
        {
            var assembly = queue.Dequeue();
            var name = assembly.Name;
            if (selected.ContainsKey(name))
                continue;
            // UIExtenderEx itself references Roslyn; generated code never does, and importing it is not free
            if (name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
                continue;
            // Never compile one prefab against another compiled prefab
            if (CompiledPrefabManager.IsGeneratedAssemblyName(name))
                continue;
            selected[name] = assembly;
            var assemblyIsFramework = IgnoresAccessChecksSource.IsFramework(name);
            foreach (var dependency in assembly.Dependencies)
            {
                var reference = dependency.Name;
                if (discovered.Contains(reference) || reference.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
                    || CompiledPrefabManager.IsGeneratedAssemblyName(reference))
                    continue;
                // The framework is very nearly a leaf. A framework assembly is worth having when the game or a mod names
                // it, and following the framework's own graph from there is not: in the game's mono folder that graph is
                // the whole BCL - System.Web, System.Design, System.DirectoryServices and a hundred more arrive through references
                // nothing emitted can reach, and each is metadata Roslyn imports on every compilation. The exception is
                // the core, which nothing but another framework assembly ever names and without which there is no
                // System.Object: on .NET 6 every assembly reaches it through System.Runtime rather than directly.
                // And netstandard, one level: it is a facade, and the assemblies it forwards to are where the types of
                // every netstandard library's signatures live - the game's included.
                if (assemblyIsFramework && !IsFacade(name) && IgnoresAccessChecksSource.IsFramework(reference) && !IsCore(reference))
                    continue;
                PrefabAssemblyReference? resolved;
                if (loaded.TryGetValue(reference, out var loadedDependency))
                    resolved = ReadLoaded(loadedDependency);
                else
                {
                    var candidates = FindFiles(reference, new[] { Path.GetDirectoryName(assembly.Path) }.OfType<string>().Concat(directories));
                    resolved = Choose(candidates, dependency);
                }
                if (resolved is not null && discovered.Add(reference))
                    queue.Enqueue(resolved);
            }
        }

        DropDuplicateVector2(selected, vector2Home);

        // Ordered, because this list is hashed as well as compiled against
        return [.. selected.Values.OrderBy(x => x.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Selects the most appropriate assembly candidate file on disk when multiple files with the same assembly name exist.
    /// <para>
    /// Prioritizes candidate files matching the exact version requested by the referencing assembly dependency.
    /// If version matches cannot disambiguate candidates, picks the closest candidate in directory search order and logs a warning.
    /// </para>
    /// </summary>
    public static PrefabAssemblyReference? Choose(IReadOnlyList<PrefabAssemblyReference> candidates, PrefabAssemblyReference.Dependency asked)
    {
        if (candidates.Count == 0)
            return null;
        if (candidates.Count == 1 || candidates.Select(x => x.Mvid).Distinct().Count() == 1)
            return candidates[0];

        var matching = candidates.Where(x => x.Version == asked.Version).ToList();
        if (matching.Count > 0 && matching.Select(x => x.Mvid).Distinct().Count() == 1)
            return matching[0];

        var chosen = (matching.Count > 0 ? matching : candidates)[0];
        Trace.TraceWarning(
            "UIExtenderEx: '{0}' version {1} matches {2} different files; compiling against '{3}'. Candidates: {4}",
            asked.Name, asked.Version, candidates.Select(x => x.Mvid).Distinct().Count(), chosen.Path,
            string.Join(", ", candidates.Select(x => $"{x.Path} ({x.Version}, {Short(x.Mvid)})")));
        return chosen;
    }

    private static string Short(string mvid) => mvid.Length <= 8 ? mvid : mvid.Substring(0, 8);

    /// <summary>
    /// Determines whether the specified assembly name corresponds to core BCL or framework forwarding facade assemblies.
    /// </summary>
    private static bool IsCore(string name) =>
        // On .NET 6 the implementation behind every facade is a System.Private.* assembly, named by the facade and by
        // nothing else; the types the facade forwards are only resolvable when it is there.
        name.StartsWith("System.Private.", StringComparison.OrdinalIgnoreCase)
        || name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase)
        || name.Equals("netstandard", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System.Runtime", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System.Private.CoreLib", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System.Core", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves search directories where unreferenced or non-loaded dependency assemblies can be discovered on disk.
    /// </summary>
    private static List<string> SearchDirectories(IEnumerable<Assembly> roots)
    {
        var gauntletDirectory = Path.GetDirectoryName(typeof(WidgetPrefab).Assembly.Location);
        var monoDirectory = gauntletDirectory is null ? null : Path.Combine(gauntletDirectory, "mono", "lib", "mono", "4.5");
        var directories = roots.Select(x => Path.GetDirectoryName(x.Location))
            .Concat([gauntletDirectory])
            .Concat(GetModuleDirectories())
            .Concat([AppDomain.CurrentDomain.BaseDirectory, RuntimeEnvironment.GetRuntimeDirectory(), monoDirectory])
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        directories.AddRange([.. directories.Select(x => Path.Combine(x, "Facades"))]);
        return directories;
    }

    /// <summary>Resolves default assembly search directories across loaded modules and engine runtime paths.</summary>
    public static List<string> SearchDirectories() => SearchDirectories([]);

    /// <summary>Finds and reads all candidate assembly files matching <paramref name="name"/> across <paramref name="directories"/>.</summary>
    public static List<PrefabAssemblyReference> FindFiles(string name, IEnumerable<string> directories) =>
    [
        .. directories.Select(x => Path.Combine(x, name + ".dll")).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists).Select(PrefabAssemblyReference.TryRead).Where(x => x is not null && x.Name == name)
            .Cast<PrefabAssemblyReference>(),
    ];

    /// <summary>
    /// Determines whether the specified assembly is a type forwarding facade like <c>netstandard</c>.
    /// </summary>
    private static bool IsFacade(string name) => name.Equals("netstandard", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The assembly that defines the <c>System.Numerics.Vector2</c> of the game's <c>Vector2PropertyChanged</c>, which every
    /// generated subscription to it has to name.
    /// <para>
    /// On .NET Framework the game's <c>System.Numerics.Vectors</c> is a netstandard build that defines the type, and the
    /// framework's <c>System.Numerics</c> defines another; this assembly's own <c>typeof(Vector2)</c> is the framework's.
    /// </para>
    /// </summary>
    private static Assembly GameVector2Home() =>
        typeof(PropertyOwnerObject).GetEvent("Vector2PropertyChanged")?.EventHandlerType?.GetGenericArguments() is { Length: 3 } arguments
            ? arguments[2].Assembly
            : typeof(System.Numerics.Vector2).Assembly;

    /// <summary>
    /// Deduplicates conflicting definitions of <c>System.Numerics.Vector2</c> across referenced assemblies, keeping
    /// <paramref name="vector2Home"/>'s.
    /// <para>
    /// Counting which definition the selected assemblies name let mods naming <c>System.Numerics</c> outvote the game, and
    /// no movie with a <c>Vector2PropertyChanged</c> subscription compiled (CS0012). A forwarding copy of
    /// <c>System.Numerics.Vectors</c> selected in place of the game's left the framework's the only definition, and every
    /// such subscription failed with <see cref="MissingMethodException"/>.
    /// </para>
    /// </summary>
    private static void DropDuplicateVector2(Dictionary<string, PrefabAssemblyReference> selected, Assembly vector2Home)
    {
        var home = vector2Home.GetName().Name;
        if (CanReference(vector2Home) && (!selected.TryGetValue(home, out var current) || !string.Equals(current.Path, vector2Home.Location, StringComparison.OrdinalIgnoreCase)))
            selected[home] = ReadLoaded(vector2Home);

        foreach (var name in selected.Values.Where(x => x.DefinesVector2 && !x.Name.Equals(home, StringComparison.OrdinalIgnoreCase)).Select(x => x.Name).ToList())
            selected.Remove(name);
    }

    /// <summary>
    /// Reads assembly metadata from disk for a currently loaded assembly.
    /// </summary>
    private static PrefabAssemblyReference ReadLoaded(Assembly assembly)
    {
        try
        {
            return PrefabAssemblyReference.Read(assembly);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException)
        {
            throw new PrefabReferenceException(assembly.GetName().Name ?? assembly.FullName ?? string.Empty, GetLocation(assembly), e);
        }
    }

    private static string? GetLocation(Assembly assembly)
    {
        try { return assembly.Location; }
        catch (Exception) { return null; }
    }

    private static IEnumerable<string> GetModuleDirectories()
    {
        // The native module list is unavailable in tools/tests and may not exist during compiler warm-up.
        try
        {
            var platform = Path.GetFileName(Path.GetDirectoryName(typeof(WidgetPrefab).Assembly.Location));
            return [.. ModuleInfoHelper.GetLoadedModules().Select(x => Path.Combine(x.Path, "bin", platform))];
        }
        catch (Exception) { return []; }
    }

    /// <summary>
    /// Determines whether the specified assembly can be referenced in compilation (excluding dynamic assemblies, generated prefabs, and memory-only images).
    /// </summary>
    public static bool CanReference(Assembly assembly) =>
        !assembly.IsDynamic && !CompiledPrefabManager.IsGeneratedAssembly(assembly) && !string.IsNullOrEmpty(GetLocation(assembly));
}