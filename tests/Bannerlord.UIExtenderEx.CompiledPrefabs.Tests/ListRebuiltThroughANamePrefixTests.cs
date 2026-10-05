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
/// Verifies prefix-based binding path invalidation behavior across XML and compiled prefabs (UIX0027).
/// <para>
/// Pinpoints the intended deviation where replacing property <c>Player</c> causes GauntletUI XML views to erroneously
/// rebuild lists at <c>PlayerActions</c> via string prefix matching (<c>BindingPath.IsRelatedWithPathAsString</c>),
/// whereas compiled prefabs isolate discrete property paths and preserve existing list item widgets.
/// </para>
/// </summary>
[NonParallelizable]
public class ListRebuiltThroughANamePrefixTests
{
    private const string Movie = "NamePrefixListMovie";
    private const string AssemblyTag = "nameprefixlist0";

    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is NamePrefixOwnerVM;

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
        _workspace = new PrefabWorkspace((Movie, """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget DataSource="{Player}" HoveredCursorState="@Name" />
        <ListPanel Id="List" DataSource="{PlayerActions}">
          <ItemTemplate><Widget HoveredCursorState="@Name" /></ItemTemplate>
        </ListPanel>
      </Children>
    </Widget>
  </Window>
</Prefab>
"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(ListRebuiltThroughANamePrefixTests));
        TaleWorlds.GauntletUI.GamepadNavigation.GauntletGamepadNavigationManager.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    [Test]
    public void ReplacingPlayer_RebuildsTheItemsOfPlayerActions_Xml()
    {
        var (viewModel, list) = Open(compiled: false);
        var item = list.GetChild(0);

        viewModel.Player = new NamePrefixItemVM("second player");

        Assert.That(list.ChildCount, Is.EqualTo(1));
        Assert.That(list.GetChild(0), Is.Not.SameAs(item), "the item was built again");
        Assert.That(list.GetChild(0).HoveredCursorState, Is.EqualTo("action"));
    }

    /// <summary>Verifies that compiled prefabs preserve list child widgets when an unrelated property sharing a prefix changes.</summary>
    [Test]
    public void ReplacingPlayer_LeavesTheItemsOfPlayerActions_Compiled()
    {
        Register();
        var (viewModel, list) = Open(compiled: true);
        var item = list.GetChild(0);

        viewModel.Player = new NamePrefixItemVM("second player");

        Assert.That(list.ChildCount, Is.EqualTo(1));
        Assert.That(list.GetChild(0), Is.SameAs(item), "the item is the widget it was");
        Assert.That(list.GetChild(0).HoveredCursorState, Is.EqualTo("action"));
    }

    private (NamePrefixOwnerVM ViewModel, Widget List) Open(bool compiled)
    {
        var viewModel = new NamePrefixOwnerVM();
        viewModel.PlayerActions.Add(new NamePrefixItemVM("action"));
        var movie = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, viewModel, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(movie is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");
        var list = movie.RootWidget.FindChild("List", includeAllChildren: true);
        Assert.That(list, Is.Not.Null);
        Assert.That(list!.ChildCount, Is.EqualTo(1));
        return (viewModel, list);
    }

    private void Register()
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(NamePrefixOwnerVM));
        var sources = _workspace.Generate(Movie, typeof(NamePrefixOwnerVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(NamePrefixOwnerVM), AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class NamePrefixOwnerVM : ViewModel
{
    private NamePrefixItemVM _player = new("player");

    [DataSourceProperty]
    public NamePrefixItemVM Player
    {
        get => _player;
        set
        {
            if (value != _player)
            {
                _player = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<NamePrefixItemVM> PlayerActions { get; } = [];
}

public sealed class NamePrefixItemVM(string name) : ViewModel
{
    [DataSourceProperty]
    public string Name { get; } = name;
}
