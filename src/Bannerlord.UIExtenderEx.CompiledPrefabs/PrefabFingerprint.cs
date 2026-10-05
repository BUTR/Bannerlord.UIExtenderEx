using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Computes cryptographic cache keys and fingerprint hashes for (movie, ViewModel) pairs based on XML document structure,
/// widget type bindings, and active view model mixins.
/// </summary>
public static class PrefabFingerprint
{
    /// <summary>
    /// Represents the immutable closure of a movie's prefab dependency tree, containing component XML hashes and referenced widget type names.
    /// </summary>
    public sealed record PrefabSection(string MovieName, WidgetFactory Factory, int RegistrationVersion, Dictionary<string, string> Hashes, string Text, IReadOnlyList<string> WidgetTypes)
    {
        /// <summary>
        /// Gets the composite key identifying the exact XML document hashes and referenced widget types within this section.
        /// </summary>
        public string Key => new StringBuilder(Text.Length + WidgetTypes.Count * 24)
            .Append(MovieName).Append('\n').Append(Text).Append("widgets:").Append(string.Join(",", WidgetTypes)).ToString();

        /// <summary>Determines whether this section remains current with respect to the active <see cref="WidgetFactory"/> and XML registry state.</summary>
        public bool IsCurrent(WidgetFactory factory)
        {
            if (!ReferenceEquals(Factory, factory) || RegistrationVersion != PrefabXmlRegistry.Version)
                return false;
            foreach (var pair in Hashes)
            {
                // A callback can produce different XML on its next invocation without a registration change.
                if (CodeGeneratorEnvironment.Registrations.IsRegisteredCustomType(pair.Key))
                    return false;
                if (!PrefabXmlRegistry.TryGetHash(pair.Key, out var hash) || hash != pair.Value)
                    return false;
            }
            return true;
        }
    }

    private static readonly ConcurrentDictionary<string, PrefabSection> PrefabSections = new(StringComparer.Ordinal);
    private static WeakReference<WidgetFactory>? _currentFactory;

    public static string? Compute(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string inputs) =>
        Compute(widgetFactory, movieName, viewModelType, out inputs, out _);

    /// <summary>
    /// Computes the complete fingerprint hash for the specified movie and ViewModel type.
    /// Returns <see langword="null"/> if any prefab in the dependency tree lacks a recorded XML hash.
    /// </summary>
    /// <param name="widgetFactory">The active TaleWorlds <see cref="WidgetFactory"/>.</param>
    /// <param name="movieName">The name of the movie or root prefab.</param>
    /// <param name="viewModelType">The primary bound ViewModel <see cref="Type"/>.</param>
    /// <param name="inputs">Outputs the diagnostic input text concatenated to generate the hash.</param>
    /// <param name="failure">When fingerprint computation fails, outputs the diagnostic error reason.</param>
    /// <returns>A hexadecimal SHA-256 fingerprint hash, or <see langword="null"/> on failure.</returns>
    public static string? Compute(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string inputs, out string? failure)
    {
        inputs = string.Empty;
        var prefabs = GetPrefabSection(widgetFactory, movieName, out failure);
        if (prefabs is null)
            return null;
        return Compose(widgetFactory, viewModelType, prefabs, out inputs);
    }

    /// <summary>
    /// Composes and hashes the input string for a movie, incorporating the environment generation, ViewModel type,
    /// widget type resolutions, mixin precedence order, and prefab XML hashes.
    /// </summary>
    public static string Compose(WidgetFactory widgetFactory, Type viewModelType, PrefabSection prefabs, out string inputs)
    {
        var sb = new StringBuilder();
        sb.Append("generation:").Append(ComputeGeneration()).Append('\n');
        sb.Append("viewmodel:").Append(DescribeType(viewModelType)).Append('\n');

        // The generator walks dotted attribute paths the way the loader does, which depends on whether the loader's own
        // walk could be fixed (UIExtenderEx's WidgetExtensionsPatch). It is an input of the generated code like any other.
        sb.Append("dottedpaths:").Append(CodeGeneratorEnvironment.DottedPathsResolveCorrectly).Append('\n');

        // What each widget name in the tree resolves to, not merely which assemblies are involved. A mod registering its
        // own class over a name that was already taken - even by another class of the same assembly - changes every widget
        // the generated code instantiates while leaving the assembly list, and its MVIDs, exactly as they were.
        foreach (var widgetTypeName in prefabs.WidgetTypes)
        {
            sb.Append("widget:").Append(widgetTypeName).Append('=').Append(DescribeWidgetType(widgetFactory, widgetTypeName)).Append('\n');
            // The generator listens to only the notifications a widget class's IL raises, which a patch on one of its
            // methods can change while the class and its assembly stay exactly as they were (WidgetRaises)
            if (ResolveWidgetType(widgetFactory, widgetTypeName) is { } widgetType && WidgetRaises.PatchedMethodOf(widgetType) is { } patched)
                sb.Append("widgetpatched:").Append(widgetTypeName).Append('=').Append(patched).Append('\n');
        }

        // Grouped by target and in resolver order, because that order is what decides which mixin a contested member comes
        // from. A sorted, de-duplicated list of mixin types cannot tell two registration orders apart, and the two generate
        // different code. Include targets reachable from this root through properties, list items and enabled mixins;
        // registrations for unrelated screens cannot change this movie's bindings.
        // Which mixins, and in what order; what they contain is a dependency of the build like any other inspected type.
        foreach (var pair in MixinRegistrations.ForRoot(viewModelType))
        {
            sb.Append("mixintarget:").Append(DescribeType(pair.Key)).Append('\n');
            foreach (var mixin in pair.Value)
                sb.Append("mixin:").Append(DescribeType(mixin)).Append('\n');
        }

        sb.Append(prefabs.Text);
        inputs = sb.ToString();
        return PrefabXmlRegistry.Hash(inputs);
    }

    /// <summary>
    /// An assembly's build, or its identity when the machine rather than the game or a mod supplied the file.
    /// <para>
    /// What the game and its modules ship is the same file on every machine with the same versions, so its MVID is safe to
    /// hash and catches every rebuild. Anything else - the installed .NET, above all - is the machine's own: servicing
    /// replaces it under the same assembly version, binary-compatibly, so its MVID differs between two machines with the
    /// same setup, and on one machine after a Windows update, while the identity the compiled code binds to does not.
    /// Hashing those MVIDs made a cache impossible to share and rebuilt everything after an update.
    /// </para>
    /// </summary>
    public static string DescribeAssembly(PrefabAssemblyReference assembly, IReadOnlyList<string> shippedDirectories) =>
        DescribeAssembly(assembly.Path, assembly.Mvid, assembly.Version, shippedDirectories);

    public static string DescribeAssembly(string path, string mvid, Version version, IReadOnlyList<string> shippedDirectories) =>
        shippedDirectories.Any(x => IsUnder(path, x)) ? mvid : "v" + version;

    private static IReadOnlyList<string>? _shippedDirectories;

    /// <summary>
    /// Gets the game base directory and all loaded module directories.
    /// </summary>
    /// <remarks>
    /// A module does not always reside within the main game directory; modules subscribed via Steam Workshop
    /// reside within Steam's content directory and are treated as shipped binaries.
    /// </remarks>
    public static IReadOnlyList<string> ShippedDirectories => _shippedDirectories ??= FindShippedDirectories();

    private static IReadOnlyList<string> FindShippedDirectories()
    {
        var directories = new List<string>();
        // Resolves <game>/bin/<platform>/TaleWorlds.Library.dll
        if (Path.GetDirectoryName(typeof(ViewModel).Assembly.Location) is { Length: > 0 } binDirectory)
            directories.Add(Path.GetFullPath(Path.Combine(binDirectory, "..", "..")));
        try
        {
            directories.AddRange(ModuleInfoHelper.GetLoadedModules().Select(x => Path.GetFullPath(x.Path)));
        }
        catch (Exception)
        {
            // When running outside the game process (tests, tools), defaults to the game root directory.
        }
        return directories;
    }

    private static bool IsUnder(string path, string directory)
    {
        var root = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Describes the type that a widget name in a prefab tree resolves to, formatted such that any type change modifies the fingerprint hash.
    /// </summary>
    /// <remarks>
    /// Emits the full type name and its assembly name, or a fallback indicator when the name does not resolve to a widget class.
    /// Assembly build specifics are omitted here because dependencies are recorded by MVID and validated at load time
    /// via <see cref="PrefabDependencies"/>.
    /// </remarks>
    public static string DescribeWidgetType(WidgetFactory widgetFactory, string widgetTypeName)
    {
        if (ResolveWidgetType(widgetFactory, widgetTypeName) is { } type)
            return DescribeType(type);
        // Indicates a nested prefab whose XML hash is already included, or an unresolved type name.
        return widgetFactory.IsCustomTypeIncludingRegistered(widgetTypeName) ? "(prefab)" : "(unresolved)";
    }

    /// <summary>
    /// Formats a type descriptor for the cache fingerprint key, including its full name, normalized generic type arguments, and assembly name.
    /// </summary>
    /// <remarks>
    /// Generates an assembly-qualified descriptor excluding version, culture, and public key token.
    /// Closed generic <see cref="Type.FullName"/> values are avoided because they embed fully qualified argument names.
    /// Assembly build versions are validated separately via MVID in <see cref="PrefabDependencies"/>.
    /// </remarks>
    public static string DescribeType(Type type)
    {
        var sb = new StringBuilder();
        AppendTypeName(sb, type);
        return sb.Append(", ").Append(type.Assembly.GetName().Name).ToString();
    }

    private static void AppendTypeName(StringBuilder sb, Type type)
    {
        if (type.IsArray && type.GetElementType() is { } element)
        {
            AppendTypeName(sb, element);
            sb.Append('[').Append(',', type.GetArrayRank() - 1).Append(']');
            return;
        }
        if (!type.IsGenericType || type.IsGenericTypeDefinition)
        {
            sb.Append(type.FullName ?? type.Name);
            return;
        }
        sb.Append(type.GetGenericTypeDefinition().FullName).Append('[');
        var arguments = type.GetGenericArguments();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append('[').Append(DescribeType(arguments[i])).Append(']');
        }
        sb.Append(']');
    }

    /// <summary>
    /// Resolves the widget <see cref="Type"/> corresponding to the specified name using the widget factory's resolution rules, or <see langword="null"/> if unresolved.
    /// </summary>
    public static Type? ResolveWidgetType(WidgetFactory widgetFactory, string widgetTypeName)
    {
        try
        {
            return widgetFactory.IsBuiltinTypeIncludingRegistered(widgetTypeName)
                ? widgetFactory.GetBuiltinTypeIncludingRegistered(widgetTypeName)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves or computes the cached <see cref="PrefabSection"/> for the specified movie name.
    /// </summary>
    /// <remarks>
    /// Walking the movie's prefab tree causes <see cref="WidgetFactory"/> to parse all unreferenced prefabs.
    /// A cached section is reused while all referenced prefab hashes match, no new prefabs have been registered
    /// (<see cref="PrefabXmlRegistry.Version"/>), and the active <see cref="WidgetFactory"/> instance remains unchanged.
    /// </remarks>
    public static PrefabSection? GetPrefabSection(WidgetFactory widgetFactory, string movieName) =>
        GetPrefabSection(widgetFactory, movieName, out _);

    public static PrefabSection? GetPrefabSection(WidgetFactory widgetFactory, string movieName, out string? failure)
    {
        failure = null;
        DiscardOtherFactories(widgetFactory);
        if (PrefabSections.TryGetValue(movieName, out var cached) && cached.IsCurrent(widgetFactory))
            return cached;

        return BuildPrefabSection(widgetFactory, movieName, out failure);
    }

    /// <summary>
    /// Builds the <see cref="PrefabSection"/> immediately using a temporary pinning lease without leaving prefabs pinned after execution.
    /// </summary>
    public static PrefabSection? BuildPrefabSection(WidgetFactory widgetFactory, string movieName, out string? failure)
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(widgetFactory, pin: true);
        return BuildPrefabSection(widgetFactory, movieName, lease, out failure);
    }

    /// <summary>
    /// Builds the <see cref="PrefabSection"/> within an active pinning lease, ensuring parsed prefabs in the closure remain pinned in memory.
    /// </summary>
    /// <param name="widgetFactory">The widget factory used to resolve prefabs and widgets.</param>
    /// <param name="movieName">The root prefab or movie identifier.</param>
    /// <param name="lease">The active pinning lease over <paramref name="widgetFactory"/>.</param>
    /// <param name="failure">When returning <see langword="null"/>, outputs the reason compilation cannot proceed.</param>
    /// <returns>The constructed <see cref="PrefabSection"/>, or <see langword="null"/> if a referenced prefab lacks recorded XML.</returns>
    /// <remarks>
    /// Preserves parsed prefab instances across hash extraction and code generation (see <see cref="PrefabCompilationSnapshot"/>).
    /// </remarks>
    public static PrefabSection? BuildPrefabSection(WidgetFactory widgetFactory, string movieName, WidgetFactoryLookup.PrefabLease lease, out string? failure)
    {
        if (lease.Pinned is not { } pinned || !ReferenceEquals(lease.Factory, widgetFactory))
            throw new ArgumentException("A prefab section is built inside a pinning lease over the same widget factory.", nameof(lease));

        failure = null;
        DiscardOtherFactories(widgetFactory);
        var (closure, widgetTypes) = CollectClosureAndWidgetTypes(widgetFactory, movieName);
        if (closure.Count == 0)
        {
            failure = $"'{movieName}' is not a prefab the WidgetFactory knows";
            return null;
        }

        var sb = new StringBuilder();
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prefabName in closure.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!pinned.TryGetValue(prefabName, out var prefab) || !PrefabXmlRegistry.TryGetHash(prefab, out var hash))
            {
                // Unloadable dependencies or prefabs instantiated outside the recording pipeline cannot reuse older hashes; fallback to XML movie.
                failure = $"no XML was recorded for '{prefabName}', which the movie's prefab tree refers to";
                return null;
            }
            hashes[prefabName] = hash;
            sb.Append("prefab:").Append(prefabName).Append(':').Append(hash).Append('\n');
        }

        var section = new PrefabSection(movieName, widgetFactory, PrefabXmlRegistry.Version, hashes, sb.ToString(), widgetTypes);
        PrefabSections[movieName] = section;
        return section;
    }

    private static void DiscardOtherFactories(WidgetFactory factory)
    {
        if (_currentFactory is not null && _currentFactory.TryGetTarget(out var current) && ReferenceEquals(current, factory))
            return;
        PrefabSections.Clear();
        _currentFactory = new(factory);
    }

    /// <summary>Clears cached prefab sections during resource reload events and unit tests.</summary>
    public static void ClearCache()
    {
        PrefabSections.Clear();
        _currentFactory = null;
    }

    /// <summary>
    /// Invalidates cached prefab sections for movies that depend on any of the specified prefabs.
    /// </summary>
    /// <param name="prefabNames">The names of prefabs whose cached sections should be invalidated.</param>
    /// <remarks>
    /// Invoked when parsed prefabs are discarded to force re-parsing on subsequent movie load requests.
    /// </remarks>
    public static void Invalidate(IEnumerable<string> prefabNames)
    {
        var names = new HashSet<string>(prefabNames, StringComparer.Ordinal);
        foreach (var pair in PrefabSections.ToList())
        {
            if (pair.Value.Hashes.Keys.Any(names.Contains))
                PrefabSections.TryRemove(pair.Key, out _);
        }
    }

    private static string? _generation;

    /// <summary>
    /// Computes the environmental generation hash representing the compiler, code generator, compiled runtime, and core game assemblies.
    /// </summary>
    /// <remarks>
    /// Any modification to core assemblies, the compiler, or code generator alters this generation hash, invalidating previously cached assemblies.
    /// This value remains constant for the lifetime of the process.
    /// </remarks>
    public static string ComputeGeneration()
    {
        if (_generation is { } generation)
            return generation;
        var sb = new StringBuilder();
        AppendBuilds(sb, CoreAssemblies);
        return _generation = PrefabXmlRegistry.Hash(sb.ToString());
    }

    private static Assembly[] CoreAssemblies =>
    [
        typeof(PrefabCodeGenerator).Assembly,
        typeof(PrefabAssemblyReference).Assembly,
        typeof(WidgetPrefab).Assembly,
        typeof(Widget).Assembly,
        typeof(ViewModel).Assembly,
        typeof(GauntletMovie).Assembly,
    ];

    private static void AppendBuilds(StringBuilder sb, IEnumerable<Assembly> assemblies)
    {
        sb.Append("uiextenderex:").Append(Mvid(typeof(PrefabFingerprint).Assembly)).Append('\n');
        foreach (var assembly in assemblies.OrderBy(x => x.GetName().Name, StringComparer.Ordinal))
            sb.Append("assembly:").Append(assembly.GetName().Name).Append(':').Append(Mvid(assembly)).Append('\n');
    }

    /// <summary>
    /// Collects the set of all prefabs and widget types in the transitive dependency closure of the specified movie.
    /// </summary>
    /// <param name="widgetFactory">The widget factory resolving prefabs and widgets.</param>
    /// <param name="movieName">The root movie or prefab name.</param>
    /// <returns>A tuple containing the set of referenced prefab names and sorted list of referenced widget type names.</returns>
    /// <remarks>
    /// Traverses nested prefabs, inherited prefabs, and item templates. Scoping assembly dependencies strictly to referenced
    /// widget types prevents unrelated module updates from invalidating compiled movie assemblies.
    /// </remarks>
    public static (HashSet<string> Prefabs, List<string> WidgetTypes) CollectClosureAndWidgetTypes(WidgetFactory widgetFactory, string movieName)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var widgetTypes = new HashSet<string>(StringComparer.Ordinal);
        VisitPrefab(widgetFactory, movieName, visited, widgetTypes);
        return (visited, [.. widgetTypes.OrderBy(x => x, StringComparer.Ordinal)]);
    }

    private static void VisitPrefab(WidgetFactory widgetFactory, string prefabName, HashSet<string> visited, HashSet<string> widgetTypes)
    {
        if (!widgetFactory.IsCustomTypeIncludingRegistered(prefabName) || !visited.Add(prefabName))
            return;

        if (widgetFactory.TryGetCustomTypeIncludingRegistered(prefabName, out var prefab) && prefab.RootTemplate is { } root)
            VisitTemplate(widgetFactory, root, visited, widgetTypes);
    }

    private static void VisitTemplate(WidgetFactory widgetFactory, WidgetTemplate template, HashSet<string> visited, HashSet<string> widgetTypes)
    {
        widgetTypes.Add(template.Type);
        VisitPrefab(widgetFactory, template.Type, visited, widgetTypes);

        if (template.GetExtensionData<ItemTemplateUsage>() is { } itemTemplateUsage)
        {
            if (itemTemplateUsage.DefaultItemTemplate is { } defaultItemTemplate)
                VisitTemplate(widgetFactory, defaultItemTemplate, visited, widgetTypes);
            if (itemTemplateUsage.FirstItemTemplate is { } firstItemTemplate)
                VisitTemplate(widgetFactory, firstItemTemplate, visited, widgetTypes);
            if (itemTemplateUsage.LastItemTemplate is { } lastItemTemplate)
                VisitTemplate(widgetFactory, lastItemTemplate, visited, widgetTypes);
        }

        for (var i = 0; i < template.ChildCount; i++)
            VisitTemplate(widgetFactory, template.GetChildAt(i), visited, widgetTypes);
    }

    private static string Mvid(Assembly assembly) => assembly.ManifestModule.ModuleVersionId.ToString("N");
}