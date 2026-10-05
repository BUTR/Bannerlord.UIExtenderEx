using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Linq;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests two-way alignment binding behaviors between widgets and ViewModels across XML and compiled prefabs.
/// <para>
/// Starting in v1.1.0, widgets announce alignment changes as string names while property setters expect enum instances.
/// In XML, only loosely typed <see cref="object"/> properties that manually parse strings support two-way bindings.
/// Compiled prefabs automatically parse alignment strings back into their target enum types as an intentional deviation.
/// </para>
/// </summary>
[NonParallelizable]
public class AlignmentBindingPatternTests
{
    private const string Movie = "AlignmentPatternMovie";
    private const string AssemblyTag = "alignpattern0000";

    /// <summary>
    /// Serves compiled test variants exclusively for alignment pattern test fixtures.
    /// </summary>
    private sealed class PatternRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly PatternRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is ParsingAlignmentVM or EnumAlignmentVM;

        public bool IsOwnVariant(System.Reflection.Assembly variantAssembly) => variantAssembly.GetName().Name?.Contains(AssemblyTag) == true;
    }

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        Bannerlord.UIExtenderEx.Runtimes.PrefabRuntimes.Register(PatternRuntime.Instance);
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        _workspace = new PrefabWorkspace((Movie, """<Prefab><Window><Widget Id="Target" VerticalAlignment="@Align" /></Window></Prefab>"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(AlignmentBindingPatternTests));
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    [TestCase(false, TestName = "AnObjectPropertyThatParsesTheName_WorksBothWays_Xml")]
    [TestCase(true, TestName = "AnObjectPropertyThatParsesTheName_WorksBothWays_Compiled")]
    public void AnObjectPropertyThatParsesTheName_WorksBothWays(bool compiled)
    {
        if (compiled)
            Register(typeof(ParsingAlignmentVM));
        var viewModel = new ParsingAlignmentVM { Align = VerticalAlignment.Bottom };
        var movie = Open(viewModel, compiled);
        var widget = movie.RootWidget;

        Assert.That(widget.VerticalAlignment, Is.EqualTo(VerticalAlignment.Bottom), "the ViewModel's value reached the widget");

        viewModel.Align = VerticalAlignment.Center;
        Assert.That(widget.VerticalAlignment, Is.EqualTo(VerticalAlignment.Center), "a change on the ViewModel reached the widget");
        Assert.That(viewModel.Align, Is.EqualTo(VerticalAlignment.Center), "the name written back was parsed");

        widget.VerticalAlignment = VerticalAlignment.Top;
        Assert.That(viewModel.Align, Is.EqualTo(VerticalAlignment.Top), "a change on the widget reached the ViewModel as the enum");
    }

    /// <summary>
    /// Verifies that compiled prefabs parse written-back alignment names into target enum properties.
    /// </summary>
    [Test]
    public void AnEnumProperty_WorksBothWays_Compiled()
    {
        Register(typeof(EnumAlignmentVM));
        var viewModel = new EnumAlignmentVM { Align = VerticalAlignment.Bottom };
        var widget = Open(viewModel, compiled: true).RootWidget;

        Assert.That(widget.VerticalAlignment, Is.EqualTo(VerticalAlignment.Bottom), "the ViewModel's value reached the widget");

        viewModel.Align = VerticalAlignment.Center;
        Assert.That(widget.VerticalAlignment, Is.EqualTo(VerticalAlignment.Center), "a change on the ViewModel reached the widget");
        Assert.That(viewModel.Align, Is.EqualTo(VerticalAlignment.Center));

        widget.VerticalAlignment = VerticalAlignment.Top;
        Assert.That(viewModel.Align, Is.EqualTo(VerticalAlignment.Top), "a change on the widget reached the ViewModel");
    }

    [Test]
    public void AnEnumProperty_ThrowsOnTheFirstChange_Xml()
    {
        var viewModel = new EnumAlignmentVM { Align = VerticalAlignment.Bottom };
        var widget = Open(viewModel, compiled: false).RootWidget;

        Assert.That(widget.VerticalAlignment, Is.EqualTo(VerticalAlignment.Bottom), "opening does not write back");
        var thrown = Assert.Catch(() => viewModel.Align = VerticalAlignment.Center);
        while (thrown is System.Reflection.TargetInvocationException { InnerException: { } inner })
            thrown = inner;
        Assert.That(thrown, Is.InstanceOf<ArgumentException>());
        // Verifies that field assignment succeeds before the write-back exception occurs.
        Assert.That(viewModel.Align, Is.EqualTo(VerticalAlignment.Center), "the ViewModel kept the new value");
        Assert.That(widget.VerticalAlignment, Is.EqualTo(VerticalAlignment.Center), "the widget took the new value");
    }

    private IGauntletMovie Open(ViewModel source, bool compiled)
    {
        var movie = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, source, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(movie is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");
        return movie;
    }

    private void Register(Type viewModelType)
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, viewModelType);
        var sources = _workspace.Generate(Movie, viewModelType).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, viewModelType, AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

/// <summary>Implements a ViewModel pattern that parses written-back alignment string names into enum values.</summary>
public sealed class ParsingAlignmentVM : ViewModel
{
    private object _align = VerticalAlignment.Top;

    [DataSourceProperty]
    public object Align
    {
        get => _align;
        set
        {
            var parsed = value is string name ? Enum.Parse(typeof(VerticalAlignment), name) : value;
            if (!Equals(parsed, _align))
            {
                _align = parsed;
                OnPropertyChangedWithValue(parsed);
            }
        }
    }
}

public sealed class EnumAlignmentVM : ViewModel
{
    private VerticalAlignment _align;

    [DataSourceProperty]
    public VerticalAlignment Align
    {
        get => _align;
        set
        {
            if (value != _align)
            {
                _align = value;
                // Invokes parameterless OnPropertyChanged because OnPropertyChangedWithValue requires reference types.
                OnPropertyChanged();
            }
        }
    }
}
