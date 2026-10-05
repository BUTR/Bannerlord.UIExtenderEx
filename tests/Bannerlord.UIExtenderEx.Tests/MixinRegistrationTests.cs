using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.Components;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;
using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.MixinRegistration;

/// <summary>
/// Verifies static validation and registration logic performed by <c>Register</c> for mixin types before instantiation,
/// verifying diagnostic trace messages emitted for collisions and invalid configurations.
/// </summary>
public class MixinRegistrationTests
{
    private readonly List<UIExtender> _extenders = [];
    private CollectingListener _listener = null!;

    /// <summary>
    /// Applies ViewModel patches ahead of test execution to prevent Release JIT inlining on net472
    /// from compiling unpatched method bodies before registration occurs.
    /// </summary>
    [OneTimeSetUp]
    public void ApplyThePatchesBeforeAnyTestIsCompiled()
    {
        var extender = UIExtender.Create("TestModule.MixinRegistration.Patches");
        extender.Register([typeof(DataFirstMixin)]);
        extender.Deregister();
    }


    [SetUp]
    public void SetUp()
    {
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

    private UIExtender Register(string module, params Type[] types)
    {
        var extender = UIExtender.Create($"TestModule.MixinRegistration.{module}");
        _extenders.Add(extender);
        extender.Register(types);
        return extender;
    }

    private static ViewModelComponent ComponentOf(string module) =>
        UIExtender.GetAllRuntimes().Single(x => x.ModuleName == $"TestModule.MixinRegistration.{module}").ViewModelComponent;

    private IEnumerable<string> Reports(string fragment) => _listener.Lines.Where(x => x.Contains(fragment));

    // ---------------------------------------------------------------- the ViewModel a mixin extends

    /// <summary>Resolves target ViewModel types from the closed <see cref="BaseViewModelMixin{T}"/> inheritance link.</summary>
    [Test]
    public void TheViewModel_IsTakenFromTheClosedBaseViewModelMixin()
    {
        Assert.That(ViewModelComponent.GetViewModelType(typeof(DataFirstMixin)), Is.EqualTo(typeof(RegistrationVM)));
    }

    [Test]
    public void AMixinBehindASharedBase_IsAttachedToItsViewModel()
    {
        Register(nameof(AMixinBehindASharedBase_IsAttachedToItsViewModel), typeof(DataFirstMixin)).Enable();

        Assert.That(ViewModelMixins.Get<DataFirstMixin>(new RegistrationVM()), Is.Not.Null);
    }

    // ---------------------------------------------------------------- a ViewModel no instance can be

    [Test]
    public void AMixinOfAnAbstractViewModel_IsNotRegistered()
    {
        const string module = nameof(AMixinOfAnAbstractViewModel_IsNotRegistered);
        Register(module, typeof(AbstractHostMixin));

        Assert.That(ComponentOf(module).Mixins.ContainsKey(typeof(ViewModel)), Is.False);
        Assert.That(Reports($"mixin {typeof(AbstractHostMixin).FullName} is not registered"), Is.Not.Empty);
    }

    [Test]
    public void AMixinOfAnAbstractViewModelWithHandleDerived_IsRegisteredForTheDerivedTypes()
    {
        const string module = nameof(AMixinOfAnAbstractViewModelWithHandleDerived_IsRegisteredForTheDerivedTypes);
        Register(module, typeof(AbstractHostHandleDerivedMixin));

        Assert.That(ComponentOf(module).Mixins.ContainsKey(typeof(ConcreteRegistrationVM)), Is.True);
        Assert.That(Reports("is not registered"), Is.Empty);
    }

    // ---------------------------------------------------------------- collisions

    [Test]
    public void AMixinMemberNamedLikeTheViewModels_IsReported()
    {
        Register(nameof(AMixinMemberNamedLikeTheViewModels_IsReported), typeof(ReplacingMixin));

        Assert.That(Reports($"property 'Title' of mixin {typeof(ReplacingMixin).FullName} replaces {typeof(RegistrationVM).FullName}.Title"), Is.Not.Empty);
        Assert.That(Reports($"command 'ExecuteClose' of mixin {typeof(ReplacingMixin).FullName} replaces {typeof(RegistrationVM).FullName}.ExecuteClose"), Is.Not.Empty);
    }

    [Test]
    public void TwoMixinsOfOneModuleAddingOneName_AreReported()
    {
        const string module = nameof(TwoMixinsOfOneModuleAddingOneName_AreReported);
        Register(module, typeof(SharedNameMixinA), typeof(SharedNameMixinB));

        var reports = Reports("'Shared' is added to").ToList();
        Assert.That(reports, Has.Count.EqualTo(1));
        Assert.That(reports[0], Does.Contain(typeof(SharedNameMixinA).FullName).And.Contain(typeof(SharedNameMixinB).FullName));
    }

    [Test]
    public void TwoModulesAddingOneName_AreReportedWithBothModules()
    {
        Register("FirstModule", typeof(SharedNameMixinA));
        Register("SecondModule", typeof(SharedNameMixinB));

        var reports = Reports("'Shared' is added to").ToList();
        Assert.That(reports, Has.Count.EqualTo(1));
        Assert.That(reports[0], Does.Contain("TestModule.MixinRegistration.FirstModule").And.Contain("TestModule.MixinRegistration.SecondModule"));
    }

    [Test]
    public void DistinctNames_AreNotReported()
    {
        Register(nameof(DistinctNames_AreNotReported), typeof(DataFirstMixin), typeof(SharedNameMixinA));

        Assert.That(Reports("is added to"), Is.Empty);
        Assert.That(Reports("replaces"), Is.Empty);
    }

    // ---------------------------------------------------------------- what loads

    /// <summary>
    /// Verifies that <see cref="UIExtender.Register(Assembly)"/> isolates <see cref="ReflectionTypeLoadException"/> errors
    /// caused by missing external dependencies (such as DLC assemblies), registering valid mixins successfully.
    /// </summary>
    [Test]
    public void AnAssemblyWithATypeThatCannotLoad_RegistersTheRest()
    {
        using var workspace = new PrefabWorkspace();
        var references = PrefabReferenceSet.CollectPaths(workspace.WidgetFactory, typeof(RegistrationVM)).ToList();
        var compiler = new RoslynCompiler();
        var directory = Path.Combine(Path.GetTempPath(), "UIExtenderEx.MixinRegistration." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var dlcName = "MissingDlc" + Guid.NewGuid().ToString("N");
        byte[]? dlcImage = null;
        try
        {
            var dlc = compiler.Compile(dlcName, [new GeneratedSource("Dlc.cs", "public class DlcVM : TaleWorlds.Library.ViewModel { }")], references);
            dlcImage = dlc.Assembly;
            Assert.That(dlc.Success, Is.True, string.Join(Environment.NewLine, dlc.Errors));
            var dlcPath = Path.Combine(directory, dlcName + ".dll");
            File.WriteAllBytes(dlcPath, dlc.Assembly!);

            var mod = compiler.Compile("ModWithDlcMixin" + Guid.NewGuid().ToString("N"), [new GeneratedSource("Mod.cs", """
                using Bannerlord.UIExtenderEx.Attributes;
                using Bannerlord.UIExtenderEx.ViewModels;

                [ViewModelMixin]
                public class DlcMixin : BaseViewModelMixin<DlcVM> { public DlcMixin(DlcVM vm) : base(vm) { } }

                [ViewModelMixin]
                public class PresentMixin : BaseViewModelMixin<Bannerlord.UIExtenderEx.Tests.MixinRegistration.RegistrationVM>
                {
                    public PresentMixin(Bannerlord.UIExtenderEx.Tests.MixinRegistration.RegistrationVM vm) : base(vm) { }
                }
                """)], [.. references, dlcPath, typeof(MixinRegistrationTests).Assembly.Location]);
            Assert.That(mod.Success, Is.True, string.Join(Environment.NewLine, mod.Errors));

            // Load assembly from raw bytes without resolving unreferenced external dependencies.
            var assembly = Assembly.Load(mod.Assembly!);
            Assert.That(() => assembly.GetTypes(), Throws.InstanceOf<ReflectionTypeLoadException>(), "the premise: the DLC type cannot load");

            const string module = nameof(AnAssemblyWithATypeThatCannotLoad_RegistersTheRest);
            var extender = UIExtender.Create($"TestModule.MixinRegistration.{module}");
            _extenders.Add(extender);
            Assert.That(() => extender.Register(assembly), Throws.Nothing);

            Assert.That(ComponentOf(module).Mixins.TryGetValue(typeof(RegistrationVM), out var mixins), Is.True);
            Assert.That(mixins!.Select(x => x.Name), Is.EqualTo(new[] { "PresentMixin" }));
            Assert.That(Reports("did not load and is not registered"), Is.Not.Empty);
        }
        finally
        {
            // The mod assembly stays loaded for the rest of the run, and in older versions (v1.3.4) the game's own type scans
            // (TextureProviderFactory, from WidgetInfo.Refresh) throw on a type that cannot load: the DLC turns up now
            if (dlcImage is not null)
                ResolveFromNowOn(dlcName, dlcImage);
            try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>From the next type load on, <paramref name="name"/> resolves to <paramref name="image"/>.</summary>
    private static void ResolveFromNowOn(string name, byte[] image)
    {
        Assembly? loaded = null;
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            new AssemblyName(args.Name).Name == name ? loaded ??= Assembly.Load(image) : null;
    }

    private sealed class CollectingListener : TraceListener
    {
        public List<string> Lines { get; } = [];
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { if (message is not null) lock (Lines) Lines.Add(message); }
    }
}

public class RegistrationVM : ViewModel
{
    public string Title { get; set; } = "";
    public void ExecuteClose() { }
}

public abstract class AbstractRegistrationVM : ViewModel { }
public class ConcreteRegistrationVM : AbstractRegistrationVM { }

/// <summary>Represents a generic base mixin class whose first type parameter does not specify the target ViewModel.</summary>
public abstract class DataFirstSharedMixin<TData, TViewModel> : BaseViewModelMixin<TViewModel> where TViewModel : ViewModel
{
    protected DataFirstSharedMixin(TViewModel vm) : base(vm) { }
}

[ViewModelMixin]
public class DataFirstMixin : DataFirstSharedMixin<string, RegistrationVM>
{
    public DataFirstMixin(RegistrationVM vm) : base(vm) { }

    [DataSourceProperty] public string DataFirstLabel => "";
}

[ViewModelMixin]
public class AbstractHostMixin : BaseViewModelMixin<ViewModel>
{
    public AbstractHostMixin(ViewModel vm) : base(vm) { }
}

[ViewModelMixin(true)]
public class AbstractHostHandleDerivedMixin : BaseViewModelMixin<AbstractRegistrationVM>
{
    public AbstractHostHandleDerivedMixin(AbstractRegistrationVM vm) : base(vm) { }
}

[ViewModelMixin]
public class ReplacingMixin : BaseViewModelMixin<RegistrationVM>
{
    public ReplacingMixin(RegistrationVM vm) : base(vm) { }

    [DataSourceProperty] public string Title => "mixin";
    [DataSourceMethod] public void ExecuteClose() { }
}

[ViewModelMixin]
public class SharedNameMixinA : BaseViewModelMixin<RegistrationVM>
{
    public SharedNameMixinA(RegistrationVM vm) : base(vm) { }

    [DataSourceProperty] public int Shared => 1;
}

[ViewModelMixin]
public class SharedNameMixinB : BaseViewModelMixin<RegistrationVM>
{
    public SharedNameMixinB(RegistrationVM vm) : base(vm) { }

    [DataSourceProperty] public int Shared => 2;
}