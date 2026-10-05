using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Tests.Utils;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies detection of module prefab file overrides in <see cref="PrefabOverrideRegistry"/>.
/// <para>
/// When a mod ships an XML prefab file that overrides a Native game prefab, compiled prefabs generated
/// against base game XML must yield to the overridden file to preserve mod customizations.
/// </para>
/// </summary>
public class PrefabOverrideRegistryTests
{
    private readonly List<string> _roots = [];

    [TearDown]
    public void TearDown()
    {
        PrefabOverrideRegistry.ClearCache();
        foreach (var root in _roots)
        {
            try { Directory.Delete(root, true); }
            catch (Exception) { /* ignore */ }
        }
        _roots.Clear();
    }

    /// <summary>
    /// Creates a simulated module directory containing specified prefab files within a <c>GUI/Prefabs</c> structure.
    /// </summary>
    private string CreateModule(params string[] prefabNames)
    {
        var root = Path.Combine(Path.GetTempPath(), "UIExtenderEx-override-" + Guid.NewGuid().ToString("N"));
        _roots.Add(root);
        var directory = Path.Combine(root, "GUI", "Prefabs");
        Directory.CreateDirectory(directory);
        foreach (var name in prefabNames)
            File.WriteAllText(Path.Combine(directory, name + ".xml"), PrefabWorkspace.PlainPrefab);
        return root;
    }

    private static WidgetFactory CreateFactory(params string[] moduleRoots)
    {
        var depot = ResourceDepotUtils.Create() ?? throw new InvalidOperationException("ResourceDepot constructor not found");
        foreach (var root in moduleRoots)
            depot.AddLocation(root.Replace(Path.DirectorySeparatorChar, (char) 47) + "/", "GUI/");
        depot.CollectResources();

        // Avoids calling Initialize() so the registry inspects raw ResourceDepot locations before path deduplication occurs.
        return new WidgetFactory(depot, "Prefabs");
    }

    /// <summary>
    /// Verifies that a prefab supplied by a single module is not reported as overridden.
    /// </summary>
    [Test]
    public void APrefabOnlyOneModuleHas_IsNotAnOverride()
    {
        var factory = CreateFactory(CreateModule("Alpha", "Beta"), CreateModule("Gamma"));

        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Alpha", "Beta", "Gamma"]), Is.False);
    }

    /// <summary>
    /// Verifies that prefab names containing dots are matched against generated identifier representations using underscores.
    /// </summary>
    [Test]
    public void ADottedPrefabName_IsAnsweredTheWayTheGeneratorSpellsIt()
    {
        var factory = CreateFactory(CreateModule("Lobby.Armory"), CreateModule("Lobby.Armory"));

        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Lobby_Armory"]), Is.True);
    }

    /// <summary>
    /// Verifies that distinct filenames differing by separators do not collide as overrides.
    /// </summary>
    [Test]
    public void TwoModulesSpellingANameDifferently_IsNotAnOverride()
    {
        var factory = CreateFactory(CreateModule("Lobby.Armory"), CreateModule("Lobby_Armory"));

        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Lobby_Armory"]), Is.False,
            "two different files, neither of which replaces the other");
    }

    /// <summary>
    /// Verifies that when two modules ship a file with the same relative path, the prefab is detected as overridden.
    /// </summary>
    [Test]
    public void APrefabTwoModulesShip_IsAnOverride()
    {
        var factory = CreateFactory(CreateModule("Alpha", "Beta"), CreateModule("Beta"));

        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Beta"]), Is.True);
        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Alpha"]), Is.False, "the module's other prefabs are untouched");
    }

    /// <summary>
    /// Verifies that overrides of descendant prefabs embedded anywhere in a movie tree are detected.
    /// </summary>
    [Test]
    public void AnOverride_IsFoundThroughAnyNameOfTheMoviesTree()
    {
        // Passes inlined descendant prefabs to verify that nested overrides invalidate the embedding movie.
        var factory = CreateFactory(CreateModule("Screen", "NestedPanel"), CreateModule("NestedPanel"));

        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Screen", "NestedPanel"]), Is.True);
    }

    /// <summary>
    /// Verifies that instantiating a new factory recalculates overrides rather than reading stale cached results.
    /// </summary>
    [Test]
    public void TheSetIsRecomputed_ForANewWidgetFactory()
    {
        var withoutOverride = CreateFactory(CreateModule("Alpha"));
        Assert.That(PrefabOverrideRegistry.ContainsOverridden(withoutOverride, ["Alpha"]), Is.False);

        // Verifies that instantiating a new factory recalculates overrides rather than reading stale cached results.
        var withOverride = CreateFactory(CreateModule("Alpha"), CreateModule("Alpha"));
        Assert.That(PrefabOverrideRegistry.ContainsOverridden(withOverride, ["Alpha"]), Is.True);
    }

    /// <summary>
    /// Verifies that factories containing no overridden prefabs short-circuit without enumerating queried prefab names.
    /// </summary>
    [Test]
    public void AFactoryWithNothingReplaced_AnswersWithoutTouchingTheNames()
    {
        var factory = CreateFactory(CreateModule("Alpha"));

        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, Enumerated()), Is.False);

        static IEnumerable<string> Enumerated()
        {
            Assert.Fail("the names were enumerated although nothing is replaced");
            yield break;
        }
    }

    /// <summary>
    /// Verifies that files with matching prefix but non-XML extensions are excluded from duplicate prefab counts.
    /// </summary>
    [Test]
    public void ADisabledCopyNextToAPrefab_IsNotTheSamePrefabTwice()
    {
        // Ensures exact extension matching so non-xml files (e.g., .xml_dev or .xml_mod) are not treated as duplicate prefabs.
        var module = CreateModule("Alpha");
        File.WriteAllText(Path.Combine(module, "GUI", "Prefabs", "Alpha.xml_dev"), PrefabWorkspace.PlainPrefab);
        File.WriteAllText(Path.Combine(module, "GUI", "Prefabs", "Alpha.xml_mod"), PrefabWorkspace.PlainPrefab);

        var factory = CreateFactory(module, CreateModule("Beta"));

        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Alpha"]), Is.False);
    }

    /// <summary>
    /// Verifies that clearing the cache purges previous calculations and successfully recomputes overrides.
    /// </summary>
    [Test]
    public void ClearCache_ForgetsWhatWasComputed()
    {
        var factory = CreateFactory(CreateModule("Alpha"), CreateModule("Alpha"));
        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Alpha"]), Is.True);

        PrefabOverrideRegistry.ClearCache();

        Assert.That(PrefabOverrideRegistry.ContainsOverridden(factory, ["Alpha"]), Is.True, "recomputed to the same answer");
    }
}
