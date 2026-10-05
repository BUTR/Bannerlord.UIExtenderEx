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
/// Verifies parent scope (<c>{..\Items}</c>) list binding stability across XML and compiled prefabs (UIX0027).
/// <para>
/// Pinpoints the intended deviation where replacing child ViewModel <c>Child</c> causes GauntletUI XML views to re-instantiate
/// child list items bound via parent-scope references, whereas compiled prefabs detect unchanged parent list data and preserve
/// existing item widgets.
/// </para>
/// </summary>
[NonParallelizable]
public class ListRebuiltThroughParentScopeTests
{
    private const string Movie = "ParentScopeListMovie";
    private const string AssemblyTag = "parentscopelist0";

    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is ParentScopeOwnerVM;

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
        <Widget DataSource="{Child}" HoveredCursorState="@Name">
          <Children>
            <ListPanel Id="List" DataSource="{..\Items}">
              <ItemTemplate><Widget HoveredCursorState="@Name" /></ItemTemplate>
            </ListPanel>
          </Children>
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>
"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(ListRebuiltThroughParentScopeTests));
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
    public void ReplacingTheChild_RebuildsTheItemsOfTheOwnersList_Xml()
    {
        var (viewModel, list) = Open(compiled: false);
        var item = list.GetChild(0);

        viewModel.Child = new ParentScopeItemVM("second child");

        Assert.That(list.ChildCount, Is.EqualTo(1));
        Assert.That(list.GetChild(0), Is.Not.SameAs(item), "the item was built again");
        Assert.That(list.GetChild(0).HoveredCursorState, Is.EqualTo("item"));
    }

    /// <summary>Verifies that compiled prefabs preserve list item widgets bound via parent scope when the immediate child scope changes.</summary>
    [Test]
    public void ReplacingTheChild_LeavesTheItemsOfTheOwnersList_Compiled()
    {
        Register();
        var (viewModel, list) = Open(compiled: true);
        var item = list.GetChild(0);

        viewModel.Child = new ParentScopeItemVM("second child");

        Assert.That(list.ChildCount, Is.EqualTo(1));
        Assert.That(list.GetChild(0), Is.SameAs(item), "the item is the widget it was");
        Assert.That(list.GetChild(0).HoveredCursorState, Is.EqualTo("item"));
    }

    private (ParentScopeOwnerVM ViewModel, Widget List) Open(bool compiled)
    {
        var viewModel = new ParentScopeOwnerVM();
        viewModel.Items.Add(new ParentScopeItemVM("item"));
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
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(ParentScopeOwnerVM));
        var sources = _workspace.Generate(Movie, typeof(ParentScopeOwnerVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(ParentScopeOwnerVM), AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class ParentScopeOwnerVM : ViewModel
{
    private ParentScopeItemVM _child = new("child");

    [DataSourceProperty]
    public ParentScopeItemVM Child
    {
        get => _child;
        set
        {
            if (value != _child)
            {
                _child = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<ParentScopeItemVM> Items { get; } = [];
}

public sealed class ParentScopeItemVM(string name) : ViewModel
{
    [DataSourceProperty]
    public string Name { get; } = name;
}
