using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.Tests.Oracle;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Oracle;

/// <summary>
/// Verifies that releasing a movie unsubscribes all event handlers registered on ViewModels and binding lists across both XML and compiled movies,
/// asserting listener release detection in <see cref="BindingLoaderOracle"/>.
/// </summary>
[NonParallelizable]
public class ReleaseHandlerTests
{
    private const string Movie = "ReleaseProbeMovie";
    private const string AssemblyTag = "releaseprobe0000";

    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is ReleaseProbeVM;

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
    <Widget SuggestedWidth="@Width">
      <Children>
        <ListPanel DataSource="{Items}">
          <ItemTemplate><Widget SuggestedHeight="@Width" /></ItemTemplate>
        </ListPanel>
      </Children>
    </Widget>
  </Window>
</Prefab>
"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(ReleaseHandlerTests));
        // Initializes the gamepad navigation manager required during widget detachment when movies release.
        TaleWorlds.GauntletUI.GamepadNavigation.GauntletGamepadNavigationManager.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    [TestCase(false, TestName = "AnXmlMovie_TakesItsHandlersOffWhenReleased")]
    [TestCase(true, TestName = "ACompiledMovie_TakesItsHandlersOffWhenReleased")]
    public void AMovie_TakesItsHandlersOffWhenReleased(bool compiled)
    {
        if (compiled)
            Register();
        var viewModel = new ReleaseProbeVM();
        viewModel.Items.Add(new ReleaseProbeVM());
        var movie = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, viewModel, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(movie is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");

        var open = Handlers(viewModel);
        Assert.That(open.Keys, Has.Some.StartWith(@"Root\Items."), "the list's handler is counted");
        Assert.That(open.Keys, Has.Some.StartWith(@"Root\Items\0."), "an item's handlers are counted");

        movie.Release();
        Assert.That(Handlers(viewModel), Is.Empty);
    }

    private static Dictionary<string, int> Handlers(ReleaseProbeVM root) =>
        BindingLoaderOracle.HandlersOf(new (string, ViewModel?)[] { ("Root", root) }.Concat(root.Items.Select((x, i) => ($@"Root\Items\{i}", (ViewModel?) x))));

    private void Register()
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(ReleaseProbeVM));
        var sources = _workspace.Generate(Movie, typeof(ReleaseProbeVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(ReleaseProbeVM), AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class ReleaseProbeVM : ViewModel
{
    private int _width = 10;
    private MBBindingList<ReleaseProbeVM> _items = [];

    [DataSourceProperty]
    public int Width
    {
        get => _width;
        set
        {
            if (value != _width)
            {
                _width = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<ReleaseProbeVM> Items
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
