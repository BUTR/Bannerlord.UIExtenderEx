using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System.Linq;
using System.Threading.Tasks;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

public class DerivedItemVM : DeclaredItemVM
{
    [DataSourceProperty]
    public bool HasIndicator => true;

    [DataSourceProperty]
    public int IndicatorCount => 3;
}

public class DeclaredItemVM : ViewModel
{
    [DataSourceProperty]
    public string BaseText => "base";
}

public class DerivedItemListVM : ViewModel
{
    [DataSourceProperty]
    public MBBindingList<DeclaredItemVM> ItemList { get; } = [];
}

/// <summary>
/// A list declared as <c>MBBindingList&lt;Base&gt;</c> that a mod fills with a subclass, then patches the item template
/// to bind to the subclass' own properties.
/// <para>
/// This is how the game's own selector dropdowns get extended, and it is the case the game's generator gets wrong: it
/// resolves a binding against the element type the list declares and drops whatever that type does not have, so the
/// widgets are built but never bound and keep their defaults - every indicator visible, every count zero. The XML
/// loader binds against the instance it actually holds, so the same prefab works when it is loaded from XML.
/// </para>
/// </summary>
public class DerivedViewModelBindingTests
{
    private const string MovieName = "DerivedItemMovie";

    private const string Prefab = @"
<Prefab>
  <Window><Widget><Children>
    <ListPanel Id=""Items"" DataSource=""{ItemList}"">
      <ItemTemplate>
        <Widget>
          <Children>
            <TextWidget Id=""BaseText"" Text=""@BaseText"" />
            <Widget Id=""Indicator"" IsVisible=""@HasIndicator"" />
            <TextWidget Id=""IndicatorText"" IntText=""@IndicatorCount"" />
          </Children>
        </Widget>
      </ItemTemplate>
    </ListPanel>
  </Children></Widget></Window>
</Prefab>";

    private PrefabWorkspace _workspace = null!;


    [SetUp]
    public void SetUp() => _workspace = new PrefabWorkspace((MovieName, Prefab));

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    private GeneratedCode Generate() => new(string.Join("\n", _workspace.Generate(MovieName, typeof(DerivedItemListVM)).Select(x => x.Content)));

    [Test]
    public void APropertyOfTheDeclaredElementType_StaysTyped()
    {
        var code = Generate();

        Assert.That(code.Statements.Any(x => x.Contains(".Text = ") && x.Contains(".BaseText")), Is.True);
        Assert.That(code.HasStatement("DynamicMember.Get(_datasource_Root, \"BaseText\")"), Is.False, "nothing the generator can resolve pays for a lookup");
    }

    [Test]
    public void APropertyOnlyTheSubclassHas_IsBoundByName()
    {
        var code = Generate();

        Assert.That(code.HasStatement("\"IsVisible\""), Is.True);
        Assert.That(code.HasStatement("DynamicMember.Get(_datasource_Root, \"HasIndicator\")"), Is.True);
        Assert.That(code.HasStatement("DynamicMember.Get(_datasource_Root, \"IndicatorCount\")"), Is.True);
        Assert.That(code.HasComment("Couldn't find property in ViewModel"), Is.False);
    }

    [Test]
    public void TheByNameBinding_RunsOnEveryPropertyChange_NotOnlyOnTheFirstRead()
    {
        // A binding that is only read when the data source is assigned looks right on the screen the first time and
        // never moves again. Both directions of the dispatch have to carry it.
        var code = Generate();

        Assert.That(code.Statements.Count(x => x.Contains("object uiExtenderExValue_") && x.Contains("_IsVisible = ")),
            Is.GreaterThanOrEqualTo(2), "once when the data source is assigned and once from the property-changed handler");
        Assert.That(code.Statements.Any(x => x.Contains("DynamicMember.Set(_datasource_Root, \"HasIndicator\", value);")), Is.True,
            "the widget writes back the way the loader does");
    }

    /// <summary>
    /// The notification that carries its own bool goes straight into the widget's own bool property. This is the whole
    /// point of the typed overloads: a value that arrives as a bool needs neither a box on the way in nor reflection on
    /// the way out.
    /// </summary>
    [Test]
    public void AnEligibleBoolBinding_IsAssignedDirectlyInItsOwnOverload()
    {
        var code = Generate();

        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithBoolValueOf_datasource_Root(global::System.String propertyName, global::System.Boolean value)"), Is.True);
        Assert.That(code.HasStatement("_widget_1.IsVisible = uiExtenderExAssigned__widget_1_IsVisible;"), Is.True);
    }

    /// <summary>
    /// And the object notification, which carries anything at all, tests for that one type and keeps the loader's call
    /// for everything else - null, a string, a boxed something that is not a bool.
    /// </summary>
    [Test]
    public void TheObjectOverload_GuardsAndFallsBackToReflection()
    {
        var code = Generate();

        Assert.That(code.HasStatement("if (value is global::System.Boolean uiExtenderExAssigned__widget_1_IsVisible)"), Is.True);
        Assert.That(code.HasStatement("SetWidgetAttribute(this.Context, _widget_1, \"IsVisible\", value)"), Is.True);
    }

    [Test]
    public void TheGeneratedCode_Compiles()
    {
        var compiler = new RoslynCompiler();

        var references = PrefabReferenceSet.CollectPaths(_workspace.WidgetFactory, typeof(DerivedItemListVM));
        var sources = _workspace.Generate(MovieName, typeof(DerivedItemListVM))
            .Concat([IgnoresAccessChecksSource.Create(references)])
            .ToList();

        var result = compiler.Compile(
            CompiledPrefabManager.GetAssemblyName(MovieName, typeof(DerivedItemListVM), "testbuild0000000"), sources, references);

        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
    }

    // --- the whole output ----------------------------------------------------------------------------------------

    /// <summary>Every file generated for the movie, line for line; see <see cref="GeneratedSnapshot"/>.</summary>
    [Test]
    public Task Snapshot() => GeneratedSnapshot.Verify(_workspace.Generate(MovieName, typeof(DerivedItemListVM)));
}
