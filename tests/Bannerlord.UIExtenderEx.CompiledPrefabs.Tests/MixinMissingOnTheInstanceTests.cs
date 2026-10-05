using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using System;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies binding behavior when referencing mixin members on ViewModel instances that lack the registered mixin.
/// <para>
/// When a derived ViewModel lacks the registered mixin, dynamic property lookups return <see langword="null"/>
/// and bindings reset to default values, matching GauntletUI XML loader semantics.
/// </para>
/// </summary>
[NonParallelizable]
public class MixinMissingOnTheInstanceTests
{
    private const string Movie = "MixinMissingMovie";
    private const string Prefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Child" DataSource="{Child}">
          <Children>
            <Widget Id="Shown" IsVisible="@MixinShown" />
            <Widget Id="Label" HoveredCursorState="@MixinLabel" />
            <Widget Id="Go" Command.Click="ExecuteMixinGo" />
          </Children>
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    private UIExtender _extender = null!;
    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        _extender = UIExtender.Create("TestModule.CompiledPrefabs." + nameof(MixinMissingOnTheInstanceTests) + "." + NUnit.Framework.TestContext.CurrentContext.Test.Name);
        _extender.Register([typeof(MissingProbeMixin)]);
        _extender.Enable();
        _workspace = new PrefabWorkspace((Movie, Prefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(MixinMissingOnTheInstanceTests));
    }

    [TearDown]
    public void TearDown()
    {
        _extender?.Deregister();
        _ui?.Dispose();
        _workspace?.Dispose();
        DynamicMember.Host = _previousHost;
    }

    private Widget XmlRoot(ViewModel viewModel)
    {
        var loaded = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, viewModel, doNotUseGeneratedPrefabs: true, hotReloadEnabled: false);
        Assert.That(loaded, Is.InstanceOf<GauntletMovie>(), "the XML path was not taken");
        return loaded.RootWidget;
    }

    private static Widget ById(Widget root, string id) => root.FindChild(id, includeAllChildren: true) ?? throw new InvalidOperationException($"No widget '{id}'.");

    /// <summary>
    /// Verifies that dynamic property queries return <see langword="null"/> and command invocations complete safely on instances without the mixin.
    /// </summary>
    [Test]
    public void TheLoadersViewModelCalls_OnAnInstanceWithoutTheMixin()
    {
        var withMixin = new MissingProbeVM();
        var without = new MissingProbeDerivedVM();

        Assert.That(ViewModelMixins.Get<MissingProbeMixin>(withMixin), Is.Not.Null);
        Assert.That(ViewModelMixins.Get<MissingProbeMixin>(without), Is.Null, "the mixin is registered for MissingProbeVM exactly");
        Assert.That(withMixin.GetPropertyValue("MixinShown"), Is.EqualTo(true));
        Assert.That(without.GetPropertyValue("MixinShown"), Is.Null);
        Assert.That(() => without.SetPropertyValue("MixinShown", false), Throws.Nothing);
        Assert.That(() => without.ExecuteCommand("ExecuteMixinGo", []), Throws.Nothing);
    }

    /// <summary>
    /// Verifies that opening a movie with an instance lacking the mixin sets default property values, matching XML loader behavior.
    /// </summary>
    [Test]
    public void Opening_SetsTheDefault_AsTheXmlLoaderDoes()
    {
        var xmlRoot = XmlRoot(new MissingProbeRootVM(new MissingProbeDerivedVM()));
        var compiled = CompiledMovie.Build(_workspace, Movie, typeof(MissingProbeRootVM), _ui);
        compiled.SetDataSource(new MissingProbeRootVM(new MissingProbeDerivedVM()));

        Assert.That(ById(xmlRoot, "Shown").IsVisible, Is.False, "XML: null into the bool setter");
        Assert.That(compiled.ById("Shown").IsVisible, Is.False);
        Assert.That(ById(xmlRoot, "Label").HoveredCursorState, Is.Null);
        Assert.That(compiled.ById("Label").HoveredCursorState, Is.Null);
    }

    /// <summary>
    /// Verifies that replacing a child ViewModel that has a mixin with one lacking the mixin resets widget attributes to defaults.
    /// </summary>
    [Test]
    public void AChildReplacedByOneWithoutTheMixin_ClearsTheWidgets_AsTheXmlLoaderDoes()
    {
        var xmlViewModel = new MissingProbeRootVM(new MissingProbeVM());
        var xmlRoot = XmlRoot(xmlViewModel);
        var compiled = CompiledMovie.Build(_workspace, Movie, typeof(MissingProbeRootVM), _ui);
        var compiledViewModel = new MissingProbeRootVM(new MissingProbeVM());
        compiled.SetDataSource(compiledViewModel);

        Assert.That(ById(xmlRoot, "Label").HoveredCursorState, Is.EqualTo("mixin"));
        Assert.That(compiled.ById("Label").HoveredCursorState, Is.EqualTo("mixin"));

        xmlViewModel.Child = new MissingProbeDerivedVM();
        compiledViewModel.Child = new MissingProbeDerivedVM();

        Assert.That(ById(xmlRoot, "Label").HoveredCursorState, Is.Null, "XML: GetPropertyValue answers null, assigned");
        Assert.That(compiled.ById("Label").HoveredCursorState, Is.Null);
        Assert.That(ById(xmlRoot, "Shown").IsVisible, Is.False, "XML: null into the bool setter");
        Assert.That(compiled.ById("Shown").IsVisible, Is.False);

        // Reinstates the child with the registered mixin to verify that values reapply.
        xmlViewModel.Child = new MissingProbeVM();
        compiledViewModel.Child = new MissingProbeVM();
        Assert.That(ById(xmlRoot, "Label").HoveredCursorState, Is.EqualTo("mixin"));
        Assert.That(compiled.ById("Label").HoveredCursorState, Is.EqualTo("mixin"));
        Assert.That(compiled.ById("Shown").IsVisible, Is.True);
    }

    /// <summary>
    /// Verifies that write-backs and command executions execute safely without throwing or mutating missing mixin instances.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void WritesAndCommands_DoNothing(bool compiled)
    {
        var child = new MissingProbeDerivedVM();
        Widget root;
        if (compiled)
        {
            var movie = CompiledMovie.Build(_workspace, Movie, typeof(MissingProbeRootVM), _ui);
            movie.SetDataSource(new MissingProbeRootVM(child));
            root = movie.Root;
        }
        else
        {
            root = XmlRoot(new MissingProbeRootVM(child));
        }

        var shown = ById(root, "Shown");
        Assert.That(() => shown.IsVisible = !shown.IsVisible, Throws.Nothing);
        Assert.That(() => CompiledMovie.FireEvent(ById(root, "Go"), "Click"), Throws.Nothing);
        Assert.That(child.GetPropertyValue("MixinShown"), Is.Null, "nothing was added to the instance");
    }
}

public class MissingProbeRootVM : ViewModel
{
    private MissingProbeVM _child;

    public MissingProbeRootVM(MissingProbeVM child) => _child = child;

    [DataSourceProperty]
    public MissingProbeVM Child
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
}

public class MissingProbeVM : ViewModel { }

/// <summary>
/// Represents a derived ViewModel type for which the target mixin is not registered.
/// </summary>
public class MissingProbeDerivedVM : MissingProbeVM { }

[ViewModelMixin]
public class MissingProbeMixin : BaseViewModelMixin<MissingProbeVM>
{
    public MissingProbeMixin(MissingProbeVM vm) : base(vm) { }

    [DataSourceProperty]
    public bool MixinShown => true;

    [DataSourceProperty]
    public string MixinLabel => "mixin";

    [DataSourceMethod]
    public void ExecuteMixinGo() { }
}
