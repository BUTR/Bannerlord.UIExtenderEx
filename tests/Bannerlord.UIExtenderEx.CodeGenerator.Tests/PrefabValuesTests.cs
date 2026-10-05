using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System.Collections.Generic;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// <see cref="WidgetPropertyPath"/> and <see cref="PrefabValues"/> resolve the two things an attribute needs before it
/// can be emitted: the property the attribute names, and the value a parameter stands for; and the names and literals
/// copied out of prefab XML into generated code.
/// </summary>
public class PrefabValuesTests
{
    private static PrefabValues Create() => new(null!, null!);

    /// <summary>
    /// How the generator walks a dotted path depends on whether the loader was patched to walk it the same way, and the
    /// patches go on from UIExtender's static constructor. Run it here rather than depend on another fixture having.
    /// </summary>
    [OneTimeSetUp]
    public void ApplyPatches() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(UIExtender).TypeHandle);

    // --- property paths -------------------------------------------------------------------------------------------

    [Test]
    public void PropertyPath_OfOneSegment_IsTheProperty()
    {
        var property = WidgetPropertyPath.Resolve(typeof(PropertyPathRoot), "Width");

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.Name, Is.EqualTo("Width"));
        Assert.That(property.DeclaringType, Is.EqualTo(typeof(PropertyPathRoot)));
    }

    [Test]
    public void PropertyPath_OfTwoSegments_WalksIntoTheNestedType()
    {
        // The shape shipped prefabs use: Brush.Color, Brush.FontSize
        var property = WidgetPropertyPath.Resolve(typeof(PropertyPathRoot), "Text.Left");

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.Name, Is.EqualTo("Left"));
        Assert.That(property.DeclaringType, Is.EqualTo(typeof(PropertyPathBranch)));
    }

    [Test]
    public void PropertyPath_OfThreeSegments_WalksTheWholePath()
    {
        // The game cuts the segment after the first with Substring(start, separatorIndex), passing an absolute index
        // where a length belongs, so "Text.Left.Margin" asks for Substring(5, 9) and gets "Left.Marg". That matches no
        // property and the attribute is dropped. WidgetExtensionsPatch fixes the loader's copy of the same walk, which is
        // what makes fixing it here safe; see PatchedXmlLoaderTests for the other half.
        var property = WidgetPropertyPath.Resolve(typeof(PropertyPathRoot), "Text.Left.Margin");

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.Name, Is.EqualTo("Margin"));
        Assert.That(property.DeclaringType, Is.EqualTo(typeof(PropertyPathLeaf)));
    }

    [Test]
    public void PropertyPath_WithALongFirstSegment_DoesNotRunOffTheEnd()
    {
        // The same arithmetic, other failure mode: once start + separatorIndex passes the end of the string the game's
        // version throws rather than quietly returning the wrong segment. "Container.Left.Margin" asks for
        // Substring(10, 14) of 21 characters.
        var property = WidgetPropertyPath.Resolve(typeof(PropertyPathRoot), "Container.Left.Margin");

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.Name, Is.EqualTo("Margin"));
    }

    [Test]
    public void PropertyPath_FollowsTheLoaderWhenThePatchIsNotInPlace()
    {
        // The generator never resolves a path the loader would not. If the patch ever fails to apply - after a game
        // update that changes that method, say - this drops back to the game's arithmetic rather than leaving compiled
        // prefabs resolving attributes that quietly do nothing on the XML the movie falls back to.
        Assert.That(WidgetExtensionsPatch.DottedPathsResolveCorrectly, Is.True,
            "the rest of these expectations only hold while the loader is patched");
    }

    [Test]
    public void PropertyPath_ThatDoesNotExist_IsNull()
    {
        Assert.That(WidgetPropertyPath.Resolve(typeof(PropertyPathRoot), "NoSuchProperty"), Is.Null);
        Assert.That(WidgetPropertyPath.Resolve(typeof(PropertyPathRoot), "Text.NoSuchProperty"), Is.Null);
    }

    [Test]
    public void PropertyPath_OfANonPublicProperty_IsNotFound()
    {
        // Widget properties are public by definition, so the public-only lookup is right here. It is the wrong lookup
        // when the same helper is pointed at a ViewModel, which is what GetViewModelPropertyForWriteback does.
        Assert.That(WidgetPropertyPath.Resolve(typeof(PropertyPathRoot), "Hidden"), Is.Null);
    }

    // --- parameters -----------------------------------------------------------------------------------------------

    [Test]
    public void ParameterDefaultValue_IgnoresWhatWasPassedIn()
    {
        var collection = Create();
        collection.FillFromPrefab(Prefab("<Prefab><Parameters><Parameter Name=\"Size\" DefaultValue=\"10\" /></Parameters><Window><Widget /></Window></Prefab>"));
        var given = GivenParameters("<Widget Size=\"25\" />");
        collection.SetGivenParameters(given, given);

        Assert.That(collection.GetParameterDefaultValue("Size"), Is.EqualTo("10"));
        Assert.That(collection.GetParameterDefaultValue("NoSuchParameter"), Is.Empty);
    }

    [Test]
    public void FillFromPrefab_CollectsConstantsAndParameters()
    {
        var collection = Create();

        collection.FillFromPrefab(Prefab(@"
<Prefab>
  <Constants>
    <Constant Name=""Pad"" Value=""5"" />
  </Constants>
  <Parameters>
    <Parameter Name=""Size"" DefaultValue=""10"" />
  </Parameters>
  <Window><Widget /></Window>
</Prefab>"));

        Assert.That(collection.Constants.Keys, Is.EquivalentTo(new[] { "Pad" }));
        Assert.That(collection.ParameterDefaults.Keys, Is.EquivalentTo(new[] { "Size" }));
        Assert.That(collection.GetConstantValue("Pad"), Is.EqualTo("5"));
    }

    [Test]
    public void usableName_ReplacesTheDotsThatCSharpWouldReject()
    {
        Assert.That(GeneratedNaming.GetUsableName("Standard.TopPanel"), Is.EqualTo("Standard_TopPanel"));
        Assert.That(GeneratedNaming.GetUsableName("Plain"), Is.EqualTo("Plain"));
    }

    [Test]
    public void usableName_WritesEveryOtherCharacterCSharpWouldRejectAsItsCodePoint()
    {
        // A nested ViewModel's full name is the variant, and a prefab's file name is the class
        Assert.That(GeneratedNaming.GetUsableName("Mod.Outer+InnerVM"), Is.EqualTo("Mod_Outer_2B_InnerVM"));
        Assert.That(GeneratedNaming.GetUsableName("My-Panel Wide"), Is.EqualTo("My_2D_Panel_20_Wide"));
        Assert.That(GeneratedNaming.GetUsableName("2DMap"), Is.EqualTo("_2DMap"), "an identifier cannot start with a digit");
    }

    [Test]
    public void uniqueName_NumbersOnlyTheNamesThatClash()
    {
        var taken = new System.Collections.Generic.HashSet<string>();

        Assert.That(GeneratedNaming.GetUniqueName("Tab_Left", taken), Is.EqualTo("Tab_Left"));
        Assert.That(GeneratedNaming.GetUniqueName("Tab_Left", taken), Is.EqualTo("Tab_Left_2"));
        Assert.That(GeneratedNaming.GetUniqueName("Tab_Left", taken), Is.EqualTo("Tab_Left_3"));
        Assert.That(GeneratedNaming.GetUniqueName("Other", taken), Is.EqualTo("Other"));
    }

    // --- literals ---------------------------------------------------------------------------------------------------

    [Test]
    public void aRegularString_EscapesEverythingARegularLiteralCannotHold()
    {
        var separators = new string([(char) 0x0085, (char) 0x2028, (char) 0x2029]);

        Assert.That(GeneratedLiteral.Regular("plain"), Is.EqualTo("\"plain\""), "the game's own text is unchanged");
        Assert.That(GeneratedLiteral.Regular("a\"b\\c"), Is.EqualTo(@"""a\""b\\c"""));
        Assert.That(GeneratedLiteral.Regular("line\r\n\t\0" + (char) 1), Is.EqualTo(@"""line\r\n\t\0\u0001"""));
        Assert.That(GeneratedLiteral.Regular(separators), Is.EqualTo(@"""\u0085\u2028\u2029"""));
    }

    [Test]
    public void aVerbatimString_OnlyDoublesItsQuotes()
    {
        Assert.That(GeneratedLiteral.String("say \"hi\"\nto C:\\temp"), Is.EqualTo("@\"say \"\"hi\"\"\nto C:\\temp\""));
    }

    [Test]
    public void aComment_CannotBeEndedByTheTextItQuotes()
    {
        var comment = GeneratedLiteral.Comment("one\r\ntwo" + (char) 0x2028 + "three");

        Assert.That(comment, Is.EqualTo(@"one\r\ntwo\u2028three"));
        Assert.That(comment.IndexOfAny(['\r', '\n', (char) 0x2028]), Is.EqualTo(-1));
    }

    [TestCase(float.NaN, "float.NaN")]
    [TestCase(float.PositiveInfinity, "float.PositiveInfinity")]
    [TestCase(0.25f, "0.25f")]
    public void aFloat_IsAlwaysSomethingCSharpTakes(float value, string expected)
    {
        Assert.That(GeneratedLiteral.Float(value), Is.EqualTo(expected));
    }

    // --- helpers --------------------------------------------------------------------------------------------------

    private static WidgetPrefab Prefab(string xml)
    {
        using var workspace = new PrefabWorkspace(("Fixture", xml));
        return workspace.Load("Fixture");
    }

    /// <summary>The attributes of a widget element, in the shape a prefab hands to the prefab it embeds.</summary>
    private static Dictionary<string, WidgetAttributeTemplate> GivenParameters(string widgetXml)
    {
        using var workspace = new PrefabWorkspace(("Given", $"<Prefab><Window>{widgetXml}</Window></Prefab>"));
        var given = new Dictionary<string, WidgetAttributeTemplate>();
        foreach (var attribute in workspace.Load("Given").RootTemplate.AllAttributes)
            given[attribute.Key] = attribute;
        return given;
    }
}
