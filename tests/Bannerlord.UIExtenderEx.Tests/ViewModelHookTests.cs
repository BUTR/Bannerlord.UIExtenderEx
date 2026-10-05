using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;
using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.ViewModelHooks;

/// <summary>
/// Verifies hook mechanisms that allow mixins to operate without manual Harmony patches:
/// <c>OnViewModelPropertyChanged</c>, <see cref="BUTRViewModelOverrideAttribute"/>, and <see cref="BUTRUnsafeAccessorAttribute"/>.
/// </summary>
public class ViewModelHookTests
{
    private readonly List<UIExtender> _extenders = [];
    private CollectingListener _listener = null!;

    /// <summary>
    /// Applies ViewModel patches prior to test method compilation to prevent Release JIT inlining on net472
    /// from embedding unpatched method bodies.
    /// </summary>
    [OneTimeSetUp]
    public void ApplyThePatchesBeforeAnyTestIsCompiled()
    {
        var extender = UIExtender.Create("TestModule.ViewModelHooks.Patches");
        extender.Register([typeof(ListeningMixin), typeof(FirstOverrideMixin), typeof(ArgumentOverrideMixin)]);
        extender.Deregister();
    }


    [SetUp]
    public void SetUp()
    {
        HookLog.Clear();
        _listener = new CollectingListener();
        Trace.Listeners.Add(_listener);
    }

    [TearDown]
    public void TearDown()
    {
        Trace.Listeners.Remove(_listener);
        foreach (var extender in _extenders)
            extender.Deregister();
        _extenders.Clear();
    }

    private UIExtender Enable(string module, params Type[] types)
    {
        var extender = UIExtender.Create($"TestModule.ViewModelHooks.{module}");
        _extenders.Add(extender);
        extender.Register(types);
        extender.Enable();
        return extender;
    }

    private IEnumerable<string> Reports(string fragment) => _listener.Lines.Where(x => x.Contains(fragment));

    // ---------------------------------------------------------------- OnViewModelPropertyChanged

    [Test]
    public void TypedAndPlainNotifications_ReachTheMixin()
    {
        Enable(nameof(TypedAndPlainNotifications_ReachTheMixin), typeof(ListeningMixin));
        var viewModel = new NotifyingVM();

        viewModel.Count = 3;            // OnPropertyChangedWithValue(int)
        viewModel.Label = "x";          // OnPropertyChangedWithValue<T>(string)
        viewModel.OnPropertyChanged("Plain");

        Assert.That(ViewModelMixins.Get<ListeningMixin>(viewModel)!.Heard, Is.EqualTo(new[] { "Count", "Label", "Plain" }));
    }

    [Test]
    public void AfterTheViewModelIsFinalized_TheMixinHearsNothing()
    {
        Enable(nameof(AfterTheViewModelIsFinalized_TheMixinHearsNothing), typeof(ListeningMixin));
        var viewModel = new NotifyingVM();
        var mixin = ViewModelMixins.Get<ListeningMixin>(viewModel)!;

        viewModel.OnFinalize();
        viewModel.Count = 5;

        Assert.That(mixin.Heard, Is.Empty);
    }

    // ---------------------------------------------------------------- BUTRViewModelOverride

    [Test]
    public void AnOverride_RunsForACommandAndForADirectCall()
    {
        Enable(nameof(AnOverride_RunsForACommandAndForADirectCall), typeof(FirstOverrideMixin));
        var viewModel = new OverriddenVM();

        viewModel.ExecuteCommand(nameof(OverriddenVM.ExecuteDone), []);
        viewModel.ExecuteDone();

        Assert.That(HookLog.Entries, Is.EqualTo(new[] { "first", "body", "first", "body" }));
    }

    [Test]
    public void AnOverrideThatDoesNotCallTheOriginal_SkipsTheBody()
    {
        Enable(nameof(AnOverrideThatDoesNotCallTheOriginal_SkipsTheBody), typeof(BlockingOverrideMixin));

        new OverriddenVM().ExecuteDone();

        Assert.That(HookLog.Entries, Is.EqualTo(new[] { "blocked" }));
    }

    [Test]
    public void ArgumentsReachTheOverride_AndTheOriginalPassesItsOwn()
    {
        Enable(nameof(ArgumentsReachTheOverride_AndTheOriginalPassesItsOwn), typeof(ArgumentOverrideMixin));

        new OverriddenVM().SetCategory(4, "tab");

        Assert.That(HookLog.Entries, Is.EqualTo(new[] { "override 4 tab", "body 5 tab!" }));
    }

    /// <summary>Verifies that multiple method overrides chain sequentially in registration order, invoking the original method last.</summary>
    [Test]
    public void OverridesChain_InRegistrationOrder()
    {
        Enable("First", typeof(FirstOverrideMixin));
        Enable("Second", typeof(SecondOverrideMixin));

        new OverriddenVM().ExecuteDone();

        Assert.That(HookLog.Entries, Is.EqualTo(new[] { "first", "second", "body" }));
    }

    [Test]
    public void AnInstanceWithoutTheMixin_RunsTheBodyOnly()
    {
        Enable(nameof(AnInstanceWithoutTheMixin_RunsTheBodyOnly), typeof(FirstOverrideMixin));

        new OverriddenDerivedVM().ExecuteDone();

        Assert.That(HookLog.Entries, Is.EqualTo(new[] { "body" }));
    }

    [Test]
    public void AMixinDisabledAfterTheInstanceWasCreated_NoLongerOverrides()
    {
        var extender = Enable(nameof(AMixinDisabledAfterTheInstanceWasCreated_NoLongerOverrides), typeof(FirstOverrideMixin));
        var viewModel = new OverriddenVM();

        extender.Disable();
        viewModel.ExecuteDone();

        Assert.That(HookLog.Entries, Is.EqualTo(new[] { "body" }));
    }

    [Test]
    public void AnExceptionInAnOverride_ReachesTheCallerUnwrapped()
    {
        Enable(nameof(AnExceptionInAnOverride_ReachesTheCallerUnwrapped), typeof(ThrowingOverrideMixin));

        Assert.That(() => new OverriddenVM().ExecuteDone(), Throws.InvalidOperationException.With.Message.EqualTo("from the override"));
    }

    [Test]
    public void AMalformedOverride_IsReportedAndLeftOut()
    {
        Enable(nameof(AMalformedOverride_IsReportedAndLeftOut), typeof(MalformedOverrideMixin));

        new OverriddenVM().ExecuteDone();

        Assert.That(HookLog.Entries, Is.EqualTo(new[] { "body" }));
        Assert.That(Reports("its last parameter has to be the original, a delegate"), Is.Not.Empty);
        Assert.That(Reports("it has no instance method NoSuchMethod()"), Is.Not.Empty);
    }

    /// <summary>
    /// Verifies that command clicks within compiled prefabs trigger method overrides applied directly to ViewModel methods.
    /// </summary>
    [Test]
    public void AButtonInACompiledPrefab_RunsTheOverride()
    {
        var previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        using var workspace = new PrefabWorkspace(("OverrideMovie", """
<Prefab>
  <Window>
    <Widget Id="Done" Command.Click="ExecuteDone" />
  </Window>
</Prefab>
"""));
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(AButtonInACompiledPrefab_RunsTheOverride));
        try
        {
            Enable(nameof(AButtonInACompiledPrefab_RunsTheOverride), typeof(FirstOverrideMixin));
            var movie = CompiledMovie.Build(workspace, "OverrideMovie", typeof(OverriddenVM), ui);
            movie.SetDataSource(new OverriddenVM());

            CompiledMovie.FireEvent(movie.ById("Done"), "Click");

            Assert.That(HookLog.Entries, Is.EqualTo(new[] { "first", "body" }));
        }
        finally
        {
            DynamicMember.Host = previousHost;
        }
    }

    // ---------------------------------------------------------------- BUTRUnsafeAccessor

    [Test]
    public void AccessorsReachPrivateMembers()
    {
        Enable(nameof(AccessorsReachPrivateMembers), typeof(Accessors));
        var target = new AccessedVM();

        Accessors.Secret(target) = 41;
        Accessors.Secret(target)++;

        Assert.That(target.ReadSecret(), Is.EqualTo(42));
        Assert.That(Accessors.Combine(target, 2, "x"), Is.EqualTo("42-2-x"));
        Assert.That(Accessors.InheritedSecret(target), Is.EqualTo("base"));
        Assert.That(Accessors.StaticCounter(), Is.EqualTo(7));
        Assert.That(Accessors.StaticTwice(21), Is.EqualTo(42));
    }

    /// <summary>Verifies access to private fields declared on base TaleWorlds ViewModel classes.</summary>
    [Test]
    public void AnAccessorReachesAPrivateFieldOfTheGame()
    {
        Enable(nameof(AnAccessorReachesAPrivateFieldOfTheGame), typeof(Accessors));
        var viewModel = new AccessedVM();

        Assert.That(Accessors.ViewModelType(viewModel), Is.EqualTo(typeof(AccessedVM)));
    }

    [Test]
    public void AnAccessorWhoseMemberIsMissing_IsReportedAndThrows()
    {
        Enable(nameof(AnAccessorWhoseMemberIsMissing_IsReportedAndThrows), typeof(BrokenAccessors));

        Assert.That(Reports("accessor " + typeof(BrokenAccessors).FullName + ".Missing is not resolved"), Is.Not.Empty);
        Assert.That(Reports("accessor " + typeof(BrokenAccessors).FullName + ".NotStatic is not resolved: the stub has to be static"), Is.Not.Empty);
        Assert.That(Reports("accessor " + typeof(BrokenAccessors).FullName + ".WrongType is not resolved: the stub has to return ref Int32"), Is.Not.Empty);
        Assert.That(() => BrokenAccessors.Missing(new AccessedVM()), Throws.InstanceOf<NotImplementedException>());
    }

    [Test]
    public void AnAccessorWithoutNoInlining_IsReportedAndStillResolved()
    {
        Enable(nameof(AnAccessorWithoutNoInlining_IsReportedAndStillResolved), typeof(InlinableAccessors));

        Assert.That(Reports("InlinableAccessors.Secret is not marked"), Is.Not.Empty);
        Assert.That(InlinableAccessors.Secret(new AccessedVM()), Is.EqualTo(0));
    }

    private sealed class CollectingListener : TraceListener
    {
        public List<string> Lines { get; } = [];
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { if (message is not null) lock (Lines) Lines.Add(message); }
    }
}

internal static class HookLog
{
    public static readonly List<string> Entries = [];
    public static void Clear() => Entries.Clear();
    public static void Add(string entry) => Entries.Add(entry);
}

// ---------------------------------------------------------------- notifications

public class NotifyingVM : ViewModel
{
    private int _count;
    private string _label = "";

    public int Count { get => _count; set { _count = value; OnPropertyChangedWithValue(value); } }
    public string Label { get => _label; set { _label = value; OnPropertyChangedWithValue(value); } }
}

[ViewModelMixin]
public class ListeningMixin : BaseViewModelMixin<NotifyingVM>
{
    public ListeningMixin(NotifyingVM vm) : base(vm) { }

    public List<string> Heard { get; } = [];

    protected override void OnViewModelPropertyChanged(string propertyName) => Heard.Add(propertyName);
}

// ---------------------------------------------------------------- overrides

public class OverriddenVM : ViewModel
{
    public void ExecuteDone() => HookLog.Add("body");

    public void SetCategory(int index, string name) => HookLog.Add($"body {index} {name}");
}

public class OverriddenDerivedVM : OverriddenVM { }

[ViewModelMixin]
public class FirstOverrideMixin : BaseViewModelMixin<OverriddenVM>
{
    public FirstOverrideMixin(OverriddenVM vm) : base(vm) { }

    [BUTRViewModelOverride(nameof(OverriddenVM.ExecuteDone))]
    private void ExecuteDone(Action original)
    {
        HookLog.Add("first");
        original();
    }
}

[ViewModelMixin]
public class SecondOverrideMixin : BaseViewModelMixin<OverriddenVM>
{
    public SecondOverrideMixin(OverriddenVM vm) : base(vm) { }

    [BUTRViewModelOverride(nameof(OverriddenVM.ExecuteDone))]
    private void ExecuteDone(Action original)
    {
        HookLog.Add("second");
        original();
    }
}

[ViewModelMixin]
public class BlockingOverrideMixin : BaseViewModelMixin<OverriddenVM>
{
    public BlockingOverrideMixin(OverriddenVM vm) : base(vm) { }

    [BUTRViewModelOverride(nameof(OverriddenVM.ExecuteDone))]
    private void ExecuteDone(Action original) => HookLog.Add("blocked");
}

[ViewModelMixin]
public class ArgumentOverrideMixin : BaseViewModelMixin<OverriddenVM>
{
    public ArgumentOverrideMixin(OverriddenVM vm) : base(vm) { }

    [BUTRViewModelOverride(nameof(OverriddenVM.SetCategory))]
    private void SetCategory(int index, string name, Action<int, string> original)
    {
        HookLog.Add($"override {index} {name}");
        original(index + 1, name + "!");
    }
}

[ViewModelMixin]
public class ThrowingOverrideMixin : BaseViewModelMixin<OverriddenVM>
{
    public ThrowingOverrideMixin(OverriddenVM vm) : base(vm) { }

    [BUTRViewModelOverride(nameof(OverriddenVM.ExecuteDone))]
    private void ExecuteDone(Action original) => throw new InvalidOperationException("from the override");
}

[ViewModelMixin]
public class MalformedOverrideMixin : BaseViewModelMixin<OverriddenVM>
{
    public MalformedOverrideMixin(OverriddenVM vm) : base(vm) { }

    // No original
    [BUTRViewModelOverride(nameof(OverriddenVM.ExecuteDone))]
    private void ExecuteDone() => HookLog.Add("malformed");

    // A method the ViewModel does not have
    [BUTRViewModelOverride("NoSuchMethod")]
    private void Missing(Action original) => HookLog.Add("missing");
}

// ---------------------------------------------------------------- accessors

public class AccessedBaseVM : ViewModel
{
    private string _inheritedSecret = "base";
}

public class AccessedVM : AccessedBaseVM
{
    private static int _staticCounter = 7;
    private int _secret;

    public int ReadSecret() => _secret;

    private string Combine(int number, string text) => $"{_secret}-{number}-{text}";

    private static int Twice(int value) => value * 2;
}

public static class Accessors
{
    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_secret")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref int Secret(AccessedVM instance) => throw new NotImplementedException();

    [BUTRUnsafeAccessor(BUTRAccessorKind.Method)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string Combine(AccessedVM instance, int number, string text) => throw new NotImplementedException();

    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_inheritedSecret")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref string InheritedSecret(AccessedVM instance) => throw new NotImplementedException();

    [BUTRUnsafeAccessor(BUTRAccessorKind.StaticField, typeof(AccessedVM), Name = "_staticCounter")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref int StaticCounter() => throw new NotImplementedException();

    [BUTRUnsafeAccessor(BUTRAccessorKind.StaticMethod, typeof(AccessedVM), Name = "Twice")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int StaticTwice(int value) => throw new NotImplementedException();

    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_type")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref Type ViewModelType(ViewModel instance) => throw new NotImplementedException();
}

public class BrokenAccessors
{
    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_noSuchField")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref int Missing(AccessedVM instance) => throw new NotImplementedException();

    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_secret")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ref string WrongType(AccessedVM instance) => throw new NotImplementedException();

    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_secret")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ref int NotStatic(AccessedVM instance) => throw new NotImplementedException();
}

public static class InlinableAccessors
{
    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_secret")]
    public static ref int Secret(AccessedVM instance) => throw new NotImplementedException();
}