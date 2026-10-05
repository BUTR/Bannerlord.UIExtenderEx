using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.Library;
using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Generates databinding infrastructure for a prefab class, mirroring the interaction between <c>GauntletView</c> and <c>ViewModel</c> in the XML loader.
/// <para>
/// Maintains data source fields for all referenced binding scopes (<see cref="DataSourceScope"/>) and emits reactive refresh callbacks triggered by property change notifications.
/// Subsystem responsibilities are divided across partial classes: scope hierarchies in <c>DatabindingEmitter.Scopes.cs</c>, list item templates in <c>DatabindingEmitter.Lists.cs</c>,
/// command bindings in <c>DatabindingEmitter.Commands.cs</c>, property writeback in <c>DatabindingEmitter.Properties.cs</c>, and mixin lookups in <c>DatabindingEmitter.Mixins.cs</c>.
/// </para>
/// </summary>
internal sealed partial class DatabindingEmitter
{
    /// <summary>Specifies the fully qualified runtime type invoked for dynamic member resolution when static analysis cannot resolve a member.</summary>
    private const string DynamicMemberType = "global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember";

    private readonly PrefabClass _class;

    /// <summary>Stores discovered data source scopes in discovery order, preserving deterministic traversal across code emitters.</summary>
    private readonly List<DataSourceScope> _scopes = [];

    private readonly Dictionary<BindingPath, DataSourceScope> _scopesByPath = new();

    public DatabindingEmitter(PrefabClass prefabClass)
    {
        _class = prefabClass;
    }

    private Type DataSourceType => _class.Options.DataSourceType
        ?? throw new InvalidOperationException($"Prefab '{_class.PrefabName}' was given no data source type to generate databinding against.");

    private DataSourceScope RootScope => Scope(new("Root"));

    private DataSourceScope Scope(BindingPath path) => TryGetScope(path)
        ?? throw new InvalidOperationException($"The data source path '{path.Path}' of prefab '{_class.PrefabName}' has no field; it was not collected as a scope.");

    private DataSourceScope? TryGetScope(BindingPath path) => _scopesByPath.TryGetValue(path, out var scope) ? scope : null;

    /// <summary>Resolves the static ViewModel type located at <paramref name="path"/> starting from the class root data source type.</summary>
    private Type? GetTypeAtPath(BindingPath path) => ViewModelPaths.GetTypeAtPath(DataSourceType, path);

    private IEnumerable<WidgetBinding> WidgetBindings => _class.Widgets.Select(x => x.Databinding).OfType<WidgetBinding>();

    public void GenerateInto(ClassCode classCode)
    {
        CollectScopes();
        AssignFieldNames();
        foreach (var binding in WidgetBindings)
        {
            var fullBindingPath = binding.FullBindingPath;
            if (TryGetScope(fullBindingPath) is not { } scope)
            {
                throw new InvalidOperationException($"No binding path target was collected for '{fullBindingPath.Path}'.");
            }
            scope.Widgets.Add(binding);
            if (binding.HandsItsPrefabTheScopeAround)
            {
                Scope(binding.PrefabDataSourcePath).PrefabHandoffs.Add(binding);
            }
        }
        AssignOuterHandoffs();
        // Resolve all scope types and binding paths across the prefab prior to code emission.
        ResolveScopes();
        AssignObjectFieldNames();
        CollectMixinReceivers();
        CreateMixinReceivers(classCode);
        if (_class.Kind == PrefabClassKind.Movie)
        {
            classCode.InheritedInterfaces.Add("global::TaleWorlds.GauntletUI.Data.IGeneratedGauntletMovieRoot");
            CreateRefreshBindingWithChildrenMethod(classCode);
        }
        CreateDestroyDataSourceMethod(classCode);
        CreateDataSourceFields(RootScope, classCode);
        foreach (var scope in _scopes.Where(x => x.IsOuter))
        {
            CreateDataSourceFields(scope, classCode);
        }
        CreateSetDataSourceMethod(classCode);
        CreateOuterDataSourceMethods(classCode);
        CreateEventMethods(classCode);
        CreateWidgetPropertyChangedMethods(classCode);
        CreateViewModelPropertyChangedMethods(classCode);
        CreateListChangedMethods(classCode);
        CreateHeldListChangedMethods(classCode);
        CreateRefreshDataSourceMethods(classCode);
    }

    public void AddExtrasToCreatorMethod(MethodCode methodCode)
    {
        methodCode.AddLine($"var movie = new global::TaleWorlds.GauntletUI.Data.GeneratedGauntletMovie({GeneratedLiteral.Regular(_class.PrefabName)}, widget);");
        methodCode.AddLine("var dataSource = data[\"DataSource\"];");
        methodCode.AddLine($"widget.SetDataSource(({ViewModelMemberResolution.GetCodeTypeName(DataSourceType)})dataSource);");
        methodCode.AddLine("result.AddData(\"Movie\", movie);");
    }

    /// <summary>
    /// Collects all active binding scopes across widgets, referenced prefab handoffs, and external outer paths.
    /// </summary>
    private void CollectScopes()
    {
        foreach (var binding in WidgetBindings)
        {
            AddScopeIfNew(binding.FullBindingPath);
            // Register surrounding scope handoffs supplied to child prefabs.
            if (binding.HandsItsPrefabTheScopeAround)
            {
                AddScopeIfNew(binding.PrefabDataSourcePath);
            }
        }
        foreach (var path in OuterPaths.Concat(OuterHandoffHostPaths()))
        {
            AddScopeIfNew(path);
        }
        // Iterate by index to dynamically incorporate newly discovered ancestor scopes.
        for (var i = 0; i < _scopes.Count; i++)
        {
            var scope = _scopes[i];
            // Outer scopes and root scopes do not have local parent scopes.
            if (scope.IsRoot || scope.IsOuter)
            {
                continue;
            }
            // A parent-scope binding whose scope is already the movie root simplifies to a path above the root ('' for one
            // hop, '..' for two), and BindingPath offers no parent for either. The XML loader cannot resolve such a path
            // either, so the movie is declined and stays on XML, saying which path it was.
            var parentPath = scope.Path.ParentPath
                ?? throw new InvalidOperationException(
                    $"The data source path '{scope.Path.Path}' of prefab '{_class.PrefabName}' has no parent scope"
                    + " - a parent-scope binding in a scope that is already the movie root - so its databinding cannot be generated.");
            AddScopeIfNew(parentPath).Children.Add(scope);
        }
        // Ensure the root scope is always registered for SetDataSource generation.
        AddScopeIfNew(new BindingPath("Root"));

        DataSourceScope AddScopeIfNew(BindingPath path)
        {
            if (TryGetScope(path) is { } existing)
            {
                return existing;
            }
            var scope = new DataSourceScope(path);
            _scopes.Add(scope);
            _scopesByPath.Add(path, scope);
            return scope;
        }
    }

    /// <summary>
    /// Assigns unique field names for each data source scope, disambiguating paths that would otherwise produce identical identifiers.
    /// </summary>
    private void AssignFieldNames()
    {
        var taken = _takenFieldNames;
        // Process the root scope first to guarantee deterministic assignment of _datasource_Root.
        foreach (var scope in _scopes.OrderBy(x => x.IsRoot ? 0 : 1))
        {
            var candidate = "_datasource_" + string.Join("_", scope.Path.Nodes.Select(GeneratedNaming.GetUsableName));
            scope.FieldName = GeneratedNaming.GetUniqueName(candidate, taken);
        }
    }

    private readonly HashSet<string> _takenFieldNames = new(StringComparer.Ordinal);

    /// <summary>Assigns unique untyped object backing field names for scopes requiring dynamic object tracking.</summary>
    private void AssignObjectFieldNames()
    {
        foreach (var scope in _scopes)
        {
            if (scope.Resolution.HoldsObject)
            {
                scope.ObjectFieldName = GeneratedNaming.GetUniqueName(scope.FieldName + "_object", _takenFieldNames);
            }
        }
    }

    /// <summary>
    /// Resolves the static type and binding strategy for every data source scope across the prefab.
    /// <para>
    /// Preserves statically resolved types, or marks unresolved scopes as dynamic lists or ViewModels based on widget usage (such as item templates).
    /// Emits sanitized C# type names via <see cref="ViewModelMemberResolution.GetCodeTypeName"/> to ensure generic arguments and nested classes compile correctly.
    /// </para>
    /// </summary>
    private void ResolveScopes()
    {
        foreach (var scope in _scopes)
        {
            var path = scope.Path;
            var usedAsList = scope.Widgets.Any(x => x.HasItemTemplateUsage || x.ItemsBuiltByUsedPrefab);
            if (GetTypeAtPath(path) is not { } declaredType)
            {
                // Identify scopes indexed by numeric keys as list collections.
                var indexedChildren = scope.Children.Count(x => IsIndex(x.Path.LastNode));
                if (indexedChildren > 0 && indexedChildren == scope.Children.Count)
                {
                    scope.Resolution = BindingPathResolution.UnresolvedList;
                    continue;
                }
                // Verify that unresolved paths do not traverse non-public members that are inaccessible to the Gauntlet XML loader.
                if (ViewModelPaths.FindUnusableStep(DataSourceType, path) is { } unusableStep)
                {
                    throw new InvalidOperationException(
                        $"The data source path '{path.Path}' of prefab '{_class.PrefabName}' goes through '{unusableStep}', which is declared"
                        + " but not publicly readable, so neither the XML loader nor generated databinding can walk it.");
                }

                // Disallow scopes simultaneously utilized as list collections and parent scopes with named children.
                if (usedAsList && scope.Children.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"The data source path '{path.Path}' of prefab '{_class.PrefabName}' is used both as a list and as the"
                        + " scope of a nested data source, and has no declared type to settle which it is, so its databinding cannot be generated.");
                }
                scope.Resolution = usedAsList ? BindingPathResolution.UnresolvedList : BindingPathResolution.UnresolvedViewModel;
            }
            else if (typeof(IMBBindingList).IsAssignableFrom(declaredType))
            {
                scope.Resolution = ViewModelPaths.GetElementType(declaredType) is { } elementType
                    ? BindingPathResolution.ForList(elementType)
                    // Treat lists with indeterminate element types as untyped list collections.
                    : BindingPathResolution.UnresolvedList;
            }
            else
            {
                scope.Resolution = BindingPathResolution.ForViewModel(declaredType);
            }
        }
    }

    /// <summary>Determines whether a binding path segment represents a numeric list index.</summary>
    private static bool IsIndex(string node) => node.Length > 0 && char.IsDigit(node[0]);

    private static void CreateDataSourceFields(DataSourceScope scope, ClassCode classCode)
    {
        classCode.AddVariable(new VariableCode
        {
            Name = scope.FieldName,
            AccessModifier = VariableCodeAccessModifier.Private,
            Type = scope.Resolution.EmittedTypeName,
        });
        if (scope.ObjectFieldName is { } objectField)
        {
            classCode.AddVariable(new VariableCode
            {
                Name = objectField,
                AccessModifier = VariableCodeAccessModifier.Private,
                Type = "global::System.Object",
            });
        }
        foreach (var child in scope.Children)
        {
            CreateDataSourceFields(child, classCode);
        }
    }

    private void CreateSetDataSourceMethod(ClassCode classCode)
    {
        var rootScope = RootScope;
        var methodCode = new MethodCode
        {
            Name = "SetDataSource",
            MethodSignature = $"({rootScope.Resolution.EmittedTypeName} dataSource)",
        };
        _class.MakeOverridable(methodCode, "base.SetDataSource(dataSource);");
        methodCode.AddLine($"RefreshDataSource{rootScope.FieldName}(dataSource);");
        classCode.AddMethod(methodCode);
    }

    private void CreateDestroyDataSourceMethod(ClassCode classCode)
    {
        var methodCode = new MethodCode { Name = "DestroyDataSource" };
        _class.MakeOverridable(methodCode, "base.DestroyDataSource();");
        AddCommandListeners(methodCode, add: false);
        FillClearSection(RootScope, methodCode, forDestroy: true);
        // Release external outer scopes provided by the host container during destruction.
        foreach (var scope in _scopes.Where(x => x.IsOuter))
        {
            FillClearSection(scope, methodCode, forDestroy: true);
        }
        classCode.AddMethod(methodCode);
    }

    private static void CreateRefreshBindingWithChildrenMethod(ClassCode classCode)
    {
        var methodCode = new MethodCode { Name = "RefreshBindingWithChildren" };
        methodCode.AddLine("var dataSource = _datasource_Root;");
        methodCode.AddLine("this.SetDataSource(null);");
        methodCode.AddLine("this.SetDataSource(dataSource);");
        classCode.AddMethod(methodCode);
    }
}