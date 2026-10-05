using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Runtimes;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GamePrefabs;

/// <summary>
/// Preserves native pre-compiled Gauntlet prefab variants for unmodified movies.
/// <para>
/// When a movie and its embedded dependency prefabs have not been patched or overridden by loaded modules,
/// this runtime serves the pre-compiled widget tree shipped with the base game.
/// Validates naming conventions via <see cref="ConventionsHold"/> to ensure dependency integrity before serving variants.
/// </para>
/// </summary>
public sealed class GamePrefabRuntime : IPrefabRuntime
{
    public static GamePrefabRuntime Instance { get; } = new();

    private static readonly Harmony Harmony = new("bannerlord.uiextender.ex.gameprefabs");
    private static readonly object InstallLock = new();
    private static bool _installed;

    private static readonly ConcurrentDictionary<Type, Type[]> _widgetChildCache = new();
    private static readonly ConcurrentDictionary<Type, string[]> _autoGenNameCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, Type?> _rootWidgetTypes = new();
    private static readonly AccessTools.FieldRef<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>? _generatedPrefabs =
        AccessTools2.FieldRefAccess<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>("_generatedPrefabs");

    /// <summary>
    /// Stores the evaluation result of <see cref="ConventionsHold"/> for a specific <see cref="GeneratedPrefabContext"/>.
    /// Entries are evicted upon context invalidation during resource reloads.
    /// </summary>
    private readonly ConditionalWeakTable<GeneratedPrefabContext, Verdict> _verdicts = new();
    private sealed record Verdict(bool Holds);
    private bool _collectionTracked;
    private bool _reported;

    /// <summary>Installs the runtime and hooks into <see cref="GeneratedPrefabContext.CollectPrefabs"/>. Idempotent.</summary>
    public static void Install()
    {
        lock (InstallLock)
        {
            if (_installed)
                return;
            _installed = true;
        }

        // Hook GeneratedPrefabContext.CollectPrefabs to invalidate cached convention verdicts across resource reloads.
        Instance._collectionTracked = Harmony.TryPatch(
            AccessTools2.DeclaredMethod(typeof(GeneratedPrefabContext), nameof(GeneratedPrefabContext.CollectPrefabs)),
            postfix: AccessTools2.DeclaredMethod(typeof(GamePrefabRuntime), nameof(CollectPrefabsPostfix)));

        PrefabRuntimes.Register(Instance);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectPrefabsPostfix(GeneratedPrefabContext __instance) => Instance._verdicts.Remove(__instance);

    public bool IsOwnVariant(Assembly variantAssembly) => false;

    public bool TryServe(WidgetFactory widgetFactory, string movieName, IViewModel? dataSource)
    {
        var context = widgetFactory.GeneratedPrefabContext;
        if (_generatedPrefabs?.Invoke(context) is not { } generatedPrefabs)
            return false;

        var variantName = dataSource != null ? dataSource.GetType().FullName : "Default";
        if (!generatedPrefabs.TryGetValue(movieName, out var variants) || !variants.TryGetValue(variantName, out var creator))
            return false;

        // Defer to subsequent runtimes if the variant was produced by a dynamic runtime compiler.
        if (creator.Method.DeclaringType?.Assembly is { } assembly && PrefabRuntimes.IsRuntimeVariant(assembly))
            return false;

        if (!ConventionsHold(widgetFactory))
            return false;

        var stopwatch = Stopwatch.StartNew();
        if (GetRootWidgetType(creator) is not { } rootWidgetType)
            return false;
        var moviesInvolved = new HashSet<string>(GetAutoGenNames(rootWidgetType));
        if (stopwatch.ElapsedMilliseconds >= 5)
            Trace.TraceInformation("UIExtenderEx: movie '{0}': walked the pre-compiled widget classes in {1} ms", movieName, stopwatch.ElapsedMilliseconds);

        return CanKeepRegisteredVariant(widgetFactory, moviesInvolved);
    }

    /// <summary>
    /// Validates that the naming conventions used to extract embedded prefab dependencies match native conventions.
    /// <para>
    /// Ensures all referenced autogen classes correspond to recognized prefabs within the active <see cref="WidgetFactory"/>.
    /// If conventions fail, pre-compiled variants are disabled and subsequent runtimes process movie instantiation.
    /// </para>
    /// </summary>
    public bool ConventionsHold(WidgetFactory widgetFactory)
    {
        if (!_collectionTracked)
            return Report(false, "GeneratedPrefabContext.CollectPrefabs could not be patched, so a verdict could not be kept current");

        return _verdicts.GetValue(widgetFactory.GeneratedPrefabContext, _ => new(Check(widgetFactory))).Holds;
    }

    private bool Check(WidgetFactory widgetFactory)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (_generatedPrefabs?.Invoke(widgetFactory.GeneratedPrefabContext) is not { } generatedPrefabs)
                return Report(false, "GeneratedPrefabContext._generatedPrefabs is unreachable");

            var known = new HashSet<string>(widgetFactory.GetPrefabNames().Select(PrefabNames.Normalize), StringComparer.Ordinal);
            var unresolved = new List<string>();
            var checkedVariants = 0;
            foreach (var movie in generatedPrefabs)
            {
                foreach (var variant in movie.Value)
                {
                    if (variant.Value.Method.DeclaringType?.Assembly is { } assembly && PrefabRuntimes.IsRuntimeVariant(assembly))
                        continue;

                    checkedVariants++;
                    if (GetRootWidgetType(variant.Value) is not { } root)
                    {
                        unresolved.Add($"{movie.Key} ({variant.Key}): no root widget class behind {variant.Value.Method.Name}");
                        continue;
                    }

                    var names = GetAutoGenNames(root);
                    if (!names.Contains(PrefabNames.Normalize(movie.Key)))
                        unresolved.Add($"{movie.Key} ({variant.Key}): root class {root.Name} does not name it");
                    unresolved.AddRange(names.Where(x => !known.Contains(x)).Select(x => $"{x} (in {movie.Key})"));
                }
            }

            if (unresolved.Count > 0)
            {
                var shown = unresolved.Distinct().ToList();
                return Report(false, $"{shown.Count} names read off {checkedVariants} pre-compiled variants are no prefab the game has, first: {string.Join(", ", shown.Take(10))}");
            }

            Trace.TraceInformation("UIExtenderEx: the naming of {0} pre-compiled variants checked in {1} ms", checkedVariants, stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception e)
        {
            return Report(false, $"checking the pre-compiled variants failed: {e}");
        }
    }

    /// <summary>Logs validation failures to the trace log once per failure mode.</summary>
    private bool Report(bool holds, string reason)
    {
        if (!holds && !_reported)
        {
            _reported = true;
            Trace.TraceWarning("UIExtenderEx: the game's pre-compiled prefabs are not used, every movie loads from XML or from UIExtenderEx's own builds: {0}", reason);
        }
        return holds;
    }

    /// <summary>
    /// Resolves the generated root widget class associated with a <see cref="CreateGeneratedWidget"/> delegate.
    /// </summary>
    public static Type? GetRootWidgetType(CreateGeneratedWidget creator) => _rootWidgetTypes.GetOrAdd(creator.Method, static method =>
    {
        const string create = "Create";
        var className = method.Name.StartsWith(create, StringComparison.Ordinal) ? method.Name.Substring(create.Length) : method.Name;
        if (method.DeclaringType is not { } declaringType)
            return AccessTools2.TypeByName(className);

        var inCreatorNamespace = declaringType.Namespace is { } ns ? declaringType.Assembly.GetType(ns + "." + className) : null;
        return inCreatorNamespace
            ?? declaringType.Assembly.GetTypes().FirstOrDefault(x => x.Name == className)
            ?? AccessTools2.TypeByName(className);
    });

    /// <summary>
    /// Discovers all prefab names inlined into the specified pre-compiled root widget hierarchy.
    /// <para>
    /// Combines static assembly type inspection for sibling dependency types (<c>_Dependency_</c>) with a depth-first
    /// traversal of child widget fields to discover cross-movie widget references while avoiding circular type references.
    /// </para>
    /// </summary>
    public static string[] GetAutoGenNames(Type rootWidgetType) => _autoGenNameCache.GetOrAdd(rootWidgetType, static root =>
    {
        var visited = new HashSet<Type> { root };
        var pending = new Stack<Type>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var widgetType = pending.Pop();
            var children = _widgetChildCache.GetOrAdd(widgetType, static x => [.. x.GetFields(AccessTools.all).Select(x => x.FieldType).Where(x => x.IsSubclassOf(typeof(Widget))).Distinct()]);
            foreach (var child in children)
            {
                if (visited.Add(child))
                    pending.Push(child);
            }
        }

        return
        [
            .. visited
                .Select(x => GetPrefabName(x.Name))
                .Concat(GetInlinedDependencyNames(root))
                .OfType<string>()
                .Distinct(),
        ];
    });

    /// <summary>
    /// Discovers inlined dependency prefab names generated beside <paramref name="root"/> within the declaring assembly.
    /// </summary>
    private static IEnumerable<string?> GetInlinedDependencyNames(Type root)
    {
        try
        {
            var prefix = root.Name + DependencyMarker;
            return
            [
                .. root.Assembly.GetTypes()
                    .Where(x => x.Name.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(x => GetPrefabName(x.Name)),
            ];
        }
        catch (Exception e)
        {
            // Fall back to field traversal if assembly type enumeration fails.
            Trace.TraceWarning("UIExtenderEx: could not list the prefabs inlined into '{0}': {1}", root.Name, e.Message);
            return [];
        }
    }

    private const string DependencyMarker = "_Dependency_";
    private static readonly string[] DependencySuffixes = ["__DependendPrefab", "__InheritedPrefab"];

    /// <summary>
    /// Extracts the normalized prefab name from a generated widget class name.
    /// </summary>
    public static string? GetPrefabName(string className)
    {
        foreach (var suffix in DependencySuffixes)
        {
            if (!className.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            var withoutSuffix = className.Substring(0, className.Length - suffix.Length);
            var marker = withoutSuffix.LastIndexOf(DependencyMarker, StringComparison.Ordinal);
            if (marker < 0)
                return null;

            var afterMarker = withoutSuffix.Substring(marker + DependencyMarker.Length);
            var underscore = afterMarker.IndexOf('_');
            return underscore < 0 || underscore == afterMarker.Length - 1 ? null : afterMarker.Substring(underscore + 1);
        }

        var variantSeparator = className.IndexOf("__", StringComparison.Ordinal);
        return variantSeparator <= 0 ? null : className.Substring(0, variantSeparator);
    }

    /// <summary>
    /// Evaluates whether a pre-compiled variant can be preserved.
    /// Returns <see langword="false"/> if any involved prefab is patched, overridden by a module, or registered dynamically.
    /// </summary>
    public static bool CanKeepRegisteredVariant(WidgetFactory widgetFactory, HashSet<string> moviesInvolved)
    {
        if (PrefabSource.PatchedPrefabNames.Any(x => moviesInvolved.Contains(PrefabNames.Normalize(x))))
            return false;

        // Invalidate variant if any inlined prefab has been overridden by a dynamic runtime registration.
        foreach (var registeredName in PrefabSource.RegisteredCustomTypeNames)
        {
            if (moviesInvolved.Contains(PrefabNames.Normalize(registeredName)))
                return false;
        }

        return !PrefabOverrideRegistry.ContainsOverridden(widgetFactory, moviesInvolved);
    }
}