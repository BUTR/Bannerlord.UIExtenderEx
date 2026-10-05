using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Linq;

using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies the intended deviation where widgets detached from the visual tree suppress property write-backs,
/// comparing behaviors against the vanilla XML loader.
/// <para>
/// When a widget detaches (<c>EventManager.OnWidgetDisconnectedFromRoot</c>), the game resets its
/// <c>GamepadNavigationIndex</c> to -1. The XML loader executes write-back bindings during detachment, mutating
/// ViewModels of released screens or deleted list items. Compiled prefabs unsubscribe bindings prior to widget
/// disconnection, preventing inadvertent ViewModel mutation.
/// </para>
/// </summary>
[NonParallelizable]
public class ResetWrittenBackOnRemovalTests
{
    private const string Movie = "ResetWriteBackMovie";
    private const string AssemblyTag = "resetwriteback00";

    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is NavigationIndexVM;

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
    <ListPanel GamepadNavigationIndex="@Index">
      <Children>
        <ListPanel DataSource="{Items}">
          <ItemTemplate><Widget GamepadNavigationIndex="@Index" /></ItemTemplate>
        </ListPanel>
      </Children>
    </ListPanel>
  </Window>
</Prefab>
"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(ResetWrittenBackOnRemovalTests));
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
    public void ReleasingTheMovie_WritesTheResetIntoTheViewModels_Xml()
    {
        var (viewModel, movie) = Open(compiled: false);

        movie.Release();

        Assert.That(viewModel.Index, Is.EqualTo(-1), "the root's reset reached the root ViewModel");
        Assert.That(viewModel.Items[0].Index, Is.EqualTo(-1), "the item's reset reached the item ViewModel");
    }

    [Test]
    public void RemovingAListItem_WritesTheResetIntoTheItem_Xml()
    {
        var (viewModel, _) = Open(compiled: false);
        var item = viewModel.Items[0];

        viewModel.Items.RemoveAt(0);

        Assert.That(item.Index, Is.EqualTo(-1));
    }

    /// <summary>
    /// Verifies that releasing a compiled movie unsubscribes write-back bindings before widget disconnection,
    /// preventing stale write-backs to the ViewModel.
    /// </summary>
    [Test]
    public void ReleasingTheMovie_WritesNothing_Compiled()
    {
        Register();
        var (viewModel, movie) = Open(compiled: true);

        movie.Release();

        Assert.That(viewModel.Index, Is.EqualTo(7));
        Assert.That(viewModel.Items[0].Index, Is.EqualTo(3));
    }

    [Test]
    public void RemovingAListItem_WritesNothing_Compiled()
    {
        Register();
        var (viewModel, _) = Open(compiled: true);
        var item = viewModel.Items[0];

        viewModel.Items.RemoveAt(0);

        Assert.That(item.Index, Is.EqualTo(3));
    }

    private (NavigationIndexVM ViewModel, IGauntletMovie Movie) Open(bool compiled)
    {
        var viewModel = new NavigationIndexVM(7);
        viewModel.Items.Add(new NavigationIndexVM(3));
        var movie = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, viewModel, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(movie is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");
        Assert.That(movie.RootWidget.GamepadNavigationIndex, Is.EqualTo(7), "opening binds the index");
        return (viewModel, movie);
    }

    private void Register()
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(NavigationIndexVM));
        var sources = _workspace.Generate(Movie, typeof(NavigationIndexVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(NavigationIndexVM), AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class NavigationIndexVM(int index) : ViewModel
{
    private int _index = index;

    [DataSourceProperty]
    public int Index
    {
        get => _index;
        set
        {
            if (value != _index)
            {
                _index = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<NavigationIndexVM> Items { get; } = [];
}
