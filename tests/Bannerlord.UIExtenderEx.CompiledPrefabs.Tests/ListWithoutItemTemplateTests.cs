using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests error-handling parity for list data sources bound without an item template across XML and compiled prefabs.
/// <para>
/// Verifies that compiled prefabs replicate TaleWorlds runtime behavior when binding itemless list views, throwing
/// exceptions upon loading non-empty lists, adding items, or binding parent list scopes through item widgets.
/// </para>
/// </summary>
[NonParallelizable]
public class ListWithoutItemTemplateTests
{
    private const string TypedMovie = "ListWithoutTemplateMovie";
    private const string HeldMovie = "HeldListWithoutTemplateMovie";
    private const string AssemblyTag = "listwithouttemplate0";

    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName is TypedMovie or HeldMovie && dataSource is TemplatelessOwnerVM;

        public bool IsOwnVariant(Assembly variantAssembly) => variantAssembly.GetName().Name?.Contains(AssemblyTag) == true;
    }

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        Bannerlord.UIExtenderEx.Runtimes.PrefabRuntimes.Register(ProbeRuntime.Instance);
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        _workspace = new PrefabWorkspace(
            (TypedMovie, """<Prefab><Window><Widget><Children><Widget DataSource="{Rows}" /></Children></Widget></Window></Prefab>"""),
            (HeldMovie, """
<Prefab><Window><Widget><Children>
  <ListPanel DataSource="{Rows}">
    <ItemTemplate><Widget><Children><Widget DataSource="{..}" /></Children></Widget></ItemTemplate>
  </ListPanel>
</Children></Widget></Window></Prefab>
"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(ListWithoutItemTemplateTests));
        TaleWorlds.GauntletUI.GamepadNavigation.GauntletGamepadNavigationManager.Initialize();
        // Reset registration flag because each test initializes an isolated factory instance.
        _registered = false;
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AListWithItems_KeepsTheMovieFromOpening(bool compiled)
    {
        var viewModel = new TemplatelessOwnerVM();
        viewModel.Rows.Add(new TemplatelessItemVM());

        Assert.That(Failure(() => Load(TypedMovie, viewModel, compiled), compiled), Is.EqualTo(nameof(NullReferenceException)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AnEmptyList_Opens_AndFailsOnItsFirstItem(bool compiled)
    {
        var viewModel = new TemplatelessOwnerVM();
        Load(TypedMovie, viewModel, compiled);

        Assert.That(Failure(() => viewModel.Rows.Add(new TemplatelessItemVM()), compiled), Is.EqualTo(nameof(NullReferenceException)));
        Assert.That(viewModel.Rows, Has.Count.EqualTo(1), "the item is in the list; the throw came after");
        Assert.That(Failure(() => viewModel.Rows.RemoveAt(0), compiled), Is.EqualTo(nameof(ArgumentOutOfRangeException)));
        Assert.That(Failure(() => viewModel.Rows.Clear(), compiled), Is.Null, "a reset touches no item");
    }

    /// <summary>Verifies that navigating back to a parent list without an item template via <c>{..}</c> prevents movie instantiation.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AListReachedFromItsOwnItem_KeepsTheMovieFromOpening(bool compiled)
    {
        var viewModel = new TemplatelessOwnerVM();
        viewModel.Rows.Add(new TemplatelessItemVM());

        Assert.That(Failure(() => Load(HeldMovie, viewModel, compiled), compiled), Is.EqualTo(nameof(NullReferenceException)));
    }

    /// <summary>
    /// Executes <paramref name="action"/> and returns the exception type name, verifying stack traces for compiled prefab origin.
    /// </summary>
    private static string? Failure(Action action, bool compiled)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception e)
        {
            while (e is TargetInvocationException { InnerException: { } inner })
                e = inner;
            if (compiled)
                Assert.That(e.StackTrace, Does.Match("RefreshDataSource_datasource_|OnList_datasource_|OnHeldList_datasource_"), "thrown by the compiled prefab");
            return e.GetType().Name;
        }
    }

    private bool _registered;

    private void Load(string movie, TemplatelessOwnerVM viewModel, bool compiled)
    {
        if (compiled && !_registered)
        {
            Register();
            _registered = true;
        }
        var loaded = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, movie, viewModel, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(loaded is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");
    }

    private void Register()
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(TemplatelessOwnerVM));
        // Process both movies in a single generator pass to share the registration entry point.
        System.Collections.Generic.List<GeneratedSource> sources;
        using (Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets.WidgetFactoryLookup.PrefabLease.Begin(factory))
        {
            var generator = new Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.PrefabCodeGenerator("Bannerlord.UIExtenderEx.Tests.Generated", factory, null!, null!);
            generator.AddMovie(TypedMovie, typeof(TemplatelessOwnerVM).FullName, typeof(TemplatelessOwnerVM));
            generator.AddMovie(HeldMovie, typeof(TemplatelessOwnerVM).FullName, typeof(TemplatelessOwnerVM));
            sources = [.. generator.GenerateInMemory().Select(x => new GeneratedSource(x.Key, x.Value))];
        }
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(TypedMovie, typeof(TemplatelessOwnerVM), AssemblyTag),
            [.. sources, IgnoresAccessChecksSource.Create(references)], references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class TemplatelessOwnerVM : ViewModel
{
    [DataSourceProperty]
    public MBBindingList<TemplatelessItemVM> Rows { get; } = [];
}

public sealed class TemplatelessItemVM : ViewModel;
