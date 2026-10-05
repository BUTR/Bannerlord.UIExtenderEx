using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// A prefab whose root widget is another prefab rather than a widget class.
/// <para>
/// Nothing in the game ships one, but a <c>Replace</c> patch makes one out of a prefab that was fine before: Diplomacy
/// replaces the root of <c>DiplomacyPanel</c> with <c>&lt;DiplomacyPanelCustom /&gt;</c>, and <c>KingdomManagement</c>
/// writes <c>&lt;DiplomacyPanel MarginTop="188" ... /&gt;</c>. Asking what type <c>MarginTop</c> is meant one step down
/// into the prefab's root and then straight into the widget factory's dictionary, which threw a
/// <c>KeyNotFoundException</c> naming nothing and cost the whole movie its compiled prefab.
/// </para>
/// </summary>
public class PrefabRootChainTests
{
    private const string MovieName = "RootChainMovie";

    private const string Movie = @"
<Prefab>
  <Window>
    <Widget Id=""RootWidget"">
      <Children>
        <RootChainPanel Id=""Panel"" MarginTop=""188"" />
      </Children>
    </Widget>
  </Window>
</Prefab>";

    /// <summary>What a Replace patch on the root of a prefab leaves behind.</summary>
    private const string PanelPrefab = @"
<Prefab><Window><RootChainPanelCustom /></Window></Prefab>";

    private const string PanelCustomPrefab = @"
<Prefab><Window><Widget Id=""Custom""><Children><TextWidget Id=""Label"" /></Children></Widget></Window></Prefab>";

    /// <summary>A prefab rooted in itself, which only a patch can produce. Resolving it must end rather than recurse.</summary>
    private const string CyclicPrefab = @"
<Prefab><Window><RootChainCycle /></Window></Prefab>";

    private PrefabWorkspace _workspace = null!;

    [SetUp]
    public void SetUp() => _workspace = new PrefabWorkspace(
        (MovieName, Movie),
        ("RootChainPanel", PanelPrefab),
        ("RootChainPanelCustom", PanelCustomPrefab),
        ("RootChainCycle", CyclicPrefab));

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    private GeneratedCode Generate() => new(_workspace
        .Generate(MovieName, typeof(StructureVM))
        .Single(x => x.FileName == MovieName + ".gen.cs").Content);

    [Test]
    public void APrefabRootedInAnotherPrefab_IsFollowedToTheWidgetClass()
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory);

        Assert.That(_workspace.WidgetFactory.IsBuiltinTypeIncludingRegistered("RootChainPanelCustom"), Is.False,
            "test premise: the prefab's root is a prefab, not a widget class");

        Assert.That(_workspace.WidgetFactory.TryGetWidgetTypeWithinPrefabRoots("RootChainPanel", out var type), Is.True);
        Assert.That(type!.Name, Is.EqualTo("Widget"));
    }

    [Test]
    public void AnAttributeOnAPrefabRootedInAnotherPrefab_IsStillTyped()
    {
        // The whole point: generation runs to the end, and MarginTop is written as the float Widget declares
        var code = Generate();

        Assert.That(code.HasStatement("MarginTop = 188f;"), Is.True);
    }

    /// <summary>A name nothing defines is a plain Widget to the loader (<c>WidgetFactory.CreateBuiltinWidget</c>), and its attributes land on that.</summary>
    [Test]
    public void AnUnresolvableRoot_IsAPlainWidget()
    {
        using var workspace = new PrefabWorkspace(
            ("UnresolvableMovie", @"<Prefab><Window><Widget><Children><UnresolvableHost MarginTop=""12"" /></Children></Widget></Window></Prefab>"),
            ("UnresolvableHost", @"<Prefab><Window><NoSuchWidgetClass /></Window></Prefab>"));
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(workspace.WidgetFactory);

        Assert.That(workspace.WidgetFactory.TryGetWidgetTypeWithinPrefabRoots("UnresolvableHost", out var type), Is.True);
        Assert.That(type, Is.EqualTo(typeof(TaleWorlds.GauntletUI.BaseTypes.Widget)));
    }

    [Test]
    public void APrefabRootedInItself_EndsRatherThanRecursing()
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory);

        Assert.That(_workspace.WidgetFactory.TryGetWidgetTypeWithinPrefabRoots("RootChainCycle", out _), Is.False);
    }

    // --- the whole output ----------------------------------------------------------------------------------------

    /// <summary>Every file generated for the movie, line for line; see <see cref="GeneratedSnapshot"/>.</summary>
    [Test]
    public Task Snapshot() => GeneratedSnapshot.Verify(_workspace.Generate(MovieName, typeof(StructureVM)));
}
