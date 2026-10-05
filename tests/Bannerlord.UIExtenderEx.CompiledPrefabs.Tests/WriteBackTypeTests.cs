using Bannerlord.UIExtenderEx.Attributes;
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
/// Verifies two-way binding write-back behavior when numeric types differ between widget properties and ViewModel setters.
/// <para>
/// In TaleWorlds GauntletUI XML loading, property write-backs pass unconverted announced values directly to reflection setters,
/// causing <see cref="ArgumentException"/> when a float widget property (e.g. <c>SuggestedWidth</c>) writes to an int ViewModel property.
/// Compiled prefabs cast announced values to the target type, supporting bidirectional updates as an intentional deviation.
/// </para>
/// </summary>
[NonParallelizable]
public class WriteBackTypeTests
{
    private const string Movie = "WidthProbeMovie";
    private const string AssemblyTag = "widthprobe000000";

    /// <summary>
    /// Serves compiled prefabs specifically for this test fixture, isolating movie requests to <see cref="IntWidthVM"/>.
    /// </summary>
    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is IntWidthVM;

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
        _workspace = new PrefabWorkspace((Movie, """<Prefab><Window><Widget SuggestedWidth="@Width" /></Window></Prefab>"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(WriteBackTypeTests));
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    [Test]
    public void AnIntBoundToAFloatWidgetProperty_ThrowsOnEveryChange_Xml()
    {
        var viewModel = new IntWidthVM { Width = 10 };
        var widget = Open(viewModel, compiled: false);

        Assert.That(widget.SuggestedWidth, Is.EqualTo(10f), "opening widens the int and does not write back");
        AssertThrowsFloatIntoInt(() => viewModel.Width = 20);
        AssertThrowsFloatIntoInt(() => widget.SuggestedWidth = 30);
    }

    /// <summary>
    /// Verifies the intentional deviation where compiled prefabs cast announced float values into integer ViewModel properties.
    /// </summary>
    [Test]
    public void AnIntBoundToAFloatWidgetProperty_WorksBothWays_Compiled()
    {
        Register();
        var viewModel = new IntWidthVM { Width = 10 };
        var widget = Open(viewModel, compiled: true);

        Assert.That(widget.SuggestedWidth, Is.EqualTo(10f));
        viewModel.Width = 20;
        Assert.That(widget.SuggestedWidth, Is.EqualTo(20f), "a change on the ViewModel reached the widget");
        Assert.That(viewModel.Width, Is.EqualTo(20));

        widget.SuggestedWidth = 30;
        Assert.That(viewModel.Width, Is.EqualTo(30), "a change on the widget reached the ViewModel");

        // Truncates floating-point values upon cast; the ViewModel synchronizes the integer value back to the widget.
        widget.SuggestedWidth = 41.7f;
        Assert.That(viewModel.Width, Is.EqualTo(41));
        Assert.That(widget.SuggestedWidth, Is.EqualTo(41f));
    }

    private static void AssertThrowsFloatIntoInt(TestDelegate change)
    {
        var thrown = Assert.Catch(change);
        Assert.That(thrown!.GetBaseException(), Is.InstanceOf<ArgumentException>());
        Assert.That(thrown.GetBaseException().Message, Does.Contain("System.Single").And.Contain("System.Int32"));
    }

    private Widget Open(ViewModel source, bool compiled)
    {
        var movie = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, source, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(movie is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");
        return movie.RootWidget;
    }

    private void Register()
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(IntWidthVM));
        var sources = _workspace.Generate(Movie, typeof(IntWidthVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(IntWidthVM), AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class IntWidthVM : ViewModel
{
    private int _width;

    [DataSourceProperty]
    public int Width
    {
        get => _width;
        set
        {
            if (value != _width)
            {
                _width = value;
                OnPropertyChanged();
            }
        }
    }
}
