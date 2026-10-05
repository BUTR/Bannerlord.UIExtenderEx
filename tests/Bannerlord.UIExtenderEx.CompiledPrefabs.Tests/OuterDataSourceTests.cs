using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Linq;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies binding resolution for outer parent scope data sources (<c>{..}</c>) when an intermediate child data source evaluates to <see langword="null"/>.
/// <para>
/// Ensures both XML and compiled prefabs preserve access to outer data sources when child scope sources are reassigned or cleared,
/// maintaining binding updates and clean handler disconnection on movie release.
/// </para>
/// </summary>
[NonParallelizable]
public class OuterDataSourceTests
{
    private const string Movie = "OuterDataSourceMovie";
    private const string AssemblyTag = "outerdatasource0";

    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is OuterOwnerVM;

        public bool IsOwnVariant(System.Reflection.Assembly variantAssembly) => variantAssembly.GetName().Name?.Contains(AssemblyTag) == true;
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
        // Binds IsVisible directly on the instance so that OuterCard receives Selected and evaluates {..} relative to outer scope.
        _workspace = new PrefabWorkspace(
            (Movie, """<Prefab><Window><Widget><Children><OuterCard DataSource="{Selected}" IsVisible="@IsShown" /></Children></Widget></Window></Prefab>"""),
            ("OuterCard", """<Prefab><Window><Widget><Children><Widget Id="Title" DataSource="{..}" HoveredCursorState="@Title" /></Children></Widget></Window></Prefab>"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(OuterDataSourceTests));
        TaleWorlds.GauntletUI.GamepadNavigation.GauntletGamepadNavigationManager.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    /// <summary>
    /// Verifies that bindings targeting outer parent scopes continue receiving updates when the local child data source is <see langword="null"/>.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AboveTheRoot_IsFollowedWhileThePrefabsOwnDataSourceIsNull(bool compiled)
    {
        var (viewModel, title) = Open(compiled);

        viewModel.Selected = null;
        viewModel.Title = "while nothing is selected";
        Assert.That(title.HoveredCursorState, Is.EqualTo("while nothing is selected"));

        viewModel.Selected = new OuterSelectedVM();
        viewModel.Title = "selected again";
        Assert.That(title.HoveredCursorState, Is.EqualTo("selected again"));
    }

    /// <summary>
    /// Verifies that releasing the movie unregisters all event handlers from the outer data source.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AboveTheRoot_IsLetGoOfWhenTheMovieIsReleased(bool compiled)
    {
        var viewModel = new OuterOwnerVM();
        var movie = Load(viewModel, compiled);

        movie.Release();

        Assert.That(Bannerlord.UIExtenderEx.Tests.Oracle.BindingLoaderOracle.HandlersOf([("Root", viewModel)]), Is.Empty);
    }

    private (OuterOwnerVM ViewModel, Widget Title) Open(bool compiled)
    {
        var viewModel = new OuterOwnerVM();
        var movie = Load(viewModel, compiled);
        var title = movie.RootWidget.FindChild("Title", includeAllChildren: true);
        Assert.That(title, Is.Not.Null);
        Assert.That(title!.HoveredCursorState, Is.EqualTo("title"));
        return (viewModel, title);
    }

    private IGauntletMovie Load(OuterOwnerVM viewModel, bool compiled)
    {
        if (compiled)
            Register();
        var movie = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, viewModel, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(movie is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");
        return movie;
    }

    private void Register()
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(OuterOwnerVM));
        var sources = _workspace.Generate(Movie, typeof(OuterOwnerVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(OuterOwnerVM), AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class OuterOwnerVM : ViewModel
{
    private string _title = "title";
    private OuterSelectedVM? _selected = new();

    [DataSourceProperty]
    public string Title
    {
        get => _title;
        set
        {
            if (value != _title)
            {
                _title = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public OuterSelectedVM? Selected
    {
        get => _selected;
        set
        {
            if (value != _selected)
            {
                _selected = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}

public sealed class OuterSelectedVM : ViewModel
{
    [DataSourceProperty]
    public bool IsShown => true;
}
