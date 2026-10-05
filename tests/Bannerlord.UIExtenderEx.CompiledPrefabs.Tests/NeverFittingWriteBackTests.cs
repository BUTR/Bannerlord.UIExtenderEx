using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.Tests.Oracle;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies write-back semantics when a widget announces a value type incompatible with the bound ViewModel property.
/// <para>
/// In GauntletUI XML, passing an unconverted widget value (such as a <see cref="Brush"/> into a string property) throws
/// an <see cref="ArgumentException"/> via reflection without updating the target value. Compiled prefabs intentionally
/// drop the write-back assignment rather than throwing at runtime (documented in <c>XmlDeviations.md</c>).
/// </para>
/// </summary>
[NonParallelizable]
public class NeverFittingWriteBackTests
{
    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
    }

    [TearDown]
    public void TearDown() => DynamicMember.Host = _previousHost;

    private static Widget XmlRoot(PrefabWorkspace workspace, TestUIContext ui, string movie, ViewModel viewModel)
    {
        var loaded = GauntletMovie.Load(ui.Context, workspace.WidgetFactory, movie, viewModel, doNotUseGeneratedPrefabs: true, hotReloadEnabled: false);
        Assert.That(loaded, Is.InstanceOf<GauntletMovie>(), "the XML path was not taken");
        return loaded.RootWidget;
    }

    private static Widget ById(Widget root, string id) => root.FindChild(id, includeAllChildren: true) ?? throw new InvalidOperationException($"No widget '{id}'.");

    private static Exception Innermost(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner })
            exception = inner;
        return exception;
    }

    private static void RegisterBrush(BrushFactory factory, string name)
    {
        var brushes = (Dictionary<string, Brush>) typeof(BrushFactory).GetField("_brushes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(factory)!;
        brushes[name] = new Brush { Name = name };
    }

    private const string BrushMovie = "NeverFittingBrushMovie";
    private const string BrushPrefab = """<Prefab><Window><Widget><Children><BrushWidget Id="Target" Brush="@BrushName" /></Children></Widget></Window></Prefab>""";

    /// <summary>
    /// Verifies that in GauntletUI XML, setting a <see cref="Brush"/> on a widget bound to a string property throws an <see cref="ArgumentException"/>.
    /// </summary>
    [Test]
    public void ABrushSetOnTheWidget_ThrowsIntoAStringProperty_Xml()
    {
        using var workspace = new PrefabWorkspace((BrushMovie, BrushPrefab));
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(ABrushSetOnTheWidget_ThrowsIntoAStringProperty_Xml));
        var viewModel = new BrushNameVM();
        var widget = (BrushWidget) ById(XmlRoot(workspace, ui, BrushMovie, viewModel), "Target");

        var thrown = Assert.Catch(() => widget.Brush = new Brush { Name = "FromCode" });
        Assert.That(Innermost(thrown!), Is.InstanceOf<ArgumentException>());
        Assert.That(Innermost(thrown!).Message, Does.Contain("TaleWorlds.GauntletUI.Brush").And.Contain("System.String"));
        Assert.That(viewModel.BrushName, Is.EqualTo("none"), "the ViewModel kept its value");

        // Verifies that repeated updates continue to throw on subsequent property mutations.
        Assert.That(() => widget.Brush = new Brush { Name = "Again" }, Throws.Exception);
    }

    /// <summary>
    /// Verifies that updating the brush name on the ViewModel throws an <see cref="ArgumentException"/> in GauntletUI XML when resolving the brush.
    /// </summary>
    [Test]
    public void ABrushNameChangedOnTheViewModel_ThrowsOutOfItsSetter_Xml()
    {
        using var workspace = new PrefabWorkspace((BrushMovie, BrushPrefab));
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(ABrushNameChangedOnTheViewModel_ThrowsOutOfItsSetter_Xml));
        RegisterBrush(ui.BrushFactory, "NeverFittingBrush");
        var viewModel = new BrushNameVM();
        XmlRoot(workspace, ui, BrushMovie, viewModel);

        var thrown = Assert.Catch(() => viewModel.BrushName = "NeverFittingBrush");
        Assert.That(Innermost(thrown!), Is.InstanceOf<ArgumentException>());
        Assert.That(Innermost(thrown!).Message, Does.Contain("TaleWorlds.GauntletUI.Brush").And.Contain("System.String"));
        Assert.That(viewModel.BrushName, Is.EqualTo("NeverFittingBrush"));
    }

    /// <summary>
    /// Verifies that compiled prefabs omit incompatible write-backs to string properties without throwing exceptions.
    /// </summary>
    [Test]
    public void ABrushIntoAStringProperty_IsNotWrittenBack_Compiled()
    {
        using var workspace = new PrefabWorkspace((BrushMovie, BrushPrefab));
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(ABrushIntoAStringProperty_IsNotWrittenBack_Compiled));
        RegisterBrush(ui.BrushFactory, "NeverFittingBrush");
        var movie = CompiledMovie.Build(workspace, BrushMovie, typeof(BrushNameVM), ui);
        var viewModel = new BrushNameVM();
        movie.SetDataSource(viewModel);
        var widget = (BrushWidget) movie.ById("Target");

        Assert.That(() => widget.Brush = new Brush { Name = "FromCode" }, Throws.Nothing);
        Assert.That(viewModel.BrushName, Is.EqualTo("none"));
        Assert.That(() => viewModel.BrushName = "NeverFittingBrush", Throws.Nothing);
        Assert.That(viewModel.BrushName, Is.EqualTo("NeverFittingBrush"));
    }

    /// <summary>
    /// Verifies that an <see cref="object"/> property accepts written-back brush instances in both XML and compiled prefabs.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void ABrushIntoAnObjectProperty_IsWrittenBack(bool compiled)
    {
        const string movieName = "NeverFittingBrushObjectMovie";
        using var workspace = new PrefabWorkspace((movieName, """<Prefab><Window><Widget><Children><BrushWidget Id="Target" Brush="@Held" /></Children></Widget></Window></Prefab>"""));
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(ABrushIntoAnObjectProperty_IsWrittenBack));
        var viewModel = new BrushObjectVM();
        BrushWidget widget;
        if (compiled)
        {
            var movie = CompiledMovie.Build(workspace, movieName, typeof(BrushObjectVM), ui);
            movie.SetDataSource(viewModel);
            widget = (BrushWidget) movie.ById("Target");
        }
        else
        {
            widget = (BrushWidget) ById(XmlRoot(workspace, ui, movieName, viewModel), "Target");
        }

        var brush = new Brush { Name = "FromCode" };
        widget.Brush = brush;
        Assert.That(viewModel.Held, Is.SameAs(brush));
    }

    /// <summary>
    /// Verifies that the binding oracle accounts for intended deviation handling when widgets announce incompatible brush types.
    /// </summary>
    [Test]
    public void TheBindingOracle_MatchesPastIt()
    {
        const string movieName = "NeverFittingBrushOracleMovie";
        using var workspace = new PrefabWorkspace((movieName, """<Prefab><Window><BrushWidget Brush="@ImageID" /></Window></Prefab>"""));
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(TheBindingOracle_MatchesPastIt));
        // Seeds the root ViewModel and pre-registers test brushes across lifecycle rounds 0 through 2.
        for (var round = 0; round <= 2; round++)
            RegisterBrush(ui.BrushFactory, SynthValues.Get<string>(1, "ImageID", round));

        var result = new BindingLoaderOracle(workspace.WidgetFactory, ui.SpriteData, ui.BrushFactory, ui.Context, [typeof(LabelWidget).Assembly])
            .Run([movieName]).Single();

        Assert.That(result.Outcome, Is.EqualTo(OracleOutcome.Match), () => $"{result.Detail}{Environment.NewLine}{string.Join(Environment.NewLine, result.Differences.Take(10))}");
    }
}

public sealed class BrushNameVM : ViewModel
{
    private string _brushName = "none";

    [DataSourceProperty]
    public string BrushName
    {
        get => _brushName;
        set
        {
            if (value != _brushName)
            {
                _brushName = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}

public sealed class BrushObjectVM : ViewModel
{
    private object? _held;

    [DataSourceProperty]
    public object? Held
    {
        get => _held;
        set
        {
            if (value != _held)
            {
                _held = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}
