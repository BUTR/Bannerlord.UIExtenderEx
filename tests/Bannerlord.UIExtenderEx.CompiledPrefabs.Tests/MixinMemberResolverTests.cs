using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;

using NUnit.Framework;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies mixin member and command resolution semantics performed by <see cref="MixinMemberResolver"/>.
/// </summary>
public class MixinMemberResolverTests
{
    private UIExtender? _extender;
    private readonly MixinMemberResolver _resolver = new();

    [SetUp]
    public void SetUp()
    {
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.Resolver");
        _extender.Register([typeof(CodegenTestVMMixin), typeof(CodegenTestVMSecondMixin)]);
        _extender.Enable();
    }

    [TearDown]
    public void TearDown() => _extender?.Deregister();

    [Test]
    public void Property_OfEnabledMixin_IsResolved()
    {
        var property = _resolver.ResolveProperty(typeof(CodegenTestVM), nameof(CodegenTestVMMixin.EditableText), out var mixinType);

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.PropertyType, Is.EqualTo(typeof(string)));
        Assert.That(mixinType, Is.EqualTo(typeof(CodegenTestVMMixin)));
    }

    [Test]
    public void Property_DefinedByTwoMixins_LastRegistrationWins()
    {
        _resolver.ResolveProperty(typeof(CodegenTestVM), nameof(CodegenTestVMMixin.MixinText), out var mixinType);

        Assert.That(mixinType, Is.EqualTo(typeof(CodegenTestVMSecondMixin)));
    }

    [Test]
    public void Property_WithoutDataSourceAttribute_IsNotResolved()
    {
        Assert.That(_resolver.ResolveProperty(typeof(CodegenTestVM), nameof(CodegenTestVMMixin.NotBound), out var mixinType), Is.Null);
        Assert.That(mixinType, Is.Null);
    }

    [Test]
    public void Property_OfUnrelatedViewModel_IsNotResolved()
    {
        Assert.That(_resolver.ResolveProperty(typeof(CodegenOtherVM), nameof(CodegenTestVMMixin.MixinText), out _), Is.Null);
    }

    [Test]
    public void Property_OfDisabledMixin_IsNotResolved()
    {
        _extender!.Disable();

        Assert.That(_resolver.ResolveProperty(typeof(CodegenTestVM), nameof(CodegenTestVMMixin.MixinText), out _), Is.Null);
    }

    [Test]
    public void Method_WithDataSourceMethodAttribute_IsResolved()
    {
        var method = _resolver.ResolveMethod(typeof(CodegenTestVM), nameof(CodegenTestVMMixin.ExecuteMixinCommand), out var mixinType);

        Assert.That(method, Is.Not.Null);
        Assert.That(mixinType, Is.EqualTo(typeof(CodegenTestVMMixin)));
    }

    [Test]
    public void Method_WithoutAttribute_IsNotResolved()
    {
        Assert.That(_resolver.ResolveMethod(typeof(CodegenTestVM), nameof(CodegenTestVMMixin.NotACommand), out _), Is.Null);
    }

    /// <summary>
    /// Verifies that an overridden command without an explicit <c>[DataSourceMethod]</c> attribute is not resolved,
    /// matching the GauntletUI runtime requirement where only direct declared attributes register commands.
    /// </summary>
    [Test]
    public void Method_AnUnmarkedOverrideOfAMarkedCommand_IsNotResolved_AsTheLoaderDoesNotRunIt()
    {
        _extender!.Deregister();
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.Resolver");
        _extender.Register([typeof(OverridingCommandMixin)]);
        _extender.Enable();
        var viewModel = CreateOverriddenCommandHost();

        viewModel.ExecuteCommand(nameof(OverridingCommandMixin.ExecuteShared), []);

        Assert.That(ViewModels.ViewModelMixins.Get<OverridingCommandMixin>(viewModel)!.Runs, Is.Zero, "the loader does not run it");
        Assert.That(_resolver.ResolveMethod(typeof(OverriddenCommandHostVM), nameof(OverridingCommandMixin.ExecuteShared), out _), Is.Null);
    }

    // The test was compiled before the mixin's constructor patch existed, and in Release the JIT can inline the view
    // model's small constructor into it, past the patch (PatchInliningTests). This is compiled on its first call, after it.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static OverriddenCommandHostVM CreateOverriddenCommandHost() => new();

    [Test]
    public void GeneratorLookup_PrefersTheViewModelTypeOverMixins()
    {
        var property = ViewModelMemberResolution.GetProperty(typeof(CodegenTestVM), nameof(CodegenTestVM.Title), out var mixinType);

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.DeclaringType, Is.EqualTo(typeof(CodegenTestVM)));
        Assert.That(mixinType, Is.Null);
    }

    [Test]
    public void GeneratorLookup_FallsBackToMixins()
    {
        Assert.That(ViewModelMemberResolution.Resolver, Is.Not.Null, "the compiled runtime installs the resolver");

        var property = ViewModelMemberResolution.GetProperty(typeof(CodegenTestVM), nameof(CodegenTestVMMixin.EditableText), out var mixinType);

        Assert.That(property, Is.Not.Null);
        Assert.That(mixinType, Is.EqualTo(typeof(CodegenTestVMMixin)));
    }

    [Test]
    public void GeneratorLookup_HandlesNullType()
    {
        Assert.That(ViewModelMemberResolution.GetProperty(null, "Anything", out _), Is.Null);
        Assert.That(ViewModelMemberResolution.GetMethod(null, "Anything", out _), Is.Null);
    }

    [Test]
    public void ViewModelMixins_Get_ReturnsTheAttachedInstance()
    {
        var vm = new CodegenTestVM();

        var mixin = ViewModels.ViewModelMixins.Get<CodegenTestVMMixin>(vm);

        Assert.That(mixin, Is.Not.Null);
        Assert.That(ViewModels.ViewModelMixins.Select<CodegenTestVMMixin, string>(vm, x => x.MixinText), Is.EqualTo("mixin"));
        Assert.That(ViewModels.ViewModelMixins.Get<CodegenTestVMMixin>(null), Is.Null);
        Assert.That(ViewModels.ViewModelMixins.Select<CodegenTestVMMixin, string>(null, x => x.MixinText), Is.Null);
    }

    [Test]
    public void ViewModelMixins_Get_ReturnsNullForAViewModelWithoutThatMixin()
    {
        ViewModel vm = new CodegenOtherVM();

        Assert.That(ViewModels.ViewModelMixins.Get<CodegenTestVMMixin>(vm), Is.Null);
    }
}

public class OverriddenCommandHostVM : ViewModel;

public abstract class CommandMixinBase : ViewModels.BaseViewModelMixin<OverriddenCommandHostVM>
{
    protected CommandMixinBase(OverriddenCommandHostVM vm) : base(vm) { }

    [Attributes.DataSourceMethod]
    public virtual void ExecuteShared() { }
}

[Attributes.ViewModelMixin]
public class OverridingCommandMixin : CommandMixinBase
{
    public OverridingCommandMixin(OverriddenCommandHostVM vm) : base(vm) { }

    public int Runs { get; private set; }

    public override void ExecuteShared() => Runs++;
}
