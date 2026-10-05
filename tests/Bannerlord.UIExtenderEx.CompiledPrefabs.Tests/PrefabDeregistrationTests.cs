using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Prefabs;

using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System.Linq;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Defines a test prefab extension patch on a shared widget prefab to verify deregistration cleanup behavior.
/// </summary>
[PrefabExtension(PrefabDeregistrationTests.SharedPrefabName, "descendant::Widget[@Id='shared']")]
internal sealed class DeregistrationSetAttributePatch : PrefabExtensionSetAttributePatch
{
    public override string Id => "shared";
    public override string Attribute => "PatchedAttribute";
    public override string Value => "yes";
}

/// <summary>
/// Verifies that deregistering an extension module removes its prefab patches and purges cached prefabs from the widget factory.
/// <para>
/// Because widget factories retain parsed prefabs while referenced, module deregistration explicitly evicts patched
/// instances and invalidates fingerprints for any movies embedding the shared prefab.
/// </para>
/// </summary>
public class PrefabDeregistrationTests
{
    internal const string SharedPrefabName = "DeregistrationShared";
    private const string Movie = "DeregistrationMovie";
    private const string OtherMovie = "DeregistrationOtherMovie";

    private const string SharedPrefab = "<Prefab><Window><Widget Id=\"shared\" /></Window></Prefab>";
    private const string MoviePrefab = $"<Prefab><Window><Widget><Children><{SharedPrefabName} /></Children></Widget></Window></Prefab>";

    private PrefabWorkspace? _workspace;
    private UIExtender? _extender;
    private WidgetFactory? _previousFactory;

    [SetUp]
    public void SetUp()
    {
        _workspace = new PrefabWorkspace((Movie, MoviePrefab), (OtherMovie, MoviePrefab), (SharedPrefabName, SharedPrefab));

        // Replaces the global UIResourceManager factory with the test workspace instance for reloading verification.
        _previousFactory = UIResourceManager.WidgetFactory;
        SetWidgetFactory(_workspace.WidgetFactory);

        _extender = UIExtender.Create("TestModule.CompiledPrefabs.Deregistration");
        _extender.Register([typeof(DeregistrationSetAttributePatch)]);
        _extender.Enable();
        PrefabFingerprint.ClearCache();
    }

    [TearDown]
    public void TearDown()
    {
        _extender?.Deregister();
        SetWidgetFactory(_previousFactory!);
        _workspace?.Dispose();
        PrefabFingerprint.ClearCache();
    }

    private static void SetWidgetFactory(WidgetFactory factory) =>
        AccessTools2.DeclaredProperty("TaleWorlds.Engine.GauntletUI.UIResourceManager:WidgetFactory")!.SetValue(null, factory);

    private bool SharedPrefabIsPatched() =>
        _workspace!.WidgetFactory.GetCustomType(SharedPrefabName).RootTemplate.AllAttributes.Any(x => x.Key == "PatchedAttribute");

    /// <summary>
    /// Verifies that deregistering a module reverts patched prefab attributes on subsequent loads.
    /// </summary>
    [Test]
    public void DeregisteringAPrefabOnlyModule_RemovesThePatchedContentOnTheNextLoad()
    {
        Assert.That(SharedPrefabIsPatched(), Is.True, "test premise: the patch applies");

        _extender!.Deregister();
        _extender = null;

        Assert.That(SharedPrefabIsPatched(), Is.False, "the next load reads the prefab as it is on disk");
    }

    /// <summary>
    /// Verifies that deregistration evicts and reverts shared prefabs even while other active movies hold references to them.
    /// </summary>
    [Test]
    public void DeregisteringWhileAMovieHoldsTheSharedPrefab_StillRemovesThePatch()
    {
        var factory = _workspace!.WidgetFactory;
        factory.GetCustomType(SharedPrefabName);
        factory.GetCustomType(SharedPrefabName);
        Assert.That(_workspace.LivePrefabNames, Does.Contain(SharedPrefabName), "test premise: held parsed");
        Assert.That(SharedPrefabIsPatched(), Is.True);

        _extender!.Deregister();
        _extender = null;

        Assert.That(_workspace.LivePrefabNames, Does.Not.Contain(SharedPrefabName), "the patched copy is forgotten");
        Assert.That(SharedPrefabIsPatched(), Is.False);
    }

    /// <summary>
    /// Verifies that deregistration invalidates the compilation fingerprint for every movie embedding the shared prefab.
    /// </summary>
    [Test]
    public void DeregistrationInvalidatesTheFingerprintOfEveryMovieEmbeddingTheSharedPrefab()
    {
        var factory = _workspace!.WidgetFactory;
        var patched = TestFingerprint.Compute(factory, Movie, typeof(CodegenTestVM));
        var patchedOther = TestFingerprint.Compute(factory, OtherMovie, typeof(CodegenTestVM));
        Assert.That(patched, Is.Not.Null);
        Assert.That(patchedOther, Is.Not.Null);

        _extender!.Deregister();
        _extender = null;

        Assert.That(TestFingerprint.Compute(factory, Movie, typeof(CodegenTestVM)), Is.Not.EqualTo(patched));
        Assert.That(TestFingerprint.Compute(factory, OtherMovie, typeof(CodegenTestVM)), Is.Not.EqualTo(patchedOther),
            "a shared prefab is shared by more than one movie, and all of them were compiled from it");
    }
}
