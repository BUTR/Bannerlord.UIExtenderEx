using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using System;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies mixin member resolution across distinct ViewModel instances of the same runtime type.
/// <para>
/// Because mixin members bind to specific mixin instances, member accessors must preserve instance
/// isolation and prevent cross-instance cache pollution during compiled movie evaluations.
/// </para>
/// </summary>
public class MixinInstanceBindingTests
{
    private const string Movie = "MixinInstanceMovie";

    private const string Prefab = """
<Prefab>
  <Window>
    <Widget Id="Mixin" HoveredCursorState="@InstanceText" />
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
        // Registers the mixin without enabling it initially to force late runtime dynamic member resolution.
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.MixinInstance");
        _extender.Register([typeof(MixinInstanceMixin)]);
        _workspace = new PrefabWorkspace((Movie, Prefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(MixinInstanceBindingTests));
    }

    [TearDown]
    public void TearDown()
    {
        _extender.Deregister();
        DynamicMember.Host = _previousHost;
        _ui.Dispose();
        _workspace.Dispose();
    }

    /// <summary>
    /// Verifies that distinct ViewModel instances retain independent mixin property values across sequential data source updates.
    /// </summary>
    [Test]
    public void TwoInstancesOfOneTypeKeepTheirOwnMixinValues()
    {
        _extender.Enable();
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(MixinInstanceVM), _ui);
        var first = new MixinInstanceVM("first");
        var second = new MixinInstanceVM("second");

        movie.SetDataSource(first);
        Assert.That(movie.ById("Mixin").HoveredCursorState, Is.EqualTo("first"));

        movie.SetDataSource(second);
        Assert.That(movie.ById("Mixin").HoveredCursorState, Is.EqualTo("second"));

        movie.SetDataSource(first);
        Assert.That(movie.ById("Mixin").HoveredCursorState, Is.EqualTo("first"));
    }

    /// <summary>
    /// Verifies that releasing or destroying the data source clears cached mixin references and allows garbage collection of the ViewModel.
    /// </summary>
    [TestCase(false, TestName = "ReleasingTheDataSource_LetsTheViewModelAndItsMixinGo")]
    [TestCase(true, TestName = "DestroyingTheDataSource_LetsTheViewModelAndItsMixinGo")]
    public void ReleasingTheDataSource_LetsTheViewModelAndItsMixinGo(bool destroy)
    {
        _extender.Enable();
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(MixinInstanceVM), _ui);

        var viewModel = BindAndForget(movie);
        if (destroy)
            movie.DestroyDataSource();
        else
            movie.SetDataSource(null);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.That(viewModel.IsAlive, Is.False, "the compiled widget still holds the released ViewModel through its mixin receiver");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference BindAndForget(CompiledMovie movie)
    {
        var viewModel = new MixinInstanceVM("first");
        movie.SetDataSource(viewModel);
        Assert.That(movie.ById("Mixin").HoveredCursorState, Is.EqualTo("first"));
        return new WeakReference(viewModel);
    }

    /// <summary>
    /// Verifies that property bindings evaluate to <see langword="null"/> when the contributing mixin is disabled.
    /// </summary>
    [Test]
    public void WithTheMixinDisabledTheBindingFindsNothing()
    {
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(MixinInstanceVM), _ui);

        movie.SetDataSource(new MixinInstanceVM("first"));

        Assert.That(movie.ById("Mixin").HoveredCursorState, Is.Null);
    }
}

public class MixinInstanceVM : ViewModel
{
    public MixinInstanceVM(string tag) => Tag = tag;

    /// <summary>
    /// Gets the instance tag consumed by the associated mixin.
    /// </summary>
    public string Tag { get; }
}

[ViewModelMixin]
public class MixinInstanceMixin : BaseViewModelMixin<MixinInstanceVM>
{
    private readonly MixinInstanceVM _viewModel;

    public MixinInstanceMixin(MixinInstanceVM vm) : base(vm) => _viewModel = vm;

    [DataSourceProperty]
    public string InstanceText => _viewModel.Tag;
}
