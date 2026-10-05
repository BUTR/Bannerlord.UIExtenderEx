using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies member shadowing rules when a mixin member shares the same name as a member on its underlying ViewModel.
/// <para>
/// When a mixin contributes a member with a matching name, the mixin member takes precedence in the instance binding table,
/// ensuring parity with the GauntletUI XML loader. When the mixin is absent or disabled, resolution falls back to the host ViewModel.
/// </para>
/// </summary>
public class MixinShadowingTests
{
    private const string Movie = "MixinShadowingMovie";

    private const string Prefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Label" HoveredCursorState="@Label" />
        <Widget Id="Done" Command.Click="ExecuteDone" />
        <Widget Id="Secret" Command.Click="ExecuteSecret" />
        <Widget Id="Child" DataSource="{Child}">
          <Children>
            <Widget Id="ChildText" HoveredCursorState="@Text" />
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
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.MixinShadowing");
        _extender.Register([typeof(ShadowingMixin)]);
        _workspace = new PrefabWorkspace((Movie, Prefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(MixinShadowingTests));
    }

    [TearDown]
    public void TearDown()
    {
        _extender.Deregister();
        DynamicMember.Host = _previousHost;
        _ui.Dispose();
        _workspace.Dispose();
    }

    private static void Click(CompiledMovie movie, string id) => CompiledMovie.FireEvent(movie.ById(id), "Click");

    // Verifies runtime behavior produced by the standard XML loader.

    [Test]
    public void TheLoader_ReadsAndRunsTheMixinsMembers()
    {
        _extender.Enable();
        var viewModel = new ShadowedHostVM();
        var mixin = ViewModelMixins.Get<ShadowingMixin>(viewModel)!;

        viewModel.ExecuteCommand("ExecuteDone", []);
        viewModel.ExecuteCommand("ExecuteSecret", []);

        Assert.That(viewModel.GetPropertyValue("Label"), Is.EqualTo("mixin"));
        Assert.That(viewModel.GetPropertyValue("Child"), Is.SameAs(mixin.Child));
        Assert.That((mixin.Done, viewModel.HostDone), Is.EqualTo((1, 0)));
        Assert.That((mixin.Secret, viewModel.HostSecret), Is.EqualTo((1, 0)));
    }

    [Test]
    public void TheLoader_OnAnInstanceWithoutTheMixin_ReadsAndRunsTheViewModelsOwn()
    {
        _extender.Enable();
        var viewModel = new ShadowedDerivedVM();

        viewModel.ExecuteCommand("ExecuteDone", []);
        viewModel.ExecuteCommand("ExecuteSecret", []);

        Assert.That(ViewModelMixins.Get<ShadowingMixin>(viewModel), Is.Null, "the mixin is registered for ShadowedHostVM exactly");
        Assert.That(viewModel.GetPropertyValue("Label"), Is.EqualTo("host"));
        Assert.That((viewModel.HostDone, viewModel.HostSecret), Is.EqualTo((1, 1)));
    }

    // Verifies runtime behavior produced by compiled prefabs.

    [Test]
    public void Compiled_ReadsTheMixinsProperty()
    {
        _extender.Enable();
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(ShadowedHostVM), _ui);

        movie.SetDataSource(new ShadowedHostVM());

        Assert.That(movie.ById("Label").HoveredCursorState, Is.EqualTo("mixin"));
    }

    [Test]
    public void Compiled_BindsTheMixinsChildDataSource()
    {
        _extender.Enable();
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(ShadowedHostVM), _ui);

        movie.SetDataSource(new ShadowedHostVM());

        Assert.That(movie.ById("ChildText").HoveredCursorState, Is.EqualTo("mixin child"));
    }

    [Test]
    public void Compiled_RunsTheMixinsCommand()
    {
        _extender.Enable();
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(ShadowedHostVM), _ui);
        var viewModel = new ShadowedHostVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Done");

        Assert.That((ViewModelMixins.Get<ShadowingMixin>(viewModel)!.Done, viewModel.HostDone), Is.EqualTo((1, 0)));
    }

    /// <summary>
    /// Verifies that a mixin command takes precedence over a private method declared on the host ViewModel.
    /// </summary>
    [Test]
    public void Compiled_RunsTheMixinsCommandOverAPrivateMethodOfTheViewModel()
    {
        _extender.Enable();
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(ShadowedHostVM), _ui);
        var viewModel = new ShadowedHostVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Secret");

        Assert.That((ViewModelMixins.Get<ShadowingMixin>(viewModel)!.Secret, viewModel.HostSecret), Is.EqualTo((1, 0)));
    }

    /// <summary>
    /// Verifies that compiled movies fall back to the host ViewModel members when provided an instance whose derived type lacks the mixin.
    /// </summary>
    [Test]
    public void Compiled_OnAnInstanceWithoutTheMixin_ReadsAndRunsTheViewModelsOwn()
    {
        _extender.Enable();
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(ShadowedHostVM), _ui);
        var viewModel = new ShadowedDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Done");
        Click(movie, "Secret");

        Assert.That(movie.ById("Label").HoveredCursorState, Is.EqualTo("host"));
        Assert.That(movie.ById("ChildText").HoveredCursorState, Is.EqualTo("host child"));
        Assert.That((viewModel.HostDone, viewModel.HostSecret), Is.EqualTo((1, 1)));
    }

    /// <summary>
    /// Verifies that compiled movies bind directly to the host ViewModel members when the mixin is disabled.
    /// </summary>
    [Test]
    public void Compiled_WithTheMixinDisabled_ReadsAndRunsTheViewModelsOwn()
    {
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(ShadowedHostVM), _ui);
        var viewModel = new ShadowedHostVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Done");

        Assert.That(movie.ById("Label").HoveredCursorState, Is.EqualTo("host"));
        Assert.That(viewModel.HostDone, Is.EqualTo(1));
    }
}

public class ShadowedHostVM : ViewModel
{
    public int HostDone { get; private set; }
    public int HostSecret { get; private set; }

    public string Label => "host";

    public ShadowChildVM Child { get; } = new("host child");

    public void ExecuteDone() => HostDone++;

    private void ExecuteSecret() => HostSecret++;
}

/// <summary>
/// Represents a derived ViewModel type for which the target mixin is not registered.
/// </summary>
public class ShadowedDerivedVM : ShadowedHostVM { }

[ViewModelMixin]
public class ShadowingMixin : BaseViewModelMixin<ShadowedHostVM>
{
    public ShadowingMixin(ShadowedHostVM vm) : base(vm) { }

    public int Done { get; private set; }
    public int Secret { get; private set; }

    [DataSourceProperty]
    public string Label => "mixin";

    [DataSourceProperty]
    public ShadowChildVM Child { get; } = new("mixin child");

    [DataSourceMethod]
    public void ExecuteDone() => Done++;

    [DataSourceMethod]
    public void ExecuteSecret() => Secret++;
}

public class ShadowChildVM : ViewModel
{
    public ShadowChildVM(string text) => Text = text;

    public string Text { get; }
}
