using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// How a prefab's shape becomes a class: the widgets it builds, the prefabs it embeds, and the widget-to-widget
/// references it resolves while generating rather than at runtime.
/// </summary>
public class PrefabStructureTests
{
    private const string MovieName = "StructureMovie";

    private const string Prefab = @"
<Prefab>
  <Window>
    <Widget Id=""RootWidget"">
      <Children>
        <Widget Id=""Sibling"" />
        <ScrollablePanel Id=""Panel"" InnerPanel=""Inner"" FixedHeader=""..\Sibling"" ClipRect=""NotThere"" ScrolledHeader=""Nowhere\Deeper"">
          <Children>
            <Widget Id=""Inner"" />
          </Children>
        </ScrollablePanel>
        <StructurePart Parameter.Label=""one"" />
        <StructurePart Parameter.Label=""one"" />
        <StructurePart Parameter.Label=""two"" />
      </Children>
    </Widget>
  </Window>
</Prefab>";

    private const string PartPrefab = @"
<Prefab>
  <Parameters><Parameter Name=""Label"" DefaultValue=""fallback"" /></Parameters>
  <Window><TextWidget Text=""*Label"" /></Window>
</Prefab>";

    private PrefabWorkspace _workspace = null!;

    [SetUp]
    public void SetUp() => _workspace = new PrefabWorkspace((MovieName, Prefab), ("StructurePart", PartPrefab));

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    private GeneratedCode Generate() => Generate(_workspace, MovieName);

    private static GeneratedCode Generate(PrefabWorkspace workspace, string movieName) => new(workspace
        .Generate(movieName, typeof(StructureVM))
        .Single(x => x.FileName == movieName + ".gen.cs").Content);

    // --- widget-to-widget references --------------------------------------------------------------------------------

    /// <summary>
    /// Every widget reference is the loader's own <c>SetWidgetAttributeFromString</c>: <c>FindChild</c> from the widget
    /// over the live tree when the attributes are set, the setter by reflection, and its try/catch. The game's generator
    /// resolved what it could against the template, which is not the tree the loader walks;
    /// <c>CompiledXmlTreeParityTests</c> has the case where the two answer differently.
    /// </summary>
    [TestCase("InnerPanel", "Inner")]
    [TestCase("FixedHeader", @"..\Sibling")]
    [TestCase("ClipRect", "NotThere")]
    [TestCase("ScrolledHeader", @"Nowhere\Deeper")]
    public void AWidgetReference_IsTheLoadersOwnCall(string property, string path)
    {
        var code = Generate();

        Assert.That(code.HasStatement(
            $"TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString(_widget_1, @\"{property}\", @\"{path}\", "), Is.True);
        Assert.That(code.HasStatement($"_widget_1.{property} = "), Is.False, "never resolved against the template");
    }

    // --- embedded prefabs -------------------------------------------------------------------------------------------

    [Test]
    public void AnEmbeddedPrefab_IsGeneratedOncePerDistinctSetOfParameters()
    {
        // Three usages, two of them identical: generating a class per usage would bloat every movie that repeats a part
        var code = Generate();
        var partClasses = code.Statements.Where(x => x.Contains("class ") && x.Contains("StructurePart")).ToList();

        Assert.That(partClasses, Has.Count.EqualTo(2), string.Join(" | ", partClasses));
    }

    [Test]
    public void AnEmbeddedPrefab_IsGivenTheParameterTheParentPassed()
    {
        var code = Generate();

        Assert.That(code.HasStatement("this.Text = @\"one\";"), Is.True);
        Assert.That(code.HasStatement("this.Text = @\"two\";"), Is.True);
        Assert.That(code.HasStatement("this.Text = @\"fallback\";"), Is.False, "the default only applies when nothing is passed");
    }

    [Test]
    public void AnEmbeddedPrefab_FallsBackToItsDeclaredDefault()
    {
        using var workspace = new PrefabWorkspace(
            ("DefaultMovie", @"<Prefab><Window><Widget><Children><StructurePart /></Children></Widget></Window></Prefab>"),
            ("StructurePart", PartPrefab));

        Assert.That(Generate(workspace, "DefaultMovie").HasStatement("this.Text = @\"fallback\";"), Is.True);
    }

    [Test]
    public void TheSameEmbeddedPrefab_IsGeneratedSeparatelyPerDataSourceType()
    {
        // The parameters match, so only the ViewModel type behind each usage tells the two apart. Sharing one class here
        // would bind one of them against the wrong ViewModel.
        using var workspace = new PrefabWorkspace(
            ("DataMovie", @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget DataSource=""{ChildA}""><Children><DataPart /></Children></Widget>
        <Widget DataSource=""{ChildB}""><Children><DataPart /></Children></Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>"),
            ("DataPart", @"<Prefab><Window><TextWidget Text=""@ChildText"" /></Window></Prefab>"));

        var code = Generate(workspace, "DataMovie");
        var partClasses = code.Statements.Where(x => x.Contains("class ") && x.Contains("DataPart")).ToList();

        Assert.That(partClasses, Has.Count.EqualTo(2), string.Join(" | ", partClasses));
        Assert.That(code.HasStatement($"SetDataSource(global::{typeof(NonPublicChildVM).FullName} dataSource)"), Is.True);
        Assert.That(code.HasStatement($"SetDataSource(global::{typeof(OtherChildVM).FullName} dataSource)"), Is.True);
    }

    [Test]
    public void EachWidget_IsBuiltAndAddedToItsParent()
    {
        var code = Generate();

        Assert.That(code.HasStatement("_widget = this;"), Is.True, "the root widget is the generated class itself");
        Assert.That(code.HasStatement("_widget_0 = new global::TaleWorlds.GauntletUI.BaseTypes.Widget(this.Context);"), Is.True);
        Assert.That(code.HasStatement("_widget.AddChild(_widget_0);"), Is.True);
        Assert.That(code.HasStatement("_widget_1_0 = new global::TaleWorlds.GauntletUI.BaseTypes.Widget(this.Context);"), Is.True);
        Assert.That(code.HasStatement("_widget_1.AddChild(_widget_1_0);"), Is.True);
    }

    [Test]
    public void EachIdInTheXml_ReachesTheWidget()
    {
        var code = Generate();

        foreach (var id in new[] { "RootWidget", "Sibling", "Panel", "Inner" })
            Assert.That(code.HasStatement($".Id = \"{id}\";"), Is.True, id);
    }

    // --- prefab inheritance ------------------------------------------------------------------------------------------

    [Test]
    public void APrefabWhoseRootIsAnotherPrefab_DerivesFromIt()
    {
        using var workspace = new PrefabWorkspace(
            ("InheritMovie", @"<Prefab><Window><InheritBase /></Window></Prefab>"),
            ("InheritBase", @"<Prefab><Window><Widget Id=""BaseWidget"" /></Window></Prefab>"));

        var code = Generate(workspace, "InheritMovie");

        Assert.That(code.HasStatement("InheritBase__InheritedPrefab"), Is.True, "the base prefab is generated as an inheritable class");
        Assert.That(code.HasStatement("public virtual void CreateWidgets()"), Is.True, "so the base class can be extended");
        Assert.That(code.HasStatement("public override void CreateWidgets()"), Is.True);
        Assert.That(code.HasStatement("base.CreateWidgets();"), Is.True);
        Assert.That(code.HasStatement("base.SetIds();"), Is.True);
        Assert.That(code.HasStatement("base.SetAttributes();"), Is.True);
    }

    // --- the entry point ---------------------------------------------------------------------------------------------

    [Test]
    public void TheCreator_RegistersTheMovieUnderItsViewModel()
    {
        var creator = new GeneratedCode(_workspace.Generate(MovieName, typeof(StructureVM))
            .Single(x => x.FileName == "PrefabCodes.gen.cs").Content);

        Assert.That(creator.HasStatement("class GeneratedUIPrefabCreator"), Is.True);
        Assert.That(creator.HasStatement($"generatedPrefabContext.AddGeneratedPrefab(\"{MovieName}\", \"{typeof(StructureVM).FullName}\", Create"), Is.True);
    }

    [Test]
    public void OneFileIsGeneratedPerPrefab_PlusTheCreator()
    {
        var files = _workspace.Generate(MovieName, typeof(StructureVM)).Select(x => x.FileName).ToList();

        Assert.That(files, Is.EquivalentTo(new[] { MovieName + ".gen.cs", "PrefabCodes.gen.cs" }),
            "embedded prefabs are generated into the file of the movie that uses them");
    }

    // --- the whole output ----------------------------------------------------------------------------------------

    /// <summary>Every file generated for the movie, line for line; see <see cref="GeneratedSnapshot"/>.</summary>
    [Test]
    public Task Snapshot() => GeneratedSnapshot.Verify(_workspace.Generate(MovieName, typeof(StructureVM)));
}
