using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests candidate dependency resolution when multiple assemblies with identical names exist on disk,
/// simulating modded game environments where sub-modules deploy custom copies of framework libraries.
/// <para>
/// Ensures the resolver selects an optimal candidate rather than dropping ambiguous references. Generated
/// prefab code binds to <c>Widget.Vector2PropertyChanged</c>, whose delegate references <c>System.Numerics.Vector2</c>
/// via <c>TaleWorlds.Library</c>. Omitting <c>System.Numerics.Vectors</c> from the reference set triggers CS0012
/// across all generated GauntletUI movies.
/// </para>
/// </summary>
public class ReferenceResolutionTests
{
    private static PrefabAssemblyReference.Dependency Asked(string name, string version) => new(name, new Version(version));

    private static PrefabAssemblyReference Candidate(string path, string version, string mvid, bool definesVector2 = false) =>
        new("Some.Assembly", path, mvid, new Version(version), [], definesVector2);

    /// <summary>
    /// Verifies that the reference resolver surfaces missing disk images for loaded assemblies as a typed
    /// <see cref="PrefabReferenceException"/>, rather than unhandled I/O exceptions, handling scenarios where
    /// external mod managers rename loaded binaries during active runtime execution.
    /// </summary>
    [Test]
    public void ALoadedAssemblyWhoseFileIsGone_IsReportedAsUnreadable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UIExtenderEx-gone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = typeof(PrefabNames).Assembly.Location;
        var path = Path.Combine(directory, Path.GetFileName(source));
        File.Copy(source, path);
        var loaded = Assembly.LoadFile(path);
        Assume.That(loaded.Location, Is.EqualTo(path), "test premise: the copy was loaded, not the assembly already in the process");
        File.Move(path, path + ".old");

        var thrown = Assert.Throws<PrefabReferenceException>(() => PrefabReferenceSet.Select([loaded]));
        Assert.That(thrown!.AssemblyName, Is.EqualTo(loaded.GetName().Name));
        Assert.That(thrown.InnerException, Is.InstanceOf<IOException>());
    }

    [Test]
    public void NoCandidates_ResolveToNothing()
    {
        Assert.That(PrefabReferenceSet.Choose([], Asked("Some.Assembly", "1.0.0.0")), Is.Null);
    }

    [Test]
    public void OneCandidate_IsTaken()
    {
        var only = Candidate(@"C:\a\Some.Assembly.dll", "1.0.0.0", "aaaa");

        Assert.That(PrefabReferenceSet.Choose([only], Asked("Some.Assembly", "9.9.9.9")), Is.SameAs(only),
            "the version asked for does not have to match: the file is the only one there is");
    }

    [Test]
    public void SeveralCopiesOfOneBuild_AreOneAnswer()
    {
        var first = Candidate(@"C:\a\Some.Assembly.dll", "1.0.0.0", "aaaa");
        var second = Candidate(@"C:\b\Some.Assembly.dll", "1.0.0.0", "aaaa");

        Assert.That(PrefabReferenceSet.Choose([first, second], Asked("Some.Assembly", "1.0.0.0")), Is.SameAs(first));
    }

    /// <summary>
    /// Verifies that candidate selection prioritizes the exact assembly identity and version requested by the caller.
    /// </summary>
    [Test]
    public void DifferentBuilds_ResolveToTheVersionThatWasAskedFor()
    {
        var moduleCopy = Candidate(@"C:\Modules\Other\bin\Some.Assembly.dll", "4.1.4.0", "aaaa");
        var runtimeCopy = Candidate(@"C:\game\mono\Some.Assembly.dll", "4.1.3.0", "bbbb");

        var chosen = PrefabReferenceSet.Choose([moduleCopy, runtimeCopy], Asked("Some.Assembly", "4.1.3.0"));

        Assert.That(chosen, Is.SameAs(runtimeCopy), "the one whose identity the code was compiled against");
    }

    /// <summary>
    /// Verifies that candidate resolution falls back to deterministic search order when version matching produces no exact hit,
    /// ensuring the compiler receives a candidate assembly rather than dropping the reference.
    /// </summary>
    [Test]
    public void DifferentBuildsAndNoVersionMatch_StillResolveToSomething()
    {
        var first = Candidate(@"C:\a\Some.Assembly.dll", "4.1.4.0", "aaaa");
        var second = Candidate(@"C:\b\Some.Assembly.dll", "4.0.0.0", "bbbb");

        var chosen = PrefabReferenceSet.Choose([first, second], Asked("Some.Assembly", "4.1.3.0"));

        Assert.That(chosen, Is.Not.Null, "dropping the reference is what broke every compiled prefab in the game");
        Assert.That(chosen, Is.SameAs(first), "search order decides, nearest to the referencing assembly first");
    }

    [Test]
    public void TwoBuildsAskedForTheSameVersion_TakeTheFirstOfThose()
    {
        var far = Candidate(@"C:\z\Some.Assembly.dll", "9.9.9.9", "cccc");
        var first = Candidate(@"C:\a\Some.Assembly.dll", "4.1.3.0", "aaaa");
        var second = Candidate(@"C:\b\Some.Assembly.dll", "4.1.3.0", "bbbb");

        Assert.That(PrefabReferenceSet.Choose([far, first, second], Asked("Some.Assembly", "4.1.3.0")), Is.SameAs(first));
    }

    // ---------------------------------------------------------------- Live game installation tests

    /// <summary>
    /// Verifies that the resolved compilation reference set contains <c>System.Numerics.Vector2</c> from a live game installation,
    /// ensuring generated widget property subscriptions compile without missing vector type metadata.
    /// </summary>
    [Test]
    public void TheReferenceSet_CarriesVector2()
    {
        if (TestGame.Directory is null)
        {
            Assert.Ignore("No game installation; set BANNERLORD_GAME_DIR to run this.");
            return;
        }

        var paths = PrefabReferenceSet.CollectPaths(null, null);
        var names = paths.Select(Path.GetFileNameWithoutExtension).ToList();

        Assert.That(names, Does.Contain("System.Numerics.Vectors").Or.Contain("System.Numerics"),
            "the generated widget subscription for Vector2PropertyChanged cannot compile without it");
    }

    /// <summary>
    /// Verifies that the dependency graph traversal terminates at framework boundaries, preventing recursive import
    /// of unused Base Class Library (BCL) assemblies (such as <c>System.Design</c> or <c>System.DirectoryServices</c>)
    /// that would degrade Roslyn compilation throughput.
    /// </summary>
    [Test]
    public void TheReferenceSet_StopsAtTheFramework()
    {
        if (TestGame.Directory is null)
        {
            Assert.Ignore("No game installation; set BANNERLORD_GAME_DIR to run this.");
            return;
        }

        var names = PrefabReferenceSet.CollectPaths(null, null).Select(Path.GetFileNameWithoutExtension).ToList();

        // Excludes System.Web facade dependencies while asserting that purely downstream framework libraries are pruned.
        foreach (var reachableOnlyThroughTheFramework in new[]
                 { "System.Design", "System.DirectoryServices", "System.EnterpriseServices", "System.Deployment" })
        {
            Assert.That(names, Does.Not.Contain(reachableOnlyThroughTheFramework),
                "nothing the generator emits can reach it, and importing it is paid on every compile");
        }
    }

    /// <summary>
    /// Verifies that assembly resolution traverses <c>netstandard.dll</c> type forwarding facades to include underlying
    /// XML assemblies required by <c>WidgetExtensions.SetWidgetAttributeFromString</c> overload resolution.
    /// </summary>
    [Test]
    public void WhatNetstandardForwards_IsReferenced()
    {
        if (TestGame.Directory is null)
        {
            Assert.Ignore("No game installation; set BANNERLORD_GAME_DIR to run this.");
            return;
        }

        var references = PrefabReferenceSet.ToPaths(PrefabReferenceSet.Select([typeof(WidgetPrefab).Assembly, typeof(TaleWorlds.GauntletUI.BaseTypes.Widget).Assembly]));
        var probe = new GeneratedSource("Probe.gen.cs", """
            public static class Probe
            {
                public static void Run(TaleWorlds.GauntletUI.BaseTypes.Widget widget) =>
                    TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString(widget, @"Property", @"Child", widget.Context.BrushFactory, widget.Context.SpriteData, null, null, null, null, null);
            }
            """);

        var result = new RoslynCompiler().Compile("Probe", [probe], references);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    /// <summary>
    /// Verifies that dependency queries originating from <c>TaleWorlds.Library</c> for <c>System.Numerics.Vectors</c>
    /// resolve to a concrete candidate on disk across candidate search directories.
    /// </summary>
    [Test]
    public void WhatTaleWorldsAsksForVector2_Resolves()
    {
        if (TestGame.Directory is null)
        {
            Assert.Ignore("No game installation; set BANNERLORD_GAME_DIR to run this.");
            return;
        }

        var gauntlet = Path.GetDirectoryName(typeof(WidgetPrefab).Assembly.Location)!;
        var library = PrefabAssemblyReference.Read(Path.Combine(gauntlet, "TaleWorlds.Library.dll"));
        var asked = library.Dependencies.FirstOrDefault(x => x.Name == "System.Numerics.Vectors");
        Assert.That(asked, Is.Not.Null, "test premise: TaleWorlds.Library binds Vector2 through System.Numerics.Vectors");

        var directories = new List<string>
        {
            gauntlet,
            Path.Combine(gauntlet, "mono", "lib", "mono", "4.5"),
            AppDomain.CurrentDomain.BaseDirectory,
            RuntimeEnvironment.GetRuntimeDirectory(),
        };
        var candidates = directories.Select(x => Path.Combine(x, asked!.Name + ".dll")).Where(File.Exists)
            .Select(PrefabAssemblyReference.TryRead).OfType<PrefabAssemblyReference>()
            .Where(x => x.Name == asked!.Name).ToList();

        Assert.That(candidates, Is.Not.Empty, "test premise: the installation has at least one copy");
        Assert.That(PrefabReferenceSet.Choose(candidates, asked!), Is.Not.Null,
            "several copies with different contents must still resolve to one file, not to nothing");
    }
}
