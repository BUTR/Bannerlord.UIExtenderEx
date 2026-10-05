using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;

using NUnit.Framework;

using System;
using System.IO;
using System.Linq;
using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies compilation fingerprint computation, closure discovery, and cache invalidation over prefab trees.
/// <para>
/// Captures XML hashes via runtime loading patches to guarantee deterministic cache keying across prefab modifications.
/// </para>
/// </summary>
public class PrefabFingerprintTests
{
    private const string Movie = "FingerprintMovie";

    private const string MoviePrefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <FingerprintNested />
        <FingerprintInherited />
        <ListPanel DataSource=""{Items}"">
          <ItemTemplate>
            <FingerprintItem />
          </ItemTemplate>
        </ListPanel>
      </Children>
    </Widget>
  </Window>
</Prefab>";

    private const string InheritedPrefab = "<Prefab><Window><FingerprintDeep /></Window></Prefab>";

    private PrefabWorkspace? _workspace;
    private UIExtender? _extender;

    [SetUp]
    public void SetUp()
    {
        // Applies runtime patches, including the patch that records prefab XML hashes during factory load.
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.Fingerprint");
        _extender.Register([typeof(CodegenTestVMMixin)]);

        _workspace = new PrefabWorkspace(
            (Movie, MoviePrefab),
            ("FingerprintNested", PrefabWorkspace.PlainPrefab),
            ("FingerprintInherited", InheritedPrefab),
            ("FingerprintDeep", PrefabWorkspace.PlainPrefab),
            ("FingerprintItem", PrefabWorkspace.PlainPrefab),
            ("FingerprintUnrelated", PrefabWorkspace.PlainPrefab));
    }

    [TearDown]
    public void TearDown()
    {
        _extender?.Deregister();
        _workspace?.Dispose();
    }

    /// <summary>
    /// Verifies that type descriptions in fingerprint keys omit assembly version qualifiers to allow compatible assembly upgrades.
    /// </summary>
    [Test]
    public void ATypeInTheKey_IsNamedWithoutItsAssemblysVersion()
    {
        var core = typeof(string).Assembly.GetName().Name;
        var generic = typeof(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int[]>>);

        Assert.That(PrefabFingerprint.DescribeType(typeof(CodegenTestVM)), Is.EqualTo($"{typeof(CodegenTestVM).FullName}, {typeof(CodegenTestVM).Assembly.GetName().Name}"));
        Assert.That(PrefabFingerprint.DescribeType(generic), Is.EqualTo(
            $"System.Collections.Generic.Dictionary`2[[System.String, {core}],[System.Collections.Generic.List`1[[System.Int32[], {core}]], {core}]], {core}"));
        Assert.That(PrefabFingerprint.DescribeType(generic), Does.Not.Contain("Version="));
    }

    [Test]
    public void Closure_ContainsNestedInheritedAndItemTemplatePrefabs()
    {
        var closure = TestFingerprint.CollectClosure(_workspace!.WidgetFactory, Movie);

        Assert.That(closure, Is.EquivalentTo(new[] { Movie, "FingerprintNested", "FingerprintInherited", "FingerprintDeep", "FingerprintItem" }));
    }

    [Test]
    public void Closure_OfUnknownMovie_IsEmpty()
    {
        Assert.That(TestFingerprint.CollectClosure(_workspace!.WidgetFactory, "DoesNotExist"), Is.Empty);
        Assert.That(Compute(), Is.Not.Null);
        Assert.That(TestFingerprint.Compute(_workspace.WidgetFactory, "DoesNotExist", typeof(CodegenTestVM)), Is.Null);
    }

    [Test]
    public void Fingerprint_IsStable()
    {
        Assert.That(Compute(), Is.EqualTo(Compute()));
    }

    /// <summary>
    /// Verifies that shipped assemblies are hashed by file build content whereas framework assemblies are described by version identity.
    /// </summary>
    [Test]
    public void Assemblies_AreHashedByBuildWhenShipped_AndByIdentityOtherwise()
    {
        var game = Path.Combine(Path.GetTempPath(), "Mount & Blade II Bannerlord");
        var workshopModule = Path.Combine(Path.GetTempPath(), "workshop", "content", "261550", "123");
        string[] shipped = [game, workshopModule];
        PrefabAssemblyReference At(string path) => new(Path.GetFileNameWithoutExtension(path), path, "0123456789abcdef", new Version(4, 0, 0, 0), [], false);

        Assert.That(PrefabFingerprint.DescribeAssembly(At(Path.Combine(game, "bin", "Win64_Shipping_Client", "TaleWorlds.Library.dll")), shipped), Is.EqualTo("0123456789abcdef"));
        Assert.That(PrefabFingerprint.DescribeAssembly(At(Path.Combine(game, "bin", "Win64_Shipping_Client", "System.Numerics.Vectors.dll")), shipped), Is.EqualTo("0123456789abcdef"), "a framework assembly the game ships is the game's file");
        Assert.That(PrefabFingerprint.DescribeAssembly(At(Path.Combine(workshopModule, "bin", "Win64_Shipping_Client", "Some.Mod.dll")), shipped), Is.EqualTo("0123456789abcdef"));
        Assert.That(PrefabFingerprint.DescribeAssembly(At(Path.Combine(Path.GetTempPath(), "Windows", "Microsoft.NET", "mscorlib.dll")), shipped), Is.EqualTo("v4.0.0.0"));
        Assert.That(PrefabFingerprint.DescribeAssembly(At(game + " Other" + Path.DirectorySeparatorChar + "Lookalike.dll"), shipped), Is.EqualTo("v4.0.0.0"), "a folder that merely starts with the game's name");
    }

    [Test]
    public void Fingerprint_ChangesWhenAPrefabInTheClosureChanges()
    {
        var before = Compute();

        // Change the actual input and request a reparse, even while an existing movie holds its old copy.
        _workspace!.WidgetFactory.GetCustomType("FingerprintDeep");
        var component = UIExtender.GetRuntimeFor("TestModule.CompiledPrefabs.Fingerprint")!.PrefabComponent;
        component.RegisterPatch("FingerprintDeep", typeof(PrefabFingerprintTests), (XmlDocument document) =>
            document.LoadXml("<Prefab><Window><Widget Id=\"changed\" /></Window></Prefab>"));
        component.Enable(typeof(PrefabFingerprintTests));
        WidgetFactoryManager.ReloadOnNextUse(_workspace.WidgetFactory, ["FingerprintDeep"]);

        Assert.That(Compute(), Is.Not.EqualTo(before));
    }

    [Test]
    public void Fingerprint_FollowsAReparse_OnceNothingHoldsThePrefab()
    {
        // Releases acquired prefabs after computing the fingerprint so subsequent parses record updated XML definitions.
        var before = Compute();
        var stale = new XmlDocument();
        stale.LoadXml("<Prefab><Window><Widget Id=\"stale\" /></Window></Prefab>");
        PrefabXmlRegistry.Record("FingerprintDeep", stale);

        Assert.That(Compute(), Is.EqualTo(before));
    }

    [Test]
    public void Fingerprint_IgnoresPrefabsOutsideTheClosure()
    {
        var before = Compute();

        var changed = new XmlDocument();
        changed.LoadXml("<Prefab><Window><Widget Id=\"changed\" /></Window></Prefab>");
        PrefabXmlRegistry.Record("FingerprintUnrelated", changed);

        Assert.That(Compute(), Is.EqualTo(before));
    }

    [Test]
    public void Fingerprint_DependsOnTheViewModelType()
    {
        Assert.That(Compute(), Is.Not.EqualTo(TestFingerprint.Compute(_workspace!.WidgetFactory, Movie, typeof(CodegenOtherVM))));
    }

    [Test]
    public void Fingerprint_ChangesWhenAMixinIsEnabled()
    {
        var disabled = Compute();
        _extender!.Enable();
        var enabled = Compute();
        _extender.Disable();

        Assert.That(enabled, Is.Not.EqualTo(disabled));
        Assert.That(Compute(), Is.EqualTo(disabled));
    }

    [Test]
    public void Fingerprint_IsNullWhenAPrefabHashIsMissing()
    {
        Assert.That(Compute(), Is.Not.Null);

        // Keeps the prefab parsed in the factory to simulate a missing XML hash without an automatic reparse.
        _workspace!.WidgetFactory.GetCustomType("FingerprintDeep");
        PrefabXmlRegistry.Clear();
        PrefabFingerprint.ClearCache();

        var fingerprint = PrefabFingerprint.Compute(_workspace.WidgetFactory, Movie, typeof(CodegenTestVM), out _, out var failure);
        Assert.That(fingerprint, Is.Null, "a prefab without a recorded hash must never be matched against a cached assembly");

        // Diagnostic failure text names the unrecorded prefab to distinguish transient loads from permanent errors.
        Assert.That(failure, Does.Contain("FingerprintDeep"));
    }

    /// <summary>
    /// Verifies that fingerprint keys omit assembly references, delegating assembly validation to post-build dependency records.
    /// </summary>
    [Test]
    public void Fingerprint_NamesNoAssembly()
    {
        PrefabFingerprint.Compute(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM), out var inputs);

        Assert.That(inputs, Does.Not.Contain("assembly:"));
    }

    private string? Compute() => TestFingerprint.Compute(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM));

    [Test]
    public void Compute_LeavesNoPrefabPinnedInTheFactory()
    {
        // Verifies that computing the fingerprint releases all inspected prefabs to allow future patch applications.
        Assert.That(Compute(), Is.Not.Null);

        Assert.That(_workspace!.LivePrefabNames, Is.Empty);
    }

    [Test]
    public void ReloadOnNextUse_ForgetsTheParsedCopy_WithoutTouchingOtherPrefabs()
    {
        var factory = _workspace!.WidgetFactory;
        factory.GetCustomType("FingerprintDeep");
        factory.GetCustomType("FingerprintNested");
        Assert.That(_workspace.LivePrefabNames, Is.EquivalentTo(new[] { "FingerprintDeep", "FingerprintNested" }));

        WidgetFactoryManager.ReloadOnNextUse(factory, ["FingerprintDeep"]);

        Assert.That(_workspace.LivePrefabNames, Is.EquivalentTo(new[] { "FingerprintNested" }));
        Assert.That(factory.GetCustomType("FingerprintDeep"), Is.Not.Null, "parsed again on the next use");
    }

    [Test]
    public void Fingerprint_FollowsAChangedPrefab_AfterReloadOnNextUse()
    {
        // Simulates modified prefab content to verify that ReloadOnNextUse triggers fresh parsing and updates fingerprint hashes.
        using var workspace = new PrefabWorkspace(("FingerprintDynamicMovie", "<Prefab><Window><FingerprintDynamic /></Window></Prefab>"));
        var factory = workspace.WidgetFactory;
        var content = "<Prefab><Window><Widget Id=\"v1\" /></Window></Prefab>";
        if (!WidgetFactoryManager.IsRegisteredCustomType("FingerprintDynamic"))
        {
            WidgetFactoryManager.Register("FingerprintDynamic", () =>
            {
                var document = new XmlDocument();
                document.LoadXml(content);
                PrefabXmlRegistry.Record("FingerprintDynamic", document);
                return WidgetPrefabPatch.LoadFromDocument(factory.PrefabExtensionContext, factory.WidgetAttributeContext, "FingerprintDynamic.xml", document);
            });
        }
        string? Fingerprint() => TestFingerprint.Compute(factory, "FingerprintDynamicMovie", typeof(CodegenTestVM));

        factory.GetCustomType("FingerprintDynamic");
        var before = Fingerprint();
        Assert.That(before, Is.Not.Null);

        content = "<Prefab><Window><Widget Id=\"v2\" /></Window></Prefab>";
        Assert.That(Fingerprint(), Is.EqualTo(before), "the held copy is not parsed again by itself");

        WidgetFactoryManager.ReloadOnNextUse(factory, ["FingerprintDynamic"]);

        Assert.That(Fingerprint(), Is.Not.EqualTo(before), "the next use parsed the current content");
    }

    /// <summary>
    /// Verifies that rebuilding a prefab section for an unchanged tree reuses cached assembly reference sets.
    /// <para>
    /// Caches the assembly reference closure using the section key to prevent redundant disk metadata reads on repeated opens.
    /// </para>
    /// </summary>
    [Test]
    public void ARebuiltSectionDescribingTheSameTree_ReusesTheCollectedAssemblySet()
    {
        var factory = _workspace!.WidgetFactory;
        var first = PrefabFingerprint.GetPrefabSection(factory, Movie)!;
        var collected = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), first);

        var rebuilt = PrefabFingerprint.BuildPrefabSection(factory, Movie, out _)!;

        Assert.That(rebuilt, Is.Not.SameAs(first), "test premise: a rebuild is a new object");
        Assert.That(rebuilt.Key, Is.EqualTo(first.Key), "and it says the same thing");
        Assert.That(PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), rebuilt), Is.SameAs(collected));
    }

    /// <summary>
    /// Verifies that modifying the prefab tree generates a distinct section key and triggers assembly reference re-collection.
    /// </summary>
    [Test]
    public void ASectionDescribingAChangedTree_CollectsAgain()
    {
        var factory = _workspace!.WidgetFactory;
        var section = PrefabFingerprint.GetPrefabSection(factory, Movie)!;
        var collected = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), section);

        var changed = section with { Text = section.Text + "prefab:Extra:0000" };

        Assert.That(changed.Key, Is.Not.EqualTo(section.Key), "test premise: a different closure");
        Assert.That(PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), changed), Is.Not.SameAs(collected));
    }

    [Test]
    public void Inputs_ListEveryDependencyByName()
    {
        var fingerprint = PrefabFingerprint.Compute(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM), out var inputs);

        Assert.That(fingerprint, Is.EqualTo(PrefabXmlRegistry.Hash(inputs)));
        Assert.That(inputs, Does.StartWith("generation:" + PrefabFingerprint.ComputeGeneration()));
        Assert.That(inputs, Does.Contain("viewmodel:" + PrefabFingerprint.DescribeType(typeof(CodegenTestVM))));
        Assert.That(inputs, Does.Contain("prefab:" + Movie + ":"));
    }

    [Test]
    public void UnrelatedPrefabs_DoNotDisturbTheFingerprint_OrTheRegistrationVersion()
    {
        var first = Compute();
        var version = PrefabXmlRegistry.Version;

        // Confirms that recording XML changes for unrelated prefabs does not alter the target movie's fingerprint.
        var other = new XmlDocument();
        other.LoadXml("<Prefab><Window><Widget /></Window></Prefab>");
        PrefabXmlRegistry.Record("FingerprintUnrelatedToThisMovie", other);
        var changed = new XmlDocument();
        changed.LoadXml("<Prefab><Window><TextWidget Text=\"changed\" /></Window></Prefab>");
        PrefabXmlRegistry.Record("FingerprintUnrelatedToThisMovie", changed);

        Assert.That(PrefabXmlRegistry.Version, Is.EqualTo(version), "XML changes are tracked per prefab, not by a global version");
        Assert.That(Compute(), Is.EqualTo(first));
    }

    private sealed class FingerprintRegisteredWidget : Widget
    {
        public FingerprintRegisteredWidget(UIContext context) : base(context) { }
    }

    [Test]
    public void RegisteringAWidgetClass_DoesNotMoveTheVersion()
    {
        // Custom widget class registrations do not increment the XML registry version, preserving cached prefab sections.
        var version = PrefabXmlRegistry.Version;

        WidgetFactoryManager.Register(typeof(FingerprintRegisteredWidget));
        WidgetFactoryManager.Register(typeof(FingerprintRegisteredWidget));

        Assert.That(PrefabXmlRegistry.Version, Is.EqualTo(version));
    }

    /// <summary>
    /// Verifies that cached prefab sections remain valid across environment changes that do not modify prefab XML registrations.
    /// </summary>
    [Test]
    public void TheCachedSection_SurvivesChangesThatAreNotPrefabRegistrations()
    {
        var factory = _workspace!.WidgetFactory;
        var section = PrefabFingerprint.GetPrefabSection(factory, Movie)!;

        UIEnvironmentVersion.Touch();
        WidgetFactoryManager.Register(typeof(FingerprintRegisteredWidget));

        Assert.That(section.IsCurrent(factory), Is.True);
        Assert.That(PrefabFingerprint.GetPrefabSection(factory, Movie), Is.SameAs(section));
    }

    /// <summary>
    /// Verifies that the prefab closure records only the widget classes explicitly referenced within the movie hierarchy.
    /// </summary>
    [Test]
    public void Closure_ReportsOnlyTheWidgetClassesTheMovieNames()
    {
        var section = PrefabFingerprint.GetPrefabSection(_workspace!.WidgetFactory, Movie)!;

        Assert.That(section.WidgetTypes, Is.EquivalentTo(new[]
        {
            "Widget", "ListPanel",
            "FingerprintNested", "FingerprintInherited", "FingerprintDeep", "FingerprintItem",
        }));
        Assert.That(section.WidgetTypes, Has.None.EqualTo("FingerprintUnrelated"), "a prefab outside the tree is not one of its widget classes");
    }

    /// <summary>
    /// Verifies that collected reference sets are reused until the global environment version is invalidated.
    /// </summary>
    [Test]
    public void ReferenceSet_IsReusedUntilTheEnvironmentMoves()
    {
        var section = PrefabFingerprint.GetPrefabSection(_workspace!.WidgetFactory, Movie)!;
        var first = PrefabReferenceSet.Collect(_workspace.WidgetFactory, typeof(CodegenTestVM), section);

        Assert.That(PrefabReferenceSet.Collect(_workspace.WidgetFactory, typeof(CodegenTestVM), section), Is.SameAs(first));

        UIEnvironmentVersion.Touch();

        Assert.That(PrefabReferenceSet.Collect(_workspace.WidgetFactory, typeof(CodegenTestVM), section), Is.Not.SameAs(first));
    }

    [Test]
    public void RegisteringAPrefab_MovesTheVersion()
    {
        var version = PrefabXmlRegistry.Version;

        WidgetFactoryManager.Register("FingerprintRegisteredPrefab" + Guid.NewGuid().ToString("N"), () => null);

        Assert.That(PrefabXmlRegistry.Version, Is.GreaterThan(version), "a registered prefab can change what a name in a prefab tree resolves to");
    }
}
