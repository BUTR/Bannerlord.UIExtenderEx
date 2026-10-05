using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// Places where the game's own generator does not do what the XML loader does, and this fork follows the loader.
/// <para>
/// The loader is the behaviour that matters. It is what mods have been written against for the whole life of the game,
/// and it is what a prefab falls back to whenever generation or compilation fails, so a prefab that behaves one way
/// compiled and another way as XML is worse than one that is consistently wrong. Each test below names the loader code
/// it is holding the generator to. <c>GameGeneratorEquivalenceTests</c> lists the same deviations from the other side.
/// </para>
/// </summary>
public class XmlLoaderParityTests
{
    private static GeneratedCode Generate(PrefabWorkspace workspace, string movieName, Type viewModelType) => new(workspace
        .Generate(movieName, viewModelType)
        .Single(x => x.FileName == movieName + ".gen.cs").Content);

    // --- conversions on the way into a widget (WidgetExtensions.ConvertObject) --------------------------------------

    [Test]
    public void AStringBoundToASpriteProperty_ClearsItWhenTheStringIsNull()
    {
        // ConvertObject only converts a non-null string; a null goes straight to the setter and clears the property.
        // The game's generator wraps the whole assignment in a null check and leaves the previous sprite in place.
        using var workspace = new PrefabWorkspace(("SpriteMovie",
            @"<Prefab><Window><BrushWidget Sprite=""@SpriteName"" /></Window></Prefab>"));

        var code = Generate(workspace, "SpriteMovie", typeof(ConversionVM));

        Assert.That(code.HasStatement("_widget.Sprite = this.Context.SpriteData.GetSprite(_datasource_Root.SpriteName);"), Is.True);
        Assert.That(code.HasStatement("_widget.Sprite = null;"), Is.True, "a null string clears the sprite, as it does in the loader");
    }

    [Test]
    public void AnIntBoundToAFloatProperty_IsWidenedLikeReflectionWould()
    {
        // Nothing in ConvertObject touches this, but SetWidgetAttribute ends in GetSetMethod().Invoke, and the binder
        // performs the widening conversions. The game's generator emits nothing at all, so the widget keeps its default.
        using var workspace = new PrefabWorkspace(("WidenMovie",
            @"<Prefab><Window><Widget SuggestedWidth=""@Width"" /></Window></Prefab>"));

        var code = Generate(workspace, "WidenMovie", typeof(ConversionVM));

        Assert.That(code.HasStatement("_widget.SuggestedWidth = (global::System.Single)_datasource_Root.Width;"), Is.True);
    }

    [Test]
    public void APairNeitherConvertsNorWidens_IsNotAssignedAtAll()
    {
        // An int into a string property: ConvertObject leaves it alone and the binder cannot convert it, so the loader
        // throws on the way into the setter. Nothing to reproduce, so no typed read of the property is emitted. (A
        // notification whose payload happens to be a string is another matter: that the loader does assign, and so do
        // the payload handlers.)
        using var workspace = new PrefabWorkspace(("NoConversionMovie",
            @"<Prefab><Window><TextWidget Text=""@NotConvertible"" /></Window></Prefab>"));

        var code = Generate(workspace, "NoConversionMovie", typeof(ConversionVM));

        Assert.That(code.HasStatement("_widget.Text = _datasource_Root"), Is.False);
    }

    [TestCase("BrushWidget")]
    [TestCase("TextWidget")]
    public void ADottedBindingIntoTheBrush_IsAssignedWhenTheDataSourceIs(string widget)
    {
        // Compass binds Brush.TextColor on a TextWidget, and Brush has no TextColor. GauntletView.RefreshBinding hands the
        // binding to SetWidgetAttribute all the same when the data source arrives, and GetObjectAndProperty reads the
        // widget's Brush on the way - which clones it - before finding nothing to assign. The generated code makes that call
        using var workspace = new PrefabWorkspace(("DottedMovie",
            $@"<Prefab><Window><Widget><Children><{widget} Brush.TextColor=""@ColorValue"" /></Children></Widget></Window></Prefab>"));

        var code = Generate(workspace, "DottedMovie", typeof(FastPathSourceVM));

        Assert.That(code.CountStatements("Brush.TextColor"), Is.GreaterThanOrEqualTo(2), code.Text);
        Assert.That(code.Statements.Any(x => x.Contains("_widget_0.Brush.TextColor = _datasource_Root.ColorValue")
                                             || x.Contains("\"Brush.TextColor\", _datasource_Root.ColorValue")
                                             || (x.Contains("\"Brush.TextColor\"") && x.Contains("uiExtenderExValue_"))), Is.True,
            () => "read when the data source is assigned, not only when a notification arrives:" + Environment.NewLine +
                  string.Join(Environment.NewLine, code.Statements.Where(x => x.Contains("TextColor") || x.Contains("ColorValue"))));
    }

    // --- writing back to a ViewModel (ViewModel.SetPropertyValue) ---------------------------------------------------

    [Test]
    public void AWidgetValueGoingBackToAViewModel_IsNotConverted()
    {
        // SetPropertyValue invokes the setter with the value as it stands. The game's generator converts a Sprite to its
        // .Name on the way back, writing a value the loader never writes.
        using var workspace = new PrefabWorkspace(("WriteBackMovie",
            @"<Prefab><Window><BrushWidget Sprite=""@SpriteName"" /></Window></Prefab>"));

        var code = Generate(workspace, "WriteBackMovie", typeof(ConversionVM));

        Assert.That(code.HasStatement("_datasource_Root.SpriteName = _widget.Sprite.Name;"), Is.False);
        Assert.That(code.Text, Does.Not.Contain(".Sprite.Name"));
    }

    // --- command arguments (GauntletView.ConvertCommandParameter) ---------------------------------------------------

    private const string CommandPrefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <ButtonWidget Id=""ViewModelArg"" Command.Click=""ExecuteWithViewModel"" />
        <ButtonWidget Id=""ObjectArg"" Command.Click=""ExecuteWithObject"" />
        <ButtonWidget Id=""StringArg"" Command.Click=""ExecuteWithString"" />
      </Children>
    </Widget>
  </Window>
</Prefab>";

    [Test]
    public void EveryWidget_CarriesTheDataSourceInScopeForIt()
    {
        // ConvertCommandParameter asks GauntletMovie.FindViewOf(widget) for the ViewModel behind any widget at all, and
        // every view has one, inherited from its parent where the widget declares no DataSource. The component is how
        // generated code answers the same question, so every widget needs one; the game's generator attaches it only to
        // drag targets and list items and hands a command null for everything else.
        using var workspace = new PrefabWorkspace(("ComponentMovie", CommandPrefab));

        var code = Generate(workspace, "ComponentMovie", typeof(CommandArgumentVM));

        foreach (var widget in new[] { "_widget", "_widget_0", "_widget_1", "_widget_2" })
        {
            Assert.That(code.HasStatement($"var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData({widget});"), Is.True, widget);
            Assert.That(code.HasStatement($"var widgetComponent = {widget}.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();"), Is.True, widget);
        }
        Assert.That(code.HasStatement("widgetComponent.Data = _datasource_Root;"), Is.True);
        Assert.That(code.HasStatement("widgetComponent.Data = null;"), Is.True, "and is cleared with the data source");
    }

    [Test]
    public void ACommandArgument_IsSwappedForItsDataSourceByWhatItIsNotByWhatWasDeclared()
    {
        // ConvertCommandParameter tests the argument with `is Widget`. It does not look at the method signature, so every
        // command - one taking object, one taking a string - has its widget arguments swapped for their data sources,
        // where the game's generator only did it for a ViewModel parameter and handed the rest the raw widget.
        using var workspace = new PrefabWorkspace(("ArgumentMovie", CommandPrefab));

        var code = Generate(workspace, "ArgumentMovie", typeof(CommandArgumentVM));

        Assert.That(code.CountStatements("value = valueWidgetData == null ? null : valueWidgetData.Data;"), Is.EqualTo(3),
            "the ViewModel, object and string commands alike");
        Assert.That(code.HasStatement("if (arg0_value is"), Is.False, "nothing decides from the declared type any more");
    }

    [Test]
    public void ACommandArgument_ReachesTheMethodOnlyOnExecuteCommandsTerms()
    {
        // ViewModel.ExecuteCommand: as many arguments as parameters, a string converted for a non-string parameter, and
        // then each argument null or of a type the parameter takes (IsAssignableFrom) - or the method does not run. The
        // game's generator cast args[i] to the declared type, which threw where the loader does nothing.
        using var workspace = new PrefabWorkspace(("TermsMovie", CommandPrefab));

        var code = Generate(workspace, "TermsMovie", typeof(CommandArgumentVM));
        var viewModelArgument = "global::" + typeof(NonPublicChildVM).FullName;

        Assert.That(code.CountStatements("if (arguments.Length == 1)"), Is.EqualTo(3));
        Assert.That(code.HasStatement($"if ((commandArgument0 == null || commandArgument0 is {viewModelArgument}))"), Is.True);
        Assert.That(code.HasStatement($"_datasource_Root.ExecuteWithViewModel(({viewModelArgument})commandArgument0);"), Is.True);
        Assert.That(code.HasStatement("_datasource_Root.ExecuteWithObject((global::System.Object)commandArgument0);"), Is.True,
            "object takes anything, so there is nothing to test");
        Assert.That(code.HasStatement("if ((commandArgument0 == null || commandArgument0 is global::System.String))"), Is.True);
        Assert.That(code.HasStatement("args[0]"), Is.False, "the event's arguments are only read by the preparation loop");
    }

    // --- filling a bound list (GauntletView.AddItemToList) -----------------------------------------------------------

    [Test]
    public void AListOfOne_TakesTheDefaultTemplateRatherThanTheLastItemTemplate()
    {
        // AddItemToList needs _items.Count > 0 before it reaches for the last-item template, so the single item of a
        // one-item list is built from the default template. The game's generator only checks that the index is the final
        // one, which for a single item is index 0. The list is filled through AddItemToList's own steps, one item at a time,
        // with the items it already holds standing for _items.Count.
        using var workspace = new PrefabWorkspace(
            ("ListMovie", @"
<Prefab>
  <Window><Widget><Children>
    <ListPanel DataSource=""{Items}"">
      <ItemTemplate>
        <TextWidget Text=""@ChildText"" />
      </ItemTemplate>
      <ItemTemplate Type=""Last"">
        <TextWidget Text=""@ChildText"" />
      </ItemTemplate>
    </ListPanel>
  </Children></Widget></Window>
</Prefab>"));

        var code = Generate(workspace, "ListMovie", typeof(ListVM));

        Assert.That(code.HasStatement("if (itemsBefore == i && itemsBefore > 0)"), Is.True);
    }

    // --- parameters (WidgetTemplate.SetAttributes) -------------------------------------------------------------------

    [Test]
    public void AParameterPassedAsABinding_StillTakesItsDeclaredDefaultFirst()
    {
        // SetAttributes assigns the declared default whenever the enclosing prefab passed anything but a plain value, and
        // the binding replaces it once there is a data source. The game's generator assigned nothing, so the widget sat
        // on its own default until the ViewModel answered - or for good, where it never did.
        using var workspace = new PrefabWorkspace(
            ("PassedBindingMovie", @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <PassedBindingPart Parameter.Label=""@SpriteName"" />
      </Children>
    </Widget>
  </Window>
</Prefab>"),
            ("PassedBindingPart", @"
<Prefab>
  <Parameters><Parameter Name=""Label"" DefaultValue=""- default -"" /></Parameters>
  <Window><TextWidget Text=""*Label"" /></Window>
</Prefab>"));

        var code = Generate(workspace, "PassedBindingMovie", typeof(ConversionVM));

        Assert.That(code.HasStatement("this.Text = @\"- default -\";"), Is.True);
        Assert.That(code.HasStatement("_datasource_Root.SpriteName"), Is.True, "and the binding is still made");
    }
}
