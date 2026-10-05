using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GamePrefabs;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ResourceManager;
using Bannerlord.UIExtenderEx.Runtimes;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies the runtime selection logic in <see cref="GauntletMoviePatch.LoadPrefix"/> and <see cref="GamePrefabRuntime"/>.
/// <para>
/// <see cref="GamePrefabRuntime"/> detects prefabs inlined into pre-compiled widget classes by traversing reachable widget fields.
/// Tests confirm that cyclic widget class references (e.g., <c>InventoryScreenWidget</c> and <c>InventoryItemButtonWidget</c>)
/// terminate safely without overflowing the call stack.
/// </para>
/// </summary>
public class GauntletMoviePatchTests
{
    // Simulates engine structure: a generated root (Prefab__Variant) whose fields lead into mutually referencing custom widgets.
    private sealed class CycleMovie__SomeVM : Widget
    {
        public CycleScreenWidget? Screen;
        public CycleMovie__SomeVM_Dependency_1_Nested__DependendPrefab? Nested;
        public CycleMovie__SomeVM(UIContext context) : base(context) { }
    }

    private sealed class CycleMovie__SomeVM_Dependency_1_Nested__DependendPrefab : Widget
    {
        public CycleItemButtonWidget? Button;
        public CycleMovie__SomeVM_Dependency_1_Nested__DependendPrefab(UIContext context) : base(context) { }
    }

    private sealed class CycleScreenWidget : Widget
    {
        public CycleItemButtonWidget? Button;
        public CycleScreenWidget? Self;
        public CycleScreenWidget(UIContext context) : base(context) { }
    }

    private sealed class CycleItemButtonWidget : Widget
    {
        public CycleScreenWidget? Screen;
        public Widget? Plain;
        public CycleItemButtonWidget(UIContext context) : base(context) { }
    }

    [Test]
    public void GetAutoGenNames_TerminatesOnCyclicWidgetClasses_AndNamesEveryInlinedPrefab()
    {
        var names = GamePrefabRuntime.GetAutoGenNames(typeof(CycleMovie__SomeVM));

        Assert.That(names, Is.EquivalentTo(new[] { "CycleMovie", "Nested" }), "the root and the prefab embedded in it; custom widgets carry no prefab name");
    }

    [TestCase("Inventory__TaleWorlds_CampaignSystem_ViewModelCollection_Inventory_SPInventoryVM", "Inventory")]
    [TestCase("Inventory__TaleWorlds_CampaignSystem_ViewModelCollection_Inventory_SPInventoryVM_Dependency_3_InventoryEquippedItemSlot__DependendPrefab", "InventoryEquippedItemSlot")]
    [TestCase("Inventory__TaleWorlds_CampaignSystem_ViewModelCollection_Inventory_SPInventoryVM_Dependency_12_Base__InheritedPrefab", "Base")]
    [TestCase("Inventory__TaleWorlds_CampaignSystem_ViewModelCollection_Inventory_SPInventoryVM_Dependency_1_ItemTemplate", "Inventory")]
    [TestCase("ClanScreen__TaleWorlds_CampaignSystem_ViewModelCollection_ClanManagement_ClanManagementVM_Dependency_7_ClanControl_Dropdown__DependendPrefab", "ClanControl_Dropdown")]
    [TestCase("EscapeMenu__Default", "EscapeMenu")]
    [TestCase("InventoryScreenWidget", null)]
    [TestCase("__Odd", null)]
    public void GetPrefabName_FollowsTheGeneratorsNaming(string className, string? expected)
    {
        Assert.That(GamePrefabRuntime.GetPrefabName(className), Is.EqualTo(expected));
    }

    [Test]
    public void PatchedPrefabNames_AreComparedTheWayTheGeneratorSpellsThem()
    {
        Assert.That(PrefabNames.Normalize("ClanControl.Dropdown"), Is.EqualTo("ClanControl_Dropdown"));
        Assert.That(PrefabNames.Normalize("Inventory"), Is.EqualTo("Inventory"));
    }

    [Test]
    public void GetAutoGenNames_StartingInsideTheCycle_Terminates()
    {
        Assert.That(GamePrefabRuntime.GetAutoGenNames(typeof(CycleScreenWidget)), Is.Empty);
    }

    [Test]
    public void GetAutoGenNames_IsCachedPerRoot()
    {
        Assert.That(GamePrefabRuntime.GetAutoGenNames(typeof(CycleMovie__SomeVM)), Is.SameAs(GamePrefabRuntime.GetAutoGenNames(typeof(CycleMovie__SomeVM))));
    }

    /// <summary>
    /// Represents a generated variant registered for a movie whose factory creator method matches the root widget class name.
    /// </summary>
    private sealed class KeepMovie__SwitchVM : Widget
    {
        public KeepMovie__SwitchVM(UIContext context) : base(context) { }
    }

    /// <summary>Represents a variant whose naming scheme does not match any recognized prefab naming pattern.</summary>
    private sealed class UnreadableRoot : Widget
    {
        public UnreadableRoot(UIContext context) : base(context) { }
    }

    private static GeneratedPrefabInstantiationResult CreateKeepMovie__SwitchVM(UIContext context, Dictionary<string, object> data) => null!;

    private static GeneratedPrefabInstantiationResult CreateUnreadableRoot(UIContext context, Dictionary<string, object> data) => null!;

    private sealed class SwitchVM : ViewModel;

    private static readonly AccessTools.FieldRef<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>? GeneratedPrefabs =
        AccessTools2.FieldRefAccess<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>("_generatedPrefabs");

    private static void RegisterVariant(WidgetFactory widgetFactory, string movieName, string creatorName)
    {
        var method = AccessTools2.DeclaredMethod(typeof(GauntletMoviePatchTests), creatorName)!;
        widgetFactory.GeneratedPrefabContext.AddGeneratedPrefab(movieName, typeof(SwitchVM).FullName!, (CreateGeneratedWidget) Delegate.CreateDelegate(typeof(CreateGeneratedWidget), method));
    }

    /// <summary>Represents a mock prefab runtime owning variants registered within the test assembly.</summary>
    private sealed class OwningRuntime : IPrefabRuntime
    {
        public bool TryServe(WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) => false;
        public bool IsOwnVariant(Assembly variantAssembly) => variantAssembly == typeof(GauntletMoviePatchTests).Assembly;
    }

    private sealed class FixedRuntime(bool serves) : IPrefabRuntime
    {
        public int Asked { get; private set; }
        public bool TryServe(WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) { Asked++; return serves; }
        public bool IsOwnVariant(Assembly variantAssembly) => false;
    }

    private sealed class ThrowingRuntime : IPrefabRuntime
    {
        public bool TryServe(WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) => throw new InvalidOperationException("runtime failure");
        public bool IsOwnVariant(Assembly variantAssembly) => false;
    }

    private static bool LoadsFromXml(WidgetFactory widgetFactory, string movieName, IViewModel? dataSource)
    {
        var doNotUseGeneratedPrefabs = false;
        GauntletMoviePatch.LoadPrefix(widgetFactory, movieName, dataSource, ref doNotUseGeneratedPrefabs);
        return doNotUseGeneratedPrefabs;
    }

    /// <summary>
    /// Verifies that movies fall back to XML loading when no runtime is registered, bypassing pre-compiled variants.
    /// </summary>
    [Test]
    public void WithNoRuntime_EveryMovieLoadsFromXml()
    {
        using var workspace = new PrefabWorkspace(("KeepMovie", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "KeepMovie", nameof(CreateKeepMovie__SwitchVM));

        using (PrefabRuntimes.ResetForTests())
            Assert.That(LoadsFromXml(workspace.WidgetFactory, "KeepMovie", new SwitchVM()), Is.True);

        using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance))
            Assert.That(LoadsFromXml(workspace.WidgetFactory, "KeepMovie", new SwitchVM()), Is.False, "the game runtime keeps an untouched variant");
    }

    private sealed class PatchedMovie__SwitchVM : Widget
    {
        public PatchedMovie__SwitchVM(UIContext context) : base(context) { }
    }

    private static GeneratedPrefabInstantiationResult CreatePatchedMovie__SwitchVM(UIContext context, Dictionary<string, object> data) => null!;

    [PrefabExtension("PatchedMovie", "descendant::Widget")]
    private sealed class PatchedMoviePatch : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => [new("Patched", "true")];
    }

    /// <summary>
    /// Verifies that patched movies fall back to XML loading until extensions are disabled, at which point the unpatched pre-compiled variant is served.
    /// </summary>
    [Test]
    public void APatchedMovie_LoadsFromXml_WithNoRuntime_AndWithTheGameRuntime()
    {
        using var workspace = new PrefabWorkspace(("PatchedMovie", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "PatchedMovie", nameof(CreatePatchedMovie__SwitchVM));

        var extender = UIExtender.Create(nameof(GauntletMoviePatchTests) + Guid.NewGuid().ToString("N"));
        extender.Register([typeof(PatchedMoviePatch)]);
        extender.Enable();
        try
        {
            using (PrefabRuntimes.ResetForTests())
                Assert.That(LoadsFromXml(workspace.WidgetFactory, "PatchedMovie", new SwitchVM()), Is.True);
            using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance))
                Assert.That(LoadsFromXml(workspace.WidgetFactory, "PatchedMovie", new SwitchVM()), Is.True);

            extender.Disable();
            using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance))
                Assert.That(LoadsFromXml(workspace.WidgetFactory, "PatchedMovie", new SwitchVM()), Is.False);
        }
        finally
        {
            extender.Deregister();
        }
    }

    /// <summary>
    /// Verifies that unregistered or unvouched game variants fall back to subsequent registered runtimes or XML loading.
    /// </summary>
    [Test]
    public void WithoutTheGameRuntime_AMovieWithAGameVariant_IsLeftToTheOthers()
    {
        using var workspace = new PrefabWorkspace(("KeepMovie", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "KeepMovie", nameof(CreateKeepMovie__SwitchVM));
        var compiled = new FixedRuntime(false);

        using (PrefabRuntimes.ResetForTests(compiled))
            Assert.That(LoadsFromXml(workspace.WidgetFactory, "KeepMovie", new SwitchVM()), Is.True);

        Assert.That(compiled.Asked, Is.EqualTo(1));
    }

    /// <summary>Verifies that untouched movies serve game variants directly without invoking the compiled prefab runtime.</summary>
    [Test]
    public void WithTheGameRuntime_AnUnpatchedMovieKeepsTheGamesVariant_AndIsNeverCompiled()
    {
        using var workspace = new PrefabWorkspace(("KeepMovie", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "KeepMovie", nameof(CreateKeepMovie__SwitchVM));
        var compiled = new FixedRuntime(true);

        using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance, compiled))
            Assert.That(LoadsFromXml(workspace.WidgetFactory, "KeepMovie", new SwitchVM()), Is.False);

        Assert.That(compiled.Asked, Is.Zero);
    }

    private sealed class InlinerMovie__SwitchVM : Widget
    {
        public InlinerMovie__SwitchVM_Dependency_1_InlinedPart__DependendPrefab? Part;
        public InlinerMovie__SwitchVM(UIContext context) : base(context) { }
    }

    private sealed class InlinerMovie__SwitchVM_Dependency_1_InlinedPart__DependendPrefab : Widget
    {
        public InlinerMovie__SwitchVM_Dependency_1_InlinedPart__DependendPrefab(UIContext context) : base(context) { }
    }

    private static GeneratedPrefabInstantiationResult CreateInlinerMovie__SwitchVM(UIContext context, Dictionary<string, object> data) => null!;

    [PrefabExtension("InlinedPart", "descendant::Widget")]
    private sealed class InlinedPartPatch : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => [new("Patched", "true")];
    }

    /// <summary>Verifies that patching an inlined dependent prefab causes the parent movie to fall back from the pre-compiled variant to XML loading.</summary>
    [Test]
    public void APatchOnAnInlinedPrefab_TakesTheMovieOffTheGamesVariant()
    {
        using var workspace = new PrefabWorkspace(("InlinerMovie", PrefabWorkspace.PlainPrefab), ("InlinedPart", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "InlinerMovie", nameof(CreateInlinerMovie__SwitchVM));

        var extender = UIExtender.Create(nameof(GauntletMoviePatchTests) + Guid.NewGuid().ToString("N"));
        extender.Register([typeof(InlinedPartPatch)]);
        try
        {
            using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance))
            {
                Assert.That(GamePrefabRuntime.Instance.TryServe(workspace.WidgetFactory, "InlinerMovie", new SwitchVM()), Is.True, "the patch is not enabled yet");
                extender.Enable();
                Assert.That(GamePrefabRuntime.Instance.TryServe(workspace.WidgetFactory, "InlinerMovie", new SwitchVM()), Is.False);
            }
        }
        finally
        {
            extender.Deregister();
        }
    }

    private sealed class RegisteredOverMovie__SwitchVM : Widget
    {
        public RegisteredOverMovie__SwitchVM(UIContext context) : base(context) { }
    }

    private static GeneratedPrefabInstantiationResult CreateRegisteredOverMovie__SwitchVM(UIContext context, Dictionary<string, object> data) => null!;

    /// <summary>
    /// Verifies that registering an override prefab dynamically invalidates pre-compiled variants generated against original XML definitions.
    /// </summary>
    [Test]
    public void APrefabRegisteredOverTheMovie_TakesItOffTheGamesVariant()
    {
        using var workspace = new PrefabWorkspace(("RegisteredOverMovie", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "RegisteredOverMovie", nameof(CreateRegisteredOverMovie__SwitchVM));

        using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance))
        {
            Assert.That(GamePrefabRuntime.Instance.TryServe(workspace.WidgetFactory, "RegisteredOverMovie", new SwitchVM()), Is.True);
            WidgetFactoryManager.Register("RegisteredOverMovie", () => null);
            Assert.That(GamePrefabRuntime.Instance.TryServe(workspace.WidgetFactory, "RegisteredOverMovie", new SwitchVM()), Is.False);
        }
    }

    private sealed class CollectingListener : TraceListener
    {
        public List<string> Lines { get; } = [];
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { if (message is not null) Lines.Add(message); }
    }

    /// <summary>Verifies that convention check failures are logged only once to prevent log spam across collections.</summary>
    [Test]
    public void AFailedCheck_IsLoggedOnce()
    {
        var listener = new CollectingListener();
        Trace.Listeners.Add(listener);
        try
        {
            for (var i = 0; i < 2; i++)
            {
                using var workspace = new PrefabWorkspace(("OtherMovie", PrefabWorkspace.PlainPrefab));
                RegisterVariant(workspace.WidgetFactory, "OtherMovie", nameof(CreateUnreadableRoot));
                Assert.That(GamePrefabRuntime.Instance.ConventionsHold(workspace.WidgetFactory), Is.False);
            }
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.That(listener.Lines.Count(x => x.Contains("pre-compiled prefabs are not used")), Is.AtMost(1), "at most: an earlier test may have failed the check first");
    }

    [Test]
    public void RuntimesAreAskedInOrder_UntilOneServes()
    {
        using var workspace = new PrefabWorkspace(("OrderMovie", PrefabWorkspace.PlainPrefab));
        var declining = new FixedRuntime(false);
        var serving = new FixedRuntime(true);
        var after = new FixedRuntime(true);

        using (PrefabRuntimes.ResetForTests(declining, serving, after))
            Assert.That(LoadsFromXml(workspace.WidgetFactory, "OrderMovie", new SwitchVM()), Is.False);

        Assert.That((declining.Asked, serving.Asked, after.Asked), Is.EqualTo((1, 1, 0)));
    }

    /// <summary>Verifies that exceptions thrown by a prefab runtime cause it to decline gracefully, allowing subsequent runtimes to serve the request.</summary>
    [Test]
    public void ARuntimeThatThrows_Declines_AndTheNextIsAsked()
    {
        using var workspace = new PrefabWorkspace(("ThrowMovie", PrefabWorkspace.PlainPrefab));
        var next = new FixedRuntime(false);

        using (PrefabRuntimes.ResetForTests(new ThrowingRuntime(), next))
            Assert.That(LoadsFromXml(workspace.WidgetFactory, "ThrowMovie", new SwitchVM()), Is.True);

        Assert.That(next.Asked, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that variants owned by custom runtimes are never retained by <see cref="GamePrefabRuntime"/>, preventing stale XML or type resolution errors.
    /// </summary>
    [Test]
    public void AVariantARuntimeOwns_IsNeverKeptByTheGameRuntime()
    {
        using var workspace = new PrefabWorkspace(("KeepMovie", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "KeepMovie", nameof(CreateKeepMovie__SwitchVM));

        using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance, new OwningRuntime()))
            Assert.That(GamePrefabRuntime.Instance.TryServe(workspace.WidgetFactory, "KeepMovie", new SwitchVM()), Is.False);

        using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance))
            Assert.That(GamePrefabRuntime.Instance.TryServe(workspace.WidgetFactory, "KeepMovie", new SwitchVM()), Is.True,
                "one of TaleWorlds' own, with nothing patching it, still matches the XML it was generated from");
    }

    /// <summary>
    /// Verifies that unrecognized naming conventions disable <see cref="GamePrefabRuntime"/> entirely until the next prefab collection cycle.
    /// </summary>
    [Test]
    public void AVariantWhoseNamesAreNoPrefab_TurnsTheGameRuntimeOff_UntilTheNextCollection()
    {
        using var workspace = new PrefabWorkspace(("KeepMovie", PrefabWorkspace.PlainPrefab), ("OtherMovie", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "KeepMovie", nameof(CreateKeepMovie__SwitchVM));
        RegisterVariant(workspace.WidgetFactory, "OtherMovie", nameof(CreateUnreadableRoot));

        using (PrefabRuntimes.ResetForTests(GamePrefabRuntime.Instance))
        {
            Assert.That(GamePrefabRuntime.Instance.ConventionsHold(workspace.WidgetFactory), Is.False);
            Assert.That(LoadsFromXml(workspace.WidgetFactory, "KeepMovie", new SwitchVM()), Is.True, "an untouched movie whose own variant reads fine");

            // Clear test-injected variants and trigger collection to restore valid convention state.
            workspace.WidgetFactory.GeneratedPrefabContext.CollectPrefabs();
            GeneratedPrefabs!(workspace.WidgetFactory.GeneratedPrefabContext).Clear();
            RegisterVariant(workspace.WidgetFactory, "KeepMovie", nameof(CreateKeepMovie__SwitchVM));
            Assert.That(GamePrefabRuntime.Instance.ConventionsHold(workspace.WidgetFactory), Is.True);
            Assert.That(LoadsFromXml(workspace.WidgetFactory, "KeepMovie", new SwitchVM()), Is.False);
        }
    }

    /// <summary>Represents a variant referencing an inlined prefab that does not exist in the widget factory.</summary>
    private sealed class NamedMovie__SwitchVM : Widget
    {
        public NamedMovie__SwitchVM_Dependency_2_Missing__DependendPrefab? Missing;
        public NamedMovie__SwitchVM(UIContext context) : base(context) { }
    }

    private sealed class NamedMovie__SwitchVM_Dependency_2_Missing__DependendPrefab : Widget
    {
        public NamedMovie__SwitchVM_Dependency_2_Missing__DependendPrefab(UIContext context) : base(context) { }
    }

    private static GeneratedPrefabInstantiationResult CreateNamedMovie__SwitchVM(UIContext context, Dictionary<string, object> data) => null!;

    [Test]
    public void AnInlinedNameTheFactoryDoesNotHave_FailsTheCheck()
    {
        using var workspace = new PrefabWorkspace(("NamedMovie", PrefabWorkspace.PlainPrefab));
        RegisterVariant(workspace.WidgetFactory, "NamedMovie", nameof(CreateNamedMovie__SwitchVM));

        Assert.That(GamePrefabRuntime.Instance.ConventionsHold(workspace.WidgetFactory), Is.False);
    }

    /// <summary>
    /// Verifies that registering a runtime prefab override invalidates pre-compiled variants that inlined the original file.
    /// </summary>
    [Test]
    public void APrefabRegisteredOverAName_TakesTheMovieOffThePreCompiledVariant()
    {
        using var workspace = new PrefabWorkspace(("RegisterMovie", PrefabWorkspace.PlainPrefab));
        // Generate a unique dotted prefab identifier to simulate runtime registration overrides.
        var registered = "Registered.Panel" + Guid.NewGuid().ToString("N");
        WidgetFactoryManager.Register(registered, () => null);

        Assert.That(GamePrefabRuntime.CanKeepRegisteredVariant(workspace.WidgetFactory, [PrefabNames.Normalize(registered)]), Is.False);
        Assert.That(GamePrefabRuntime.CanKeepRegisteredVariant(workspace.WidgetFactory, ["SomethingElseEntirely"]), Is.True,
            "a registration under a new name is not a replacement of anything");
    }

    /// <summary>
    /// Verifies that <see cref="GamePrefabRuntime.GetAutoGenNames"/> identifies inlined sibling classes instantiated exclusively in method scopes without field backing.
    /// </summary>
    private sealed class SiblingMovie__SomeVM : Widget
    {
        public SiblingMovie__SomeVM(UIContext context) : base(context) { }
    }

    private sealed class SiblingMovie__SomeVM_Dependency_4_ListItem__DependendPrefab : Widget
    {
        public SiblingMovie__SomeVM_Dependency_4_ListItem__DependendPrefab(UIContext context) : base(context) { }
    }

    [Test]
    public void GetAutoGenNames_FindsAnInlinedPrefabNoFieldPointsAt()
    {
        var names = GamePrefabRuntime.GetAutoGenNames(typeof(SiblingMovie__SomeVM));

        Assert.That(names, Does.Contain("ListItem"), "the root has no field of that type; only its name ties the two together");
        Assert.That(names, Does.Contain("SiblingMovie"));
    }

    /// <summary>
    /// Verifies that <see cref="PrefabXmlRegistry"/> captures hashes for all parsed prefabs during initialization without missing parse events.
    /// </summary>
    [Test]
    public void NoPrefabIsParsedUnheard()
    {
        using var workspace = new PrefabWorkspace(("HeardMovie", PrefabWorkspace.PlainPrefab));
        var prefab = workspace.Load("HeardMovie");

        Assert.That(PrefabXmlRegistry.TryGetHash(prefab, out _), Is.True);
        Assert.That(PrefabSource.UnheardParses, Is.Zero);
    }
}