using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.MixinHooks;

public class HookBaseVM : ViewModel
{
    public override void OnFinalize() => base.OnFinalize();
    public override void RefreshValues() => base.RefreshValues();
}

public class HookDerivedVM : HookBaseVM
{
    public int SetInConstructorBody;
    public HookDerivedVM() { SetInConstructorBody = 42; }
    public override void OnFinalize() => base.OnFinalize();
    public override void RefreshValues() => base.RefreshValues();
}

// Omits OnFinalize override to verify automatic patching of ViewModel.OnFinalize.
public class HookNoOverrideVM : ViewModel { }

public class HookOverrideVM : ViewModel
{
    public override void OnFinalize() => base.OnFinalize();
}

// Overrides OnFinalize without calling base method to verify lifecycle dispatch to derived types.
public class HookIntermediateVM : ViewModel
{
    public override void OnFinalize() { }
}

public class HookLeafVM : HookIntermediateVM { }

public class HookRefreshingVM : ViewModel
{
    public HookRefreshingVM() => RefreshValues();
    public override void RefreshValues() => base.RefreshValues();
}

public class HookTwiceRefreshingVM : ViewModel
{
    public HookTwiceRefreshingVM()
    {
        RefreshValues();
        RefreshValues();
    }
    public override void RefreshValues() => base.RefreshValues();
}

// Instantiated prior to mixin registration to verify late construction cache handling.
public class HookLateVM : ViewModel
{
    public override void RefreshValues() => base.RefreshValues();
}

public class HookRetiredVM : ViewModel
{
    public override void OnFinalize() => base.OnFinalize();
}

public class HookOverloadedRefreshVM : ViewModel
{
    public void Update(int value) { }
    public void Update(string value) { }
}

public static class HookCounters
{
    public static int DerivedFinalized, DerivedRefreshed, SeenInConstructor = -1, OverrideFinalized, LeafFinalized, RefreshingRefreshed;
    public static int TwiceRefreshed, RetiredFinalized, RetiredNotified;

    public static void Reset()
    {
        DerivedFinalized = DerivedRefreshed = OverrideFinalized = LeafFinalized = RefreshingRefreshed = 0;
        TwiceRefreshed = RetiredFinalized = RetiredNotified = 0;
        SeenInConstructor = -1;
    }
}

[ViewModelMixin(nameof(ViewModel.RefreshValues), true)]
internal class HookHierarchyMixin : BaseViewModelMixin<HookBaseVM>
{
    public HookHierarchyMixin(HookBaseVM vm) : base(vm)
    {
        if (vm is HookDerivedVM derived)
            HookCounters.SeenInConstructor = derived.SetInConstructorBody;
    }

    public override void OnRefresh() { if (ViewModel is HookDerivedVM) HookCounters.DerivedRefreshed++; }
    public override void OnFinalize() { if (ViewModel is HookDerivedVM) HookCounters.DerivedFinalized++; }
}

[ViewModelMixin]
internal class HookNoOverrideMixin : BaseViewModelMixin<HookNoOverrideVM>
{
    public HookNoOverrideMixin(HookNoOverrideVM vm) : base(vm) { }
}

[ViewModelMixin]
internal class HookOverrideMixin : BaseViewModelMixin<HookOverrideVM>
{
    public HookOverrideMixin(HookOverrideVM vm) : base(vm) { }
    public override void OnFinalize() => HookCounters.OverrideFinalized++;
}

[ViewModelMixin]
internal class HookLeafMixin : BaseViewModelMixin<HookLeafVM>
{
    public HookLeafMixin(HookLeafVM vm) : base(vm) { }
    public override void OnFinalize() => HookCounters.LeafFinalized++;
}

[ViewModelMixin(nameof(ViewModel.RefreshValues))]
internal class HookRefreshingMixin : BaseViewModelMixin<HookRefreshingVM>
{
    public HookRefreshingMixin(HookRefreshingVM vm) : base(vm) { }
    public override void OnRefresh() => HookCounters.RefreshingRefreshed++;
}

[ViewModelMixin(nameof(ViewModel.RefreshValues))]
internal class HookTwiceRefreshingMixin : BaseViewModelMixin<HookTwiceRefreshingVM>
{
    public HookTwiceRefreshingMixin(HookTwiceRefreshingVM vm) : base(vm) { }
    public override void OnRefresh() => HookCounters.TwiceRefreshed++;
}

[ViewModelMixin(nameof(ViewModel.RefreshValues))]
internal class HookLateMixin : BaseViewModelMixin<HookLateVM>
{
    public HookLateMixin(HookLateVM vm) : base(vm) { }
}

[ViewModelMixin]
internal class HookRetiredMixin : BaseViewModelMixin<HookRetiredVM>
{
    public HookRetiredMixin(HookRetiredVM vm) : base(vm) { }
    public override void OnFinalize() => HookCounters.RetiredFinalized++;
    protected override void OnViewModelPropertyChanged(string propertyName) => HookCounters.RetiredNotified++;
}

/// <summary>
/// Verifies lifecycle hook dispatch across nested ViewModel inheritance hierarchies, ensuring mixin construction,
/// refresh notifications, and finalization execute exactly once per lifecycle event.
/// </summary>
public class ViewModelMixinHookTests : BaseTests
{
    private UIExtender _uiExtender = default!;

    [SetUp]
    public void Setup()
    {
        HookCounters.Reset();
        _uiExtender = UIExtender.Create(nameof(ViewModelMixinHookTests));
        _uiExtender.Register([typeof(HookHierarchyMixin), typeof(HookNoOverrideMixin), typeof(HookOverrideMixin), typeof(HookLeafMixin), typeof(HookRefreshingMixin), typeof(HookTwiceRefreshingMixin)]);
        _uiExtender.Enable();
    }

    [TearDown]
    public void TearDown() => _uiExtender.Deregister();

    [Test]
    public void ADerivedViewModelsMixin_IsBuiltAfterItsConstructor()
    {
        _ = new HookDerivedVM();
        Assert.That(HookCounters.SeenInConstructor, Is.EqualTo(42));
    }

    [Test]
    public void ADerivedViewModelsMixin_IsRefreshedAndFinalizedOnce()
    {
        var vm = new HookDerivedVM();
        vm.RefreshValues();
        vm.OnFinalize();
        Assert.That(HookCounters.DerivedRefreshed, Is.EqualTo(1));
        Assert.That(HookCounters.DerivedFinalized, Is.EqualTo(1));
    }

    [Test]
    public void APatchedViewModelOnFinalize_DoesNotFinalizeAnOverridesMixinsAgain()
    {
        new HookOverrideVM().OnFinalize();
        Assert.That(HookCounters.OverrideFinalized, Is.EqualTo(1));
    }

    [Test]
    public void AnOverrideOnAnIntermediateType_FinalizesTheMixins()
    {
        new HookLeafVM().OnFinalize();
        Assert.That(HookCounters.LeafFinalized, Is.EqualTo(1));
    }

    [Test]
    public void ARefreshDuringConstruction_ReachesTheMixinOnceItExists()
    {
        _ = new HookRefreshingVM();
        Assert.That(HookCounters.RefreshingRefreshed, Is.EqualTo(1));
    }

    [Test]
    public void ARefreshCalledTwiceDuringConstruction_RefreshesTheMixinOnce()
    {
        _ = new HookTwiceRefreshingVM();
        Assert.That(HookCounters.TwiceRefreshed, Is.EqualTo(1));
    }

    [Test]
    public void AnInstanceConstructedBeforeItsMixinRegistered_HoldsEachRefreshNameOnce()
    {
        var vm = new HookLateVM();
        var extender = UIExtender.Create(nameof(AnInstanceConstructedBeforeItsMixinRegistered_HoldsEachRefreshNameOnce));
        extender.Register([typeof(HookLateMixin)]);
        extender.Enable();
        try
        {
            Refresh(vm);
            Refresh(vm);
            Refresh(vm);

            var component = UIExtender.GetRuntimeFor(nameof(AnInstanceConstructedBeforeItsMixinRegistered_HoldsEachRefreshNameOnce))!.ViewModelComponent;
            Assert.That(component.MixinInstanceRefreshFromConstructorCache.TryGetValue(vm, out var held), Is.True);
            Assert.That(held, Is.EqualTo(new[] { nameof(ViewModel.RefreshValues) }));
        }
        finally
        {
            extender.Deregister();
        }
    }

    [Test]
    public void ADeregisteredModulesMixin_IsFinalizedWithItsViewModel()
    {
        var extender = UIExtender.Create(nameof(ADeregisteredModulesMixin_IsFinalizedWithItsViewModel));
        extender.Register([typeof(HookRetiredMixin)]);
        extender.Enable();
        var vm = Create<HookRetiredVM>();
        extender.Deregister();

        FinalizeViewModel(vm);
        Assert.That(HookCounters.RetiredFinalized, Is.EqualTo(1));

        vm.OnPropertyChanged("AfterFinalize");
        Assert.That(HookCounters.RetiredNotified, Is.EqualTo(0), "unsubscribed when finalized");
    }

    // The tests that register a mixin themselves were compiled before its patches existed, and in Release the JIT can
    // inline a view model's small constructor or override into them, past the patch (PatchInliningTests). These are
    // compiled on their first call, after the patches.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static T Create<T>() where T : ViewModel, new() => new();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Refresh(ViewModel vm) => vm.RefreshValues();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void FinalizeViewModel(ViewModel vm) => vm.OnFinalize();

    [Test]
    public void AMissingRefreshMethod_IsNotAnError()
    {
        var lines = Traced(() => Assert.DoesNotThrow(() => ViewModelWithMixinPatch.Patch(UIExtender.Harmony, typeof(HookRefreshingVM), "NoSuchRefreshMethod")));

        Assert.That(lines, Has.Some.Contains($"{typeof(HookRefreshingVM).FullName} has no method NoSuchRefreshMethod!"));
    }

    [Test]
    public void AnOverloadedRefreshMethodWithoutAParameterlessOverload_IsReportedAsOverloaded()
    {
        var lines = Traced(() => ViewModelWithMixinPatch.Patch(UIExtender.Harmony, typeof(HookOverloadedRefreshVM), nameof(HookOverloadedRefreshVM.Update)));

        Assert.That(lines, Has.Some.Contains("has no method Update to hook: it is overloaded, and no overload takes no parameters!"));
    }

    private static List<string> Traced(Action action)
    {
        var listener = new CollectingListener();
        Trace.Listeners.Add(listener);
        try
        {
            action();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
        return listener.Lines;
    }

    private sealed class CollectingListener : TraceListener
    {
        public List<string> Lines { get; } = [];
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { if (message is not null) lock (Lines) Lines.Add(message); }
    }
}