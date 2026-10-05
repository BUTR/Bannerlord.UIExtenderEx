using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.Library;
using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Represents an outer data source handoff passed across class boundaries to an instantiated prefab or list item template.
/// </summary>
internal sealed record OuterHandoff(WidgetBinding Binding, PrefabClass Class, BindingPath ClassPath, bool IsItem);

/// <summary>
/// Emits coordination mechanisms for outer data source scopes (<see cref="OuterPath"/>) ascending above class boundaries.
/// <para>
/// Manages outer data sources received from host containers as well as those propagated down to child prefabs and list items.
/// Maintains scopes for outer paths, propagating updates when host data sources change and resetting them upon component destruction.
/// </para>
/// </summary>
internal sealed partial class DatabindingEmitter
{
    private const string SetOuterDataSourceName = "SetOuterDataSource";

    private const string ClearOuterDataSourcesName = "ClearOuterDataSources";

    private readonly List<BindingPath> _outerPaths = [];

    /// <summary>Gets the list of external outer paths supplied to this class from its enclosing host, calculated via <see cref="SettleOuterPaths"/> prior to code generation.</summary>
    public IReadOnlyList<BindingPath> OuterPaths => _outerPaths;

    public bool HasOuterPaths => _outerPaths.Count > 0;

    /// <summary>
    /// Tracks the maximum depth below the movie root across all instances of this class (<see cref="OuterPath.Depth"/>),
    /// or <see langword="null"/> if uninstantiated. Computed by <see cref="SettleRootDepths"/>.
    /// </summary>
    private int? _deepestRoot;

    /// <summary>Indicates whether this class recurses indefinitely below the movie root without a bounded maximum depth.</summary>
    private bool _rootHasNoDeepest;

    /// <summary>Stores the maximum number of ascending scope levels reachable without traversing recursive cycles.</summary>
    private int _levelsWithoutARepeat;

    /// <summary>
    /// Enumerates all binding paths referenced by <paramref name="binding"/>, including widget properties, prefab handoffs, and command targets.
    /// </summary>
    private static IEnumerable<BindingPath> OwnPathsOf(WidgetBinding binding)
    {
        yield return binding.FullBindingPath;
        if (binding.HandsItsPrefabTheScopeAround)
        {
            yield return binding.PrefabDataSourcePath;
        }
        foreach (var commandBinding in binding.CommandBindings.Values)
        {
            yield return binding.GetCommandOwnerPath(commandBinding);
        }
    }

    /// <summary>
    /// Iteratively registers required outer paths from local widgets and child classes until fixpoint convergence.
    /// <para>
    /// Returns <see langword="true"/> if new outer paths were discovered, or <see langword="false"/> once saturated.
    /// Omits paths that invariably evaluate to null across all runtime instances (<see cref="OuterPath.NullFrom"/>).
    /// </para>
    /// </summary>
    public bool SettleOuterPaths()
    {
        var before = _outerPaths.Count;
        foreach (var binding in WidgetBindings)
        {
            foreach (var path in OwnPathsOf(binding))
            {
                AddOuterPath(path);
            }
            foreach (var handoff in OuterHandoffsOf(binding))
            {
                if (OuterPath.IsOuter(handoff.HostPath) && !IsNullAtEveryInstance(OuterPath.Parse(handoff.HostPath).Levels))
                {
                    AddOuterPath(handoff.HostPath);
                }
            }
        }
        return _outerPaths.Count != before;

        void AddOuterPath(BindingPath path)
        {
            if (OuterPath.IsOuter(path) && !_outerPaths.Contains(path))
            {
                _outerPaths.Add(path);
            }
        }
    }

    /// <summary>
    /// Determines whether an outer path ascending <paramref name="levels"/> above the class root invariably evaluates to null across all instances.
    /// </summary>
    private bool IsNullAtEveryInstance(int levels)
    {
        if (_rootHasNoDeepest)
        {
            if (levels > _levelsWithoutARepeat)
            {
                throw new InvalidOperationException(
                    $"Prefab '{_class.PrefabName ?? _class.Classes.MovieName}' is used inside itself both ever deeper and ever higher above the movie's root, "
                    + $"and a data source of it climbs {levels} levels above its root; one generated class cannot hold a data source for every level, so its databinding cannot be generated.");
            }
            return false;
        }
        return _deepestRoot is not { } deepest || levels >= OuterPath.NullFrom(deepest);
    }

    /// <summary>
    /// Computes maximum instantiation depth below the movie root across all prefab classes and determines iteration bounds for outer path resolution.
    /// </summary>
    public static int SettleRootDepths(DatabindingEmitter movie, IReadOnlyList<DatabindingEmitter> emitters)
    {
        var byClass = emitters.ToDictionary(x => x._class);
        var edges = new List<(DatabindingEmitter Host, DatabindingEmitter Used, int Depth, int Nodes)>();
        foreach (var host in emitters)
        {
            host._deepestRoot = null;
            host._rootHasNoDeepest = false;
            foreach (var binding in host.WidgetBindings)
            {
                foreach (var (used, rootRaw, _) in binding.ClassesHandedADataSource())
                {
                    if (byClass.TryGetValue(used, out var usedEmitter))
                    {
                        edges.Add((host, usedEmitter, OuterPath.Depth(rootRaw), rootRaw.Nodes.Length));
                    }
                }
            }
        }

        // Calculate longest-path reachability across prefab instantiation graphs using iterative relaxation.
        movie._deepestRoot = 0;
        for (var round = 0; round < emitters.Count; round++)
        {
            var deeper = false;
            foreach (var (host, used, depth, _) in edges)
            {
                if (host._deepestRoot is { } hostDepth && (used._deepestRoot is not { } usedDepth || hostDepth + depth > usedDepth))
                {
                    used._deepestRoot = hostDepth + depth;
                    deeper = true;
                    if (round == emitters.Count - 1)
                    {
                        used._rootHasNoDeepest = true;
                    }
                }
            }
            if (!deeper)
            {
                break;
            }
        }
        // Propagate unbounded depth flags across child dependencies.
        for (var spreading = true; spreading;)
        {
            spreading = false;
            foreach (var (host, used, _, _) in edges)
            {
                if (host._rootHasNoDeepest && !used._rootHasNoDeepest)
                {
                    used._rootHasNoDeepest = true;
                    spreading = true;
                }
            }
        }

        // Compute upper bounds on outer path traversal to prevent infinite expansion in recursive prefabs.
        var ownOuterPaths = emitters.ToDictionary(x => x,
            x => x.WidgetBindings.SelectMany(OwnPathsOf).Where(OuterPath.IsOuter).Distinct().ToList());
        var mostOwnLevels = ownOuterPaths.Values.SelectMany(x => x).Select(x => OuterPath.Parse(x).Levels).DefaultIfEmpty(0).Max();
        var levelsWithoutARepeat = mostOwnLevels + edges.Sum(x => Math.Max(0, -x.Depth));
        var rests = (long) ownOuterPaths.Values.SelectMany(x => x).Select(x => string.Join("\\", OuterPath.Parse(x).Nodes)).Distinct().Count()
            * (1 + edges.Sum(x => x.Nodes));
        var most = 0L;
        foreach (var emitter in emitters)
        {
            emitter._levelsWithoutARepeat = levelsWithoutARepeat;
            var levels = emitter._rootHasNoDeepest ? levelsWithoutARepeat : emitter._deepestRoot is { } deepest ? Math.Max(0, OuterPath.NullFrom(deepest)) : 0;
            most += ownOuterPaths[emitter].Count + levels * rests;
        }
        return (int) Math.Min(most + 1, int.MaxValue);
    }

    /// <summary>Enumerates outer paths required by child classes under <paramref name="binding"/>, mapped to their corresponding host paths.</summary>
    private static IEnumerable<(OuterHandoff Handoff, BindingPath HostPath)> OuterHandoffsOf(WidgetBinding binding)
    {
        foreach (var (used, rootRaw, isItem) in binding.ClassesHandedADataSource())
        {
            foreach (var classPath in used.Databinding!.OuterPaths)
            {
                yield return (new OuterHandoff(binding, used, classPath, isItem), binding.ToHostPath(rootRaw, classPath));
            }
        }
    }

    /// <summary>Enumerates all host binding paths that must be retained to supply outer data sources to child prefabs.</summary>
    private IEnumerable<BindingPath> OuterHandoffHostPaths() =>
        WidgetBindings.SelectMany(OuterHandoffsOf).Select(x => x.HostPath);

    private void AssignOuterHandoffs()
    {
        foreach (var binding in WidgetBindings)
        {
            foreach (var (handoff, hostPath) in OuterHandoffsOf(binding))
            {
                Scope(hostPath).OuterHandoffs.Add(handoff);
            }
        }
    }

    /// <summary>
    /// Emits <c>SetOuterDataSource</c> and <c>ClearOuterDataSources</c> methods to handle reception and cleanup of external data sources.
    /// </summary>
    private void CreateOuterDataSourceMethods(ClassCode classCode)
    {
        if (!HasOuterPaths)
        {
            return;
        }
        var setMethod = new MethodCode
        {
            Name = SetOuterDataSourceName,
            MethodSignature = "(global::System.String path, global::System.Object dataSource)",
        };
        var clearMethod = new MethodCode { Name = ClearOuterDataSourcesName };
        MakeOuterMethodOverridable(setMethod);
        MakeOuterMethodOverridable(clearMethod);
        foreach (var scope in _scopes.Where(x => x.IsOuter))
        {
            // Skip duplicate notifications when data source references remain unchanged.
            setMethod.AddBlock($"if (path == {GeneratedLiteral.Regular(scope.Path.Path)})", () =>
            {
                setMethod.AddLine(scope.ObjectFieldName is not null
                    ? $"var newDataSource = {DynamicMemberType}.AsDataSource(dataSource);"
                    : $"var newDataSource = dataSource as {scope.Resolution.EmittedTypeName};");
                setMethod.AddLine($"if (!global::System.Object.ReferenceEquals(newDataSource, {scope.ObjectAccess})) RefreshDataSource{scope.FieldName}(newDataSource);");
            });
            clearMethod.AddLine($"RefreshDataSource{scope.FieldName}(null);");
        }
        classCode.AddMethod(setMethod);
        classCode.AddMethod(clearMethod);
    }

    /// <summary>
    /// Configures polymorphism modifiers on outer data source methods, marking them as overrides when inheriting from a base prefab or virtual when acting as a base prefab.
    /// </summary>
    private void MakeOuterMethodOverridable(MethodCode methodCode)
    {
        var baseClass = _class.InheritsAnotherPrefab ? _class.Widgets[0].UsedClass : null;
        if (baseClass?.Databinding is { HasOuterPaths: true })
        {
            methodCode.PolymorphismInfo = MethodCodePolymorphismInfo.Override;
        }
        else if (_class.Kind == PrefabClassKind.BasePrefab)
        {
            methodCode.PolymorphismInfo = MethodCodePolymorphismInfo.Virtual;
        }
    }

    /// <summary>
    /// Emits code propagating updated outer data source values (<paramref name="value"/>) to dependent child prefabs and list items.
    /// </summary>
    private static void AddOuterHandoffs(MethodCode methodCode, DataSourceScope scope, string value)
    {
        foreach (var handoff in scope.OuterHandoffs)
        {
            var call = $"{SetOuterDataSourceName}({GeneratedLiteral.Regular(handoff.ClassPath.Path)}, {value});";
            if (!handoff.IsItem)
            {
                methodCode.AddLine($"{(handoff.Binding.IsRoot ? "base" : handoff.Binding.Widget.VariableName)}.{call}");
                continue;
            }
            var list = handoff.Binding.Widget.VariableName;
            methodCode.AddBlock($"for (var i = 0; i < {list}.ChildCount; i++)", () =>
                methodCode.AddLine($"({list}.GetChild(i) as {handoff.Class.ClassName})?.{call}"));
        }
    }

    /// <summary>Emits statements passing all required outer data sources to a newly instantiated list item.</summary>
    private void AddItemOuterHandoffs(MethodCode methodCode, WidgetBinding list, PrefabClass itemClass, string item)
    {
        foreach (var (handoff, hostPath) in OuterHandoffsOf(list))
        {
            if (handoff.IsItem && handoff.Class == itemClass)
            {
                methodCode.AddLine($"{item}.{SetOuterDataSourceName}({GeneratedLiteral.Regular(handoff.ClassPath.Path)}, {Scope(hostPath).ObjectAccess});");
            }
        }
    }
}