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
/// Verifies indexed data source path resolution behavior (<c>{Items\0}</c>) across XML and compiled prefabs (UIX0028).
/// <para>
/// Pinpoints the intended deviation where compiled prefabs dynamically track element repositioning at the specified index,
/// whereas XML views maintain the original item instance until a full view refresh occurs.
/// </para>
/// </summary>
[NonParallelizable]
public class IndexedDataSourceTests
{
    private const string Movie = "IndexedDataSourceMovie";
    private const string AssemblyTag = "indexeddatasource0";

    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is IndexedOwnerVM;

        public bool IsOwnVariant(System.Reflection.Assembly variantAssembly) => variantAssembly.GetName().Name?.Contains(AssemblyTag) == true;
    }

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;
    private bool _registered;

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
        <Widget Id="First" DataSource="{Items\0}" HoveredCursorState="@Name" />
      </Children>
    </Widget>
  </Window>
</Prefab>
"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(IndexedDataSourceTests));
        TaleWorlds.GauntletUI.GamepadNavigation.GauntletGamepadNavigationManager.Initialize();
        _registered = false;
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    [Test]
    public void AnItemInsertedInFront_IsNotFollowed_Xml()
    {
        var (viewModel, first) = Open(compiled: false);

        viewModel.Items.Insert(0, new IndexedItemVM("inserted"));

        Assert.That(first.HoveredCursorState, Is.EqualTo("a"), "XML stays on the item that was first");
    }

    /// <summary>Verifies that compiled prefabs update bindings when an element is inserted at the tracked index.</summary>
    [Test]
    public void AnItemInsertedInFront_IsFollowed_Compiled()
    {
        var (viewModel, first) = Open(compiled: true);

        viewModel.Items.Insert(0, new IndexedItemVM("inserted"));
        Assert.That(first.HoveredCursorState, Is.EqualTo("inserted"));

        viewModel.Items.RemoveAt(0);
        Assert.That(first.HoveredCursorState, Is.EqualTo("a"));
    }

    [Test]
    public void AnItemReplacedInPlace_IsFollowed_Compiled()
    {
        var (viewModel, first) = Open(compiled: true);

        viewModel.Items[0] = new IndexedItemVM("replaced");

        Assert.That(first.HoveredCursorState, Is.EqualTo("replaced"));
    }

    /// <summary>Verifies that property changes on an element displaced from the tracked index do not trigger updates.</summary>
    [Test]
    public void AnItemThatLeftTheIndex_IsNoLongerListenedTo_Compiled()
    {
        var (viewModel, first) = Open(compiled: true);
        var old = viewModel.Items[0];

        viewModel.Items.Insert(0, new IndexedItemVM("inserted"));
        old.Name = "renamed";

        Assert.That(first.HoveredCursorState, Is.EqualTo("inserted"));
    }

    /// <summary>Verifies that replacing the entire list instance re-resolves the indexed binding in both XML and compiled prefabs.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AReplacedList_IsFollowed(bool compiled)
    {
        var (viewModel, first) = Open(compiled);

        viewModel.Items = [new IndexedItemVM("other list")];

        Assert.That(first.HoveredCursorState, Is.EqualTo("other list"));
    }

    private (IndexedOwnerVM ViewModel, Widget First) Open(bool compiled)
    {
        if (compiled && !_registered)
        {
            Register();
            _registered = true;
        }
        var viewModel = new IndexedOwnerVM();
        var movie = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, viewModel, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(movie is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");
        var first = movie.RootWidget.FindChild("First", includeAllChildren: true);
        Assert.That(first, Is.Not.Null);
        Assert.That(first!.HoveredCursorState, Is.EqualTo("a"));
        return (viewModel, first);
    }

    private void Register()
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(IndexedOwnerVM));
        var sources = _workspace.Generate(Movie, typeof(IndexedOwnerVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(IndexedOwnerVM), AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class IndexedOwnerVM : ViewModel
{
    private MBBindingList<IndexedItemVM> _items = [new("a"), new("b")];

    [DataSourceProperty]
    public MBBindingList<IndexedItemVM> Items
    {
        get => _items;
        set
        {
            if (value != _items)
            {
                _items = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}

public sealed class IndexedItemVM(string name) : ViewModel
{
    private string _name = name;

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        set
        {
            if (value != _name)
            {
                _name = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}
