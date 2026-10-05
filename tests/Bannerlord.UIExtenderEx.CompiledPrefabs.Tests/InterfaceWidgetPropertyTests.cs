using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies binding compatibility between interface-typed widget properties and unsealed class-typed ViewModel properties.
/// <para>
/// Ensures compiled prefabs replicate XML loader behavior by delegating to runtime reflection assignment when the declared
/// class does not implement the interface directly but a derived runtime subclass does.
/// </para>
/// </summary>
[NonParallelizable]
public class InterfaceWidgetPropertyTests
{
    private const string Movie = "InterfaceShapeMovie";

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        _workspace = new PrefabWorkspace((Movie, """<Prefab><Window><Widget><Children><ShapeHoldingWidget Id="Target" Shape="@Shape" /></Children></Widget></Window></Prefab>"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(InterfaceWidgetPropertyTests));
    }

    [TearDown]
    public void TearDown()
    {
        _ui?.Dispose();
        _workspace?.Dispose();
        DynamicMember.Host = _previousHost;
    }

    private static Widget ById(Widget root, string id) => root.FindChild(id, includeAllChildren: true) ?? throw new InvalidOperationException($"No widget '{id}'.");

    [Test]
    public void ANonSealedClassHoldingAnImplementation_IsAssignedWhenTheMovieOpens_AsTheXmlLoaderDoes()
    {
        var xmlViewModel = new ShapeHolderVM();
        var loaded = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, xmlViewModel, doNotUseGeneratedPrefabs: true, hotReloadEnabled: false);
        Assert.That(loaded, Is.InstanceOf<GauntletMovie>(), "the XML path was not taken");
        var xmlWidget = (ShapeHoldingWidget) ById(loaded.RootWidget, "Target");
        var compiled = CompiledMovie.Build(_workspace, Movie, typeof(ShapeHolderVM), _ui);
        var compiledViewModel = new ShapeHolderVM();
        compiled.SetDataSource(compiledViewModel);
        var compiledWidget = (ShapeHoldingWidget) compiled.ById("Target");

        Assert.That(xmlWidget.Shape, Is.SameAs(xmlViewModel.Shape), "XML: handed to the setter as it is");
        Assert.That(compiledWidget.Shape, Is.SameAs(compiledViewModel.Shape));

        var next = new RoundShape();
        xmlViewModel.Shape = next;
        compiledViewModel.Shape = next;
        Assert.That(xmlWidget.Shape, Is.SameAs(next));
        Assert.That(compiledWidget.Shape, Is.SameAs(next));
    }

    /// <summary>Verifies that assigning an instance that does not implement the required interface throws an exception in both XML and compiled prefabs.</summary>
    [Test]
    public void ANonSealedClassHoldingNoImplementation_ThrowsAsTheXmlLoaderDoes()
    {
        var compiled = CompiledMovie.Build(_workspace, Movie, typeof(ShapeHolderVM), _ui);
        Assert.That(() => GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, new ShapeHolderVM(new ShapeBase()), doNotUseGeneratedPrefabs: true, hotReloadEnabled: false),
            Throws.Exception, "XML");
        Assert.That(() => compiled.SetDataSource(new ShapeHolderVM(new ShapeBase())), Throws.Exception, "compiled");
    }
}

public interface IShape { }

/// <summary>Represents an unsealed base class that does not implement <see cref="IShape"/> directly.</summary>
public class ShapeBase { }

public sealed class RoundShape : ShapeBase, IShape { }

public class ShapeHoldingWidget : Widget
{
    public ShapeHoldingWidget(UIContext context) : base(context) { }

    public IShape? Shape { get; set; }
}

public sealed class ShapeHolderVM : ViewModel
{
    private ShapeBase _shape;

    public ShapeHolderVM() : this(new RoundShape()) { }

    public ShapeHolderVM(ShapeBase shape) => _shape = shape;

    [DataSourceProperty]
    public ShapeBase Shape
    {
        get => _shape;
        set
        {
            if (value != _shape)
            {
                _shape = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}
