using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;

using NUnit.Framework;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies dependency tracking semantics for compiled prefabs, ensuring recorded dependencies include only
/// inspected types and bound assembly references rather than the full compilation closure.
/// </summary>
public class PrefabDependenciesTests
{
    private const string Movie = "DependencyMovie";
    private const string MoviePrefab = "<Prefab><Window><TextWidget Text=\"@Title\" IsEnabled=\"@IsEnabled\" /></Window></Prefab>";

    private static string NameOf(string dependency) => dependency.Substring(0, dependency.IndexOf(':'));

    /// <summary>
    /// Verifies that inspecting a ViewModel type includes its entire inheritance hierarchy in the tracked dependencies.
    /// </summary>
    [Test]
    public void AnInspectedType_BringsItsBaseTypes()
    {
        var described = PrefabDependencies.DescribeInspected([typeof(CodegenTestVM)]);

        Assert.That(described.Keys, Does.Contain(typeof(CodegenTestVM).Assembly.GetName().Name));
        Assert.That(described.Keys, Does.Contain(typeof(TaleWorlds.Library.ViewModel).Assembly.GetName().Name), "ViewModel, its base");
        Assert.That(described.Keys, Does.Not.Contain("0Harmony"));
    }

    /// <summary>
    /// Verifies that generated movie assemblies depend exclusively on referenced types and widgets, omitting unused transitive compiler references.
    /// </summary>
    [Test]
    public void AGeneratedMovie_DependsOnWhatItUses_NotOnEverythingItWasHanded()
    {
        if (TestGame.Directory is null)
        {
            Assert.Ignore("No game installation; set BANNERLORD_GAME_DIR to run this.");
            return;
        }

        using var workspace = new PrefabWorkspace((Movie, MoviePrefab));
        var references = PrefabReferenceSet.CollectPaths(workspace.WidgetFactory, typeof(CodegenTestVM));
        System.Collections.Generic.List<GeneratedSource> sources;
        System.Collections.Generic.Dictionary<string, string> inspected;
        using (var recording = TypeDependencies.Begin())
        {
            TypeDependencies.Inspect(typeof(CodegenTestVM));
            sources = workspace.Generate(Movie, typeof(CodegenTestVM));
            inspected = PrefabDependencies.DescribeInspected(recording.Types);
        }
        var result = new RoslynCompiler().Compile("DependencyProbe", [.. sources, IgnoresAccessChecksSource.Create(references)], references);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));

        var composed = PrefabDependencies.Compose(inspected, result.UsedReferencePaths);
        var dependencies = composed.Select(NameOf).ToList();

        Assert.That(references.Select(Path.GetFileNameWithoutExtension), Does.Contain("0Harmony"), "test premise: the compilation is handed Harmony");
        foreach (var unused in new[] { "0Harmony", "MonoMod.Utils", "Mono.Cecil", "Newtonsoft.Json" })
            Assert.That(dependencies, Does.Not.Contain(unused), unused + " is handed to the compiler and used by nothing generated");
        Assert.That(dependencies, Does.Contain("TaleWorlds.GauntletUI"), "the widgets");
        Assert.That(dependencies, Does.Contain(typeof(CodegenTestVM).Assembly.GetName().Name), "the ViewModel");
        Assert.That(dependencies, Is.Ordered.Using((System.Collections.Generic.IComparer<string>) System.StringComparer.Ordinal), "the same build records the same list");
        // Verifies that assembly descriptions match currently loaded runtime assemblies to prevent false cache invalidation.
        Assert.That(PrefabDependencies.FindChanged(composed), Is.Null, "a build made just now holds");
    }

    /// <summary>
    /// Verifies that recorded dependencies report no changes when inspected assemblies remain unmodified.
    /// </summary>
    [Test]
    public void RecordedDependencies_HoldWhileNothingChanges()
    {
        var dependencies = PrefabDependencies.Compose(PrefabDependencies.DescribeInspected([typeof(CodegenTestVM)]), []);

        Assert.That(dependencies, Is.Not.Empty);
        Assert.That(PrefabDependencies.FindChanged(dependencies), Is.Null);
    }

    /// <summary>
    /// Verifies that dynamic or in-memory assemblies lacking physical file paths remain valid as long as they stay loaded.
    /// </summary>
    [Test]
    public void AnAssemblyWithoutAFile_HoldsWhileItIsLoaded()
    {
        var type = DefineType("UIExtenderEx.Tests.NoFile" + Guid.NewGuid().ToString("N"));
        var dependencies = PrefabDependencies.Compose(PrefabDependencies.DescribeInspected([type]), []);

        Assert.That(dependencies, Has.Some.StartsWith(type.Assembly.GetName().Name + ":"));
        Assert.That(PrefabDependencies.FindChanged(dependencies), Is.Null);
    }

    /// <summary>
    /// Verifies that when multiple assembly instances share identical simple names, inspecting either instance resolves correctly.
    /// </summary>
    [Test]
    public void TwoLoadedCopiesOfOneName_EitherHolds()
    {
        var name = "UIExtenderEx.Tests.Twice" + Guid.NewGuid().ToString("N");
        var first = DefineType(name);
        var second = DefineType(name);

        Assert.That(first.Assembly, Is.Not.SameAs(second.Assembly), "test premise");
        Assert.That(PrefabDependencies.FindChanged(PrefabDependencies.Compose(PrefabDependencies.DescribeInspected([second]), [])), Is.Null);
        Assert.That(PrefabDependencies.FindChanged(PrefabDependencies.Compose(PrefabDependencies.DescribeInspected([first]), [])), Is.Null);
    }

    private static Type DefineType(string assemblyName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule(assemblyName).DefineType("Probe", TypeAttributes.Public).CreateTypeInfo()!.AsType();
    }

    /// <summary>
    /// Verifies that modified fingerprints or missing assemblies are correctly identified in change diagnostics.
    /// </summary>
    [Test]
    public void AChangedOrMissingDependency_IsNamed()
    {
        var name = typeof(CodegenTestVM).Assembly.GetName().Name!;

        Assert.That(PrefabDependencies.FindChanged([name + ":0123456789abcdef0123456789abcdef"]), Does.StartWith(name + " "));
        Assert.That(PrefabDependencies.FindChanged(["Some.Assembly.Nobody.Has:0123"]), Does.Contain("missing"));
    }
}
