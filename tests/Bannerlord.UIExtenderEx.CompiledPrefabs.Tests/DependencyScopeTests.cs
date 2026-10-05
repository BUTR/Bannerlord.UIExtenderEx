using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies dependency scope resolution and fingerprint invalidation across child ViewModels, list items, and mixins.
/// </summary>
public class DependencyScopeTests
{
    public class ScopeRootVM : ViewModel
    {
        public ScopeChildVM Child => null!;
        public MBBindingList<ScopeItemVM> Items => null!;
        public ScopeRootVM Cycle => this;
    }
    public class ScopeChildVM : ViewModel { }
    public class ScopeItemVM : ViewModel { }
    public class ScopeOtherVM : ViewModel { }

    [ViewModelMixin]
    public class OtherMixin : BaseViewModelMixin<ScopeOtherVM>
    {
        public OtherMixin(ScopeOtherVM vm) : base(vm) { }
        [DataSourceProperty] public string Text => "other";
    }
    [ViewModelMixin]
    public class ChildMixin : BaseViewModelMixin<ScopeChildVM>
    {
        public ChildMixin(ScopeChildVM vm) : base(vm) { }
        [DataSourceProperty] public ScopeOtherVM Extra => null!;
    }
    [ViewModelMixin]
    public class ItemMixin : BaseViewModelMixin<ScopeItemVM>
    {
        public ItemMixin(ScopeItemVM vm) : base(vm) { }
        [DataSourceProperty] public string Text => "item";
    }

    [Test]
    public void UnreachableMixinsDoNotChangeTheFingerprint_ButChildrenAndListItemsDo()
    {
        var extender = UIExtender.Create("TestModule.DependencyScope");
        extender.Register([typeof(OtherMixin), typeof(ChildMixin), typeof(ItemMixin)]);
        try
        {
            using var workspace = new PrefabWorkspace(("ScopeMovie", PrefabWorkspace.PlainPrefab));
            string? Compute() => TestFingerprint.Compute(workspace.WidgetFactory, "ScopeMovie", typeof(ScopeRootVM));
            var original = Compute();
            extender.Enable(typeof(OtherMixin));
            Assert.That(Compute(), Is.EqualTo(original), "an unrelated target does not affect this movie");

            extender.Enable(typeof(ChildMixin));
            var child = Compute();
            Assert.That(child, Is.Not.EqualTo(original));
            Assert.That(MixinRegistrations.ForRoot(typeof(ScopeRootVM)).Select(x => x.Key), Does.Contain(typeof(ScopeOtherVM)),
                "a mixin can expose another ViewModel with its own mixins");

            extender.Enable(typeof(ItemMixin));
            Assert.That(Compute(), Is.Not.EqualTo(child), "list-element mixins remain dependencies");
            extender.Disable(typeof(ChildMixin));
            extender.Disable(typeof(ItemMixin));
            Assert.That(Compute(), Is.EqualTo(original));
        }
        finally { extender.Deregister(); }
    }

    [Test]
    public void DependencyMetadata_IsSelectedBeforeTheAssemblyLoads_AndStaysTheSameAfterwards()
    {
        var compiler = new RoslynCompiler();
        var suffix = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), "PrefabReferences-" + suffix);
        Directory.CreateDirectory(directory);
        try
        {
            var dependencyName = "Dependency" + suffix;
            var dependencyPath = Path.Combine(directory, dependencyName + ".dll");
            var dependency = compiler.Compile(dependencyName, [new GeneratedSource("dep.cs", "public class DependencyValue { }")],
                [typeof(object).Assembly.Location]);
            Assert.That(dependency.Success, Is.True, string.Join("\n", dependency.Errors));
            File.WriteAllBytes(dependencyPath, dependency.Assembly!);

            var rootName = "Root" + suffix;
            var rootPath = Path.Combine(directory, rootName + ".dll");
            var root = compiler.Compile(rootName, [new GeneratedSource("root.cs", "public class RootValue { public DependencyValue Value; }")],
                [typeof(object).Assembly.Location, dependencyPath]);
            Assert.That(root.Success, Is.True, string.Join("\n", root.Errors));
            File.WriteAllBytes(rootPath, root.Assembly!);
            var assembly = Assembly.LoadFrom(rootPath);
            bool DependencyLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name == dependencyName);

            Assert.That(DependencyLoaded(), Is.False);
            var before = PrefabReferenceSet.Select([assembly]);
            Assert.That(before.Select(x => x.Name), Does.Contain(dependencyName));
            Assert.That(DependencyLoaded(), Is.False, "metadata discovery must not load the dependency");

            Assembly.LoadFrom(dependencyPath);
            Assert.That(PrefabReferenceSet.Select([assembly]), Is.EqualTo(before));
        }
        finally
        {
            // Catch and suppress file lock exceptions when deleting assemblies held by the CLR.
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
