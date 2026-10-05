using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.PrefabVerifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Tests prefab diagnostics UIX0011 through UIX0019, verifying prefab markup, parameter passing,
/// patch content members, and dataSource bindings.
/// </summary>
public class PrefabRuleTests
{
    /// <summary>
    /// Defines test ViewModels and mixins representing a host ViewModel extended with child ViewModels and custom properties.
    /// </summary>
    private const string Mod = """
        public class HostVM : ViewModel
        {
            public string Title { get; set; } = "";
            public void ExecuteClose() { }
        }

        public class ChildVM : ViewModel
        {
            public string Label { get; set; } = "";
            public MBBindingList<ItemVM> Items { get; } = new();
            public void ExecuteChild() { }
        }

        public class ItemVM : ViewModel
        {
            public string Name { get; set; } = "";
            public bool IsSelected { get; set; }
        }

        [ViewModelMixin]
        public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
        {
            public HostVMMixin(HostVM vm) : base(vm) { }
            [DataSourceProperty] public ChildVM Mod { get; } = new();
            [DataSourceProperty] public int ModWidth { get; set; }
        }

        """;

    private static string InsertPatch(string xml) => $$"""
        [PrefabExtension("HostMovie", "descendant::Widget[@Id='Panel']")]
        public sealed class InsertPage : PrefabExtensionInsertPatch
        {
            public override InsertType Type => InsertType.Child;
            private readonly XmlDocument _document = new();
            public InsertPage() { _document.LoadXml("{{xml}}"); }
            [PrefabExtensionXmlNode] public XmlNode GetPrefabExtension() => _document;
        }

        """;

    private const string ModPage = """
        <Prefab>
          <Window>
            <ListPanel>
              <Children>
                <TextWidget Text="@Label" />
                <ButtonWidget Command.Click="ExecuteChild" />
                <ListPanel DataSource="{Items}">
                  <ItemTemplate>
                    <ModItem />
                  </ItemTemplate>
                </ListPanel>
              </Children>
            </ListPanel>
          </Window>
        </Prefab>
        """;

    private const string ModItem = """
        <Prefab>
          <Window>
            <TextWidget Text="@Name" IsVisible="@IsSelected" />
          </Window>
        </Prefab>
        """;

    [Test]
    public async Task AMatchingModReportsNothing()
    {
        await VerifyAsync(Mod + InsertPatch("<ModPage DataSource=\\\"{Mod}\\\" />"),
            ("GUI/Prefabs/ModPage.xml", ModPage),
            ("GUI/Prefabs/ModItem.xml", ModItem));
    }

    public class ContentMember
    {
        private static string Patch(string name, string member) => $$"""
            [PrefabExtension("HostMovie", "descendant::Widget[@Id='Panel']")]
            public sealed class {{name}} : PrefabExtensionInsertPatch
            {
                public override InsertType Type => InsertType.Child;
                {{member}}
            }

            """;

        /// <summary>Verifies that supported return types (including covariant returns) satisfy runtime patch content bindings.</summary>
        [Test]
        public async Task EachTypeTheRuntimeBinds_ReportsNothing()
        {
            await VerifyAsync(
                Patch("Page1", "[PrefabExtensionFileName] public string File => \"ModPage\";") +
                Patch("Page2", "[PrefabExtensionText(true)] public string GetText() => \"<Root />\";") +
                Patch("Page3", "[PrefabExtensionXmlNode] public XmlDocument Document { get; } = new();") +
                Patch("Page4", "[PrefabExtensionXmlNode] public XmlElement GetElement() => null!;") +
                Patch("Page5", "[PrefabExtensionXmlNodes] public List<XmlNode> GetNodes() => new();") +
                Patch("Page6", "[PrefabExtensionXmlNodes] public IEnumerable<XmlElement> Elements => new XmlElement[0];") +
                Patch("Page7", "[PrefabExtensionXmlDocument] public XmlDocument GetDocument() => new();"));
        }

        [Test]
        public async Task AMemberOfAnotherType_IsAnError()
        {
            await VerifyAsync(
                Patch("Page8", "[PrefabExtensionXmlNode] public string {|UIX0017:GetXml|}() => \"<Root />\";") +
                Patch("Page9", "[PrefabExtensionText] public XmlNode {|UIX0017:Text|} => null!;") +
                Patch("Page10", "[PrefabExtensionXmlNodes] public XmlNode {|UIX0017:GetNodes|}() => null!;"));
        }

        /// <summary>
        /// Verifies legacy behavior where <c>[PrefabExtensionXmlDocument]</c> required an exact <see cref="System.Xml.XmlDocument"/> return type
        /// prior to UIExtenderEx 3.0, which unified XML node handling.
        /// </summary>
        [Test]
        public async Task AnXmlNodeUnderTheXmlDocumentAttribute_FitsFromVersion3()
        {
            var messages = await MessagesAsync(Patch("Page16", "[PrefabExtensionXmlDocument] public XmlNode GetNode() => null!;"));
            Assert.That(messages, Is.EqualTo(Verifier.UIExtenderExVersion.Major < 3
                ? new[] { "UIX0017: 'GetNode' cannot supply the patch's content: [PrefabExtensionXmlDocument] needs XmlDocument, and it is XmlNode" }
                : new string[0]));
        }

        [Test]
        public async Task EachWayAMemberCannotBeBound_IsReported()
        {
            var messages = await MessagesAsync(
                Patch("Page11", "[PrefabExtensionXmlNode] private XmlNode Hidden() => null!;") +
                Patch("Page12", "[PrefabExtensionXmlNode] public static XmlNode Shared() => null!;") +
                Patch("Page13", "[PrefabExtensionXmlNode] public XmlNode WithArgument(int index) => null!;") +
                Patch("Page14", "[PrefabExtensionXmlNode] public XmlNode Generic<T>() => null!;") +
                Patch("Page15", "[PrefabExtensionXmlNodes] public string Text => \"\";"));
            Assert.That(messages, Is.EquivalentTo(new[]
            {
                "UIX0017: 'Hidden' cannot supply the patch's content: it is not public, and the patch looks for its content among its public members",
                "UIX0017: 'Shared' cannot supply the patch's content: it is static",
                "UIX0017: 'WithArgument' cannot supply the patch's content: it takes parameters",
                "UIX0017: 'Generic' cannot supply the patch's content: it is generic",
                "UIX0017: 'Text' cannot supply the patch's content: [PrefabExtensionXmlNodes] needs IEnumerable<XmlNode>, and it is string",
            }));
        }
    }

    public class WellFormed
    {
        [Test]
        public async Task APrefabFileThatIsNotWellFormed_IsAnError()
        {
            await VerifyAsync(Mod, ("GUI/Prefabs/Broken.xml", """
                <Prefab>
                  <Window>
                    <Widget>
                  </{|UIX0011:Window|}>
                </Prefab>
                """));
        }
    }

    public class WidgetAttributes
    {
        [Test]
        public async Task AttributesTheWidgetDoesNotHave_AreReported()
        {
            await VerifyAsync(Mod, ("GUI/Prefabs/Page.xml", """
                <Prefab>
                  <Window>
                    <Widget {|UIX0012:HorizontalAlightment|}="Left" {|UIX0012:Brush.Color|}="#FFFFFFFF" AlphaFactor="0.2" HorizontalAlignment="Left">
                      <Children>
                        <ScrollablePanel {|UIX0012:MouseScrollAxis|}="Vertical" AutoHideScrollBars="true" />
                        <BrushWidget Brush.Color="#FFFFFFFF" />
                        <UnknownGamePrefab AnythingGoes="1" />
                      </Children>
                    </Widget>
                  </Window>
                </Prefab>
                """));
        }

        /// <summary>Verifies validation of prefabs rooted directly at <c>&lt;Window&gt;</c> without an enclosing <c>&lt;Prefab&gt;</c> element.</summary>
        [Test]
        public async Task APrefabRootedAtWindow_IsChecked()
        {
            await VerifyAsync(Mod, ("GUI/Prefabs/Page.xml", """
                <Window>
                  <Widget {|UIX0012:HorizontalAlightment|}="Left" />
                </Window>
                """));
        }

        [Test]
        public async Task LiteralValuesTheLoaderCannotConvert_AreReported()
        {
            await VerifyAsync(Mod, ("GUI/Prefabs/Page.xml", """
                <Prefab>
                  <Window>
                    <Widget {|UIX0013:HorizontalAlignment|}="Centre" {|UIX0013:IsVisible|}="True" {|UIX0013:SuggestedWidth|}="12px" VerticalAlignment="Center" IsEnabled="false" SuggestedHeight="40" />
                  </Window>
                </Prefab>
                """));
        }

        [Test]
        public async Task AnAttributeOnTheModsPrefabTag_IsCheckedAgainstItsRootWidget()
        {
            await VerifyAsync(Mod + InsertPatch("<ModLabel DataSource=\\\"{Mod}\\\" {|UIX0012:Txet|}=\\\"x\\\" IsEnabled=\\\"true\\\" />"),
                ("GUI/Prefabs/ModLabel.xml", """
                    <Prefab>
                      <Window>
                        <TextWidget Text="@Label" />
                      </Window>
                    </Prefab>
                    """));
        }

        /// <summary>Verifies diagnostic reporting when a mod prefab is inserted into a target scope that provides none of its required bindings.</summary>
        [Test]
        public async Task APrefabInsertedWhereItsNamesDoNotResolve_IsReported()
        {
            await VerifyAsync(Mod + InsertPatch("<ModItem />"),
                ("GUI/Prefabs/ModItem.xml", """
                    <Prefab>
                      <Window>
                        <TextWidget {|UIX0016:Text|}="@Name" {|UIX0016:IsVisible|}="@IsSelected" />
                      </Window>
                    </Prefab>
                    """));
        }
    }

    public class Parameters
    {
        private const string Selector = """
            <Prefab>
              <Parameters>
                <Parameter Name="SelectorDataSource" DefaultValue="" />
              </Parameters>
              <Window>
                <ListPanel DataSource="*SelectorDataSource">
                  <ItemTemplate>
                    <TextWidget {|UIX0015:Text|}="@Nme" />
                  </ItemTemplate>
                </ListPanel>
              </Window>
            </Prefab>
            """;

        /// <summary>Verifies that all child elements under <c>&lt;Parameters&gt;</c> are recognized as parameter definitions regardless of tag name.</summary>
        [Test]
        public async Task EveryChildOfParameters_IsAParameter()
        {
            await VerifyAsync(Mod, ("GUI/Prefabs/Page.xml", """
                <Prefab>
                  <Window>
                    <ModCounter Parameter.Step="2" />
                  </Window>
                </Prefab>
                """), ("GUI/Prefabs/ModCounter.xml", """
                <Prefab>
                  <Parameters>
                    <Paramter Name="Step" DefaultValue="1" />
                  </Parameters>
                  <Window>
                    <Widget />
                  </Window>
                </Prefab>
                """));
        }

        [Test]
        public async Task AParameterThePrefabDoesNotUse_IsReported()
        {
            await VerifyAsync(Mod, ("GUI/Prefabs/Page.xml", """
                <Prefab>
                  <Window>
                    <ModSelector {|UIX0014:Parameter.Nope|}="1" Parameter.SelectorDataSource="{Items}" />
                  </Window>
                </Prefab>
                """), ("GUI/Prefabs/ModSelector.xml", Selector.Replace("{|UIX0015:Text|}", "Text")));
        }

        /// <summary>Verifies scope propagation when passing a dataSource expression into a child prefab via parameter binding.</summary>
        [Test]
        public async Task ADataSourceHandedInAsAParameter_IsFollowed()
        {
            await VerifyAsync(Mod + InsertPatch("<ModSelector DataSource=\\\"{Mod}\\\" Parameter.SelectorDataSource=\\\"{Items}\\\" />"),
                ("GUI/Prefabs/ModSelector.xml", Selector));
        }
    }

    public class Bindings
    {
        [Test]
        public async Task MisspellingsBelowTheInferredScope_AreReported()
        {
            await VerifyAsync(Mod + InsertPatch("<ModPage DataSource=\\\"{Mod}\\\" />"),
                ("GUI/Prefabs/ModPage.xml", """
                    <Prefab>
                      <Window>
                        <ListPanel>
                          <Children>
                            <TextWidget {|UIX0015:Text|}="@Lable" />
                            <ButtonWidget {|UIX0015:Command.Click|}="ExecuteChld" />
                            <ListPanel {|UIX0015:DataSource|}="{Itmes}" />
                            <ListPanel DataSource="{Items}">
                              <ItemTemplate>
                                <ModItem />
                              </ItemTemplate>
                            </ListPanel>
                          </Children>
                        </ListPanel>
                      </Window>
                    </Prefab>
                    """),
                ("GUI/Prefabs/ModItem.xml", """
                    <Prefab>
                      <Window>
                        <TextWidget Text="@Name" {|UIX0015:IsVisible|}="@IsSelcted" />
                      </Window>
                    </Prefab>
                    """));
        }

        /// <summary>Verifies that UIX0016 is reported when a patch references dataSource members not defined on any candidate mixin ViewModel.</summary>
        [Test]
        public async Task APatchWhoseNamesNoMixinViewModelHas_IsReported()
        {
            await VerifyAsync(Mod + InsertPatch("<ModPage {|UIX0016:DataSource|}=\\\"{Mdo}\\\" />"),
                ("GUI/Prefabs/ModPage.xml", ModPage),
                ("GUI/Prefabs/ModItem.xml", ModItem));
        }

        [Test]
        public async Task ASetAttributePatch_IsCheckedTheSameWay()
        {
            await VerifyAsync(Mod + """
                [PrefabExtension("HostMovie", "descendant::Widget[@Id='Description']")]
                public sealed class Width : PrefabExtensionSetAttributePatch
                {
                    public override List<Attribute> Attributes => [new Attribute("SuggestedWidth", "@ModWidth")];
                }

                [PrefabExtension("HostMovie", "descendant::Widget[@Id='Description']")]
                public sealed class Misspelt : PrefabExtensionSetAttributePatch
                {
                    public override List<Attribute> Attributes => [new Attribute("SuggestedWidth", {|UIX0016:"@ModWdth"|})];
                }
                """);
        }

        /// <summary>Verifies that patches binding both base host members and mixin members validate successfully against the combined scope.</summary>
        [Test]
        public async Task TheHostsOwnMembersCount()
        {
            await VerifyAsync(Mod + InsertPatch("<Widget><Children><TextWidget Text=\\\"@Title\\\" /><Widget DataSource=\\\"{Mod}\\\" /><ButtonWidget Command.Click=\\\"ExecuteClose\\\" /></Children></Widget>"));
        }

        [Test]
        public async Task APrefabRegisteredUnderAnotherName_IsFound()
        {
            await VerifyAsync(Mod + InsertPatch("<ModPage_Mod DataSource=\\\"{Mod}\\\" />") + """
                public static class Resources
                {
                    private static XmlDocument Load(string name) => new();
                    public static void Register() => Bannerlord.UIExtenderEx.ResourceManager.WidgetFactoryManager.CreateAndRegister("ModPage_Mod", Load("Mod.GUI.Prefabs.ModPage.xml"));
                }
                """,
                ("GUI/Prefabs/ModPage.xml", ModPage.Replace("@Label", "@Lable").Replace("<TextWidget Text=", "<TextWidget {|UIX0015:Text|}=")),
                ("GUI/Prefabs/ModItem.xml", ModItem));
        }

        [Test]
        public async Task AMovieTheModLoadsItself_IsCheckedAgainstTheViewModelItPasses()
        {
            await VerifyAsync(Mod + """
                public static class Screen
                {
                    private static void LoadMovie(string movie, ViewModel dataSource) { }
                    public static void Open() => LoadMovie("ModItem", new ItemVM());
                }
                """,
                ("GUI/Prefabs/ModItem.xml", ModItem.Replace("@Name", "@Nam").Replace("<TextWidget Text=", "<TextWidget {|UIX0015:Text|}=")));
        }
    }

    public class Links
    {
        [Test]
        public async Task APatchLinkedWithItsMixin_ReportsNothing()
        {
            await VerifyAsync("[assembly: PrefabLink(typeof(InsertPage), typeof(HostVM), typeof(HostVMMixin))]\n" + Mod + InsertPatch("<ModPage DataSource=\\\"{Mod}\\\" />"),
                ("GUI/Prefabs/ModPage.xml", ModPage),
                ("GUI/Prefabs/ModItem.xml", ModItem));
        }

        /// <summary>Verifies that an explicit <c>[assembly: PrefabLink]</c> targets the designated ViewModel directly, bypassing heuristic mixin scope inference.</summary>
        [Test]
        public async Task ALinkedViewModel_IsUsedInsteadOfTheMixins()
        {
            await VerifyAsync(Mod + InsertPatch("<TextWidget {|UIX0016:Text|}=\\\"@Label\\\" />"));
            await VerifyAsync("[assembly: PrefabLink(typeof(InsertPage), typeof(ChildVM))]\n" + Mod + InsertPatch("<TextWidget Text=\\\"@Label\\\" />"));
            await VerifyAsync("[assembly: PrefabLink(typeof(InsertPage), typeof(ChildVM))]\n" + Mod + InsertPatch("<TextWidget {|UIX0015:Text|}=\\\"@Titel\\\" />"));
        }

        [Test]
        public async Task ALinkedPrefab_IsCheckedAgainstItsViewModel()
        {
            await VerifyAsync("[assembly: PrefabLink(\"ModItem\", typeof(ItemVM))]\n" + Mod,
                ("GUI/Prefabs/ModItem.xml", ModItem.Replace("@Name", "@Nam").Replace("<TextWidget Text=", "<TextWidget {|UIX0015:Text|}=")));
        }

        [Test]
        public async Task XmlBindingNoneOfTheLinkedMixinsMembers_IsReported()
        {
            await VerifyAsync("[assembly: PrefabLink(typeof(InsertPage), typeof(HostVM), {|UIX0019:typeof(HostVMMixin)|})]\n" + Mod + InsertPatch("<TextWidget Text=\\\"@Title\\\" />"));
        }

        [Test]
        public async Task EachWayALinkDoesNotHold_IsReported()
        {
            var messages = await MessagesAsync("""
                [assembly: PrefabLink(typeof(HostVM), typeof(HostVM))]
                [assembly: PrefabLink("Nope", typeof(HostVM))]
                [assembly: PrefabLink(typeof(InsertPage), typeof(string))]
                [assembly: PrefabLink(typeof(InsertPage), typeof(HostVM), typeof(ChildVM))]
                [assembly: PrefabLink(typeof(InsertPage), typeof(ChildVM), typeof(HostVMMixin))]
                [assembly: PrefabLink(typeof(InsertPage), typeof(DerivedVM), typeof(HostVMMixin))]
                [assembly: PrefabLink(typeof(InsertPage), typeof(HostVM), typeof(HostVMMixin))]
                [assembly: PrefabLink(typeof(InsertPage), typeof(ChildVM))]

                public class DerivedVM : HostVM { }

                """ + Mod + InsertPatch("<Widget DataSource=\\\"{Mod}\\\" />"));
            Assert.That(messages.Where(m => m.StartsWith("UIX0018", StringComparison.Ordinal)), Is.EquivalentTo(new[]
            {
                "UIX0018: The link is left out: 'HostVM' is not a prefab patch of this mod, a patch class marked [PrefabExtension] that goes in at an XPath",
                "UIX0018: The link is left out: no prefab of this mod is registered under the name 'Nope' or has it as its file name",
                "UIX0018: The link is left out: 'String' is not a ViewModel",
                "UIX0018: The link is left out: 'ChildVM' is not marked [ViewModelMixin]",
                "UIX0018: The link is left out: 'HostVMMixin' extends 'HostVM', not 'ChildVM'",
                "UIX0018: The link is left out: 'HostVMMixin' extends 'HostVM', a base of 'DerivedVM', without handleDerived, so it is not attached to a 'DerivedVM'",
                "UIX0018: The link is left out: 'InsertPage' is already linked to 'HostVM', and its XML binds one ViewModel where it goes in",
            }));
        }
    }

    /// <summary>
    /// Tests diagnostic UIX0025, validating type compatibility across two-way data bindings between widget properties
    /// and ViewModel properties, including conversion rules and announced property types across game versions.
    /// </summary>
    public class BoundTypes
    {
        private const string ViewModels = """
            [assembly: PrefabLink("ModAligned", typeof(AlignedVM))]

            public class AlignedVM : ViewModel
            {
                public TaleWorlds.GauntletUI.VerticalAlignment Align { get; set; }
                public TaleWorlds.GauntletUI.VerticalAlignment FixedAlign => default;
                public string AlignName { get; set; } = "";
                public object AlignObject { get; set; } = null!;
                public int Width { get; set; }
                public float FloatWidth { get; set; }
                public bool IsOn { get; set; }
            }

            """;

        private static (string, string) Prefab(string attributes, string tag = "Widget") => ("GUI/Prefabs/ModAligned.xml", $$"""
            <Prefab>
              <Window>
                <{{tag}} {{attributes}} />
              </Window>
            </Prefab>
            """);

        private const string Widget = "TaleWorlds.GauntletUI.BaseTypes.Widget";

        /// <summary>Creates a test game fixture capturing widget announcement types (e.g. enum values prior to v1.1.0 and string names from v1.1.0 onward).</summary>
        private static TestGame Game(string version = "v1.4.8")
        {
            var announcesName = !version.StartsWith("v1.0.", StringComparison.Ordinal);
            return TestGame.Base().Version(version).Widget(Widget, null,
                ("HorizontalAlignment", [announcesName ? "System.String" : "TaleWorlds.GauntletUI.HorizontalAlignment"]),
                ("IsVisible", ["System.Boolean"]),
                ("SuggestedWidth", ["System.Single"]),
                ("VerticalAlignment", [announcesName ? "System.String" : "TaleWorlds.GauntletUI.VerticalAlignment"]));
        }

        [Test]
        public async Task WhatTheWidgetAnnounces_ThatTheViewModelPropertyCannotTake_IsAnError()
        {
            // Verifies conversion mismatches: string alignment name into enum, and float into int
            await VerifyAsync(ViewModels, [Game()], Prefab("{|UIX0025:VerticalAlignment|}=\"@Align\""));
            await VerifyAsync(ViewModels, [Game()], Prefab("{|UIX0025:SuggestedWidth|}=\"@Width\""));
        }

        /// <summary>Verifies that announcement metadata on a base widget type propagates to derived widget types.</summary>
        [Test]
        public async Task WhatABaseTypeAnnounces_IsCheckedOnADerivedWidget()
        {
            await VerifyAsync(ViewModels, [Game()], Prefab("{|UIX0025:VerticalAlignment|}=\"@Align\"", tag: "ButtonWidget"));
        }

        /// <summary>Verifies that incompatible primitive types without conversion rules report an error even without bundle package metadata.</summary>
        [Test]
        public async Task AViewModelValueTheWidgetPropertyCannotTake_IsAnError_WithoutAPackage()
        {
            await VerifyAsync(ViewModels, Prefab("{|UIX0025:SuggestedWidth|}=\"@IsOn\""));
        }

        /// <summary>Verifies string conversion validation against registered bundle conversion types via <c>ConvertObject</c>.</summary>
        [Test]
        public async Task AString_IsCheckedAgainstWhatThePackageSaysTheLoaderConvertsItInto()
        {
            var converts = Game().StringConversions("System.Int32", "TaleWorlds.TwoDimension.Sprite");
            await VerifyAsync(ViewModels, [converts], Prefab("{|UIX0025:HorizontalAlignment|}=\"@AlignName\""));
            await VerifyAsync(ViewModels, [converts], Prefab("Sprite=\"@AlignName\""));
            await VerifyAsync(ViewModels, [Game().StringConversions("System.Int32")], Prefab("{|UIX0025:Sprite|}=\"@AlignName\""));
        }

        /// <summary>Verifies fallback string conversion types (Sprite, Brush, int, Color) used when bundle package conversion metadata is absent.</summary>
        [Test]
        public async Task AString_WithoutRecordedConversions_IsCheckedAgainstTheFallbackList()
        {
            await VerifyAsync(ViewModels, Prefab("{|UIX0025:HorizontalAlignment|}=\"@AlignName\""));
            await VerifyAsync(ViewModels, [Game()], Prefab("{|UIX0025:HorizontalAlignment|}=\"@AlignName\""));
            await VerifyAsync(ViewModels, Prefab("Sprite=\"@AlignName\""));
            await VerifyAsync(ViewModels, [Game()], Prefab("Sprite=\"@AlignName\""));
        }

        /// <summary>Verifies multi-version evaluation where some versions supply explicit string conversions and others rely on fallback defaults.</summary>
        [Test]
        public async Task AString_IsCheckedPerVersion_ByTheRecordOrTheFallback()
        {
            var properties = new Dictionary<string, string> { ["UIExtenderExGameVersions"] = "v1.0.3;v1.2.12" };
            var bundle = new TestBundle(Game("v1.0.3").StringConversions("System.Int32"), Game("v1.2.12"));
            await VerifyAsync(ViewModels, [bundle], properties, Prefab("{|UIX0024:Sprite|}=\"@AlignName\""));
        }

        /// <summary>Verifies that string conversions supported in only a subset of configured versions report multi-version compatibility diagnostics.</summary>
        [Test]
        public async Task AStringConvertedInSomeVersionsOnly_IsReportedForTheOthers()
        {
            var properties = new Dictionary<string, string> { ["UIExtenderExGameVersions"] = "v1.0.3;v1.2.12" };
            var bundle = new TestBundle(Game("v1.0.3").StringConversions("System.Int32"), Game("v1.2.12").StringConversions("System.Int32", "TaleWorlds.TwoDimension.Sprite"));
            await VerifyAsync(ViewModels, [bundle], properties, Prefab("{|UIX0024:Sprite|}=\"@AlignName\""));
        }

        /// <summary>Verifies that reverse write-back validation from widget to ViewModel is skipped when announcement metadata is unavailable.</summary>
        [Test]
        public async Task WithoutAnnouncementData_TheWriteBackIsNotChecked()
        {
            await VerifyAsync(ViewModels, Prefab("VerticalAlignment=\"@Align\" SuggestedWidth=\"@Width\""));
            await VerifyAsync(ViewModels, [TestGame.Base()], Prefab("VerticalAlignment=\"@Align\" SuggestedWidth=\"@Width\""));
        }

        /// <summary>
        /// Verifies valid assignment rules: properties without public setters are read-only, identical primitives match, and object properties accept any value.
        /// </summary>
        [Test]
        public async Task WhatWorksOrCannotBeTold_ReportsNothing()
        {
            await VerifyAsync(ViewModels, [Game()], Prefab("VerticalAlignment=\"@FixedAlign\" HorizontalAlignment=\"@AlignObject\" SuggestedWidth=\"@FloatWidth\" IsVisible=\"@IsOn\""));
        }

        [Test]
        public async Task InVersionsThatAnnouncedTheValueItself_ItIsReportedForTheLaterOnesOnly()
        {
            var properties = new Dictionary<string, string> { ["UIExtenderExGameVersions"] = "v1.0.3;v1.2.12" };
            var bundle = new TestBundle(Game("v1.0.3"), Game("v1.2.12"));
            await VerifyAsync(ViewModels, [bundle], properties, Prefab("{|UIX0024:VerticalAlignment|}=\"@Align\""));
            // SuggestedWidth announces a float in both
            await VerifyAsync(ViewModels, [bundle], properties, Prefab("{|UIX0025:SuggestedWidth|}=\"@Width\""));

            properties["UIExtenderExGameVersions"] = "v1.0.3";
            await VerifyAsync(ViewModels, [bundle], properties, Prefab("VerticalAlignment=\"@Align\""));
        }

        /// <summary>
        /// Verifies that multi-targeting builds validate bindings against the active version's type definitions when per-version compilations are configured.
        /// </summary>
        [Test]
        public async Task InTheSdksBuildPerVersion_EachBuildIsCheckedForItsOwnVersion()
        {
            Dictionary<string, string> Build(string version) => new()
            {
                ["GameVersion"] = version,
                ["OverrideGameVersion"] = "v" + version,
                ["UIExtenderExGameVersions"] = "v1.0.3;v1.4.8",
            };
            var bundle = new TestBundle(Game("v1.0.3"), Game("v1.4.8"));
            await VerifyAsync(ViewModels, [bundle], Build("1.0.3"), Prefab("VerticalAlignment=\"@Align\""));
            await VerifyAsync(ViewModels, [bundle], Build("1.4.8"), Prefab("{|UIX0025:VerticalAlignment|}=\"@Align\""));
            await VerifyAsync(ViewModels, [bundle], Build("1.4.8"), Prefab("VerticalAlignment=\"@AlignObject\""));
        }
    }

    /// <summary>
    /// Tests UIX0025 on mod-defined widget classes by inspecting source code invocations of <c>OnPropertyChanged</c> to infer announced types.
    /// </summary>
    public class OwnWidgetAnnouncements
    {
        private const string Mod = """
            [assembly: PrefabLink("ModOwnWidget", typeof(OwnWidgetVM))]

            public class Shape { }

            public sealed class RoundShape : Shape { }

            public class OwnWidgetVM : ViewModel
            {
                public Shape Holder { get; set; } = new();
                public RoundShape Round { get; set; } = new();
                public string Text { get; set; } = "";
                public object Anything { get; set; } = null!;
                public int Count { get; set; }
                public float Total { get; set; }
            }

            public class ShapeWidget : TaleWorlds.GauntletUI.BaseTypes.Widget
            {
                public ShapeWidget(TaleWorlds.GauntletUI.UIContext context) : base(context) { }

                private Shape? _shape;
                public Shape? Shape
                {
                    get => _shape;
                    set { _shape = value; OnPropertyChanged(value, nameof(Shape)); }
                }

                private float _amount;
                public float Amount
                {
                    get => _amount;
                    set { _amount = value; OnPropertyChanged(value); }
                }

                private object? _loose;
                public object? Loose
                {
                    get => _loose;
                    set { _loose = value; OnPropertyChanged(value, nameof(Loose)); }
                }

                public int Raised { get; set; }
                public void Raise(string name) => OnPropertyChanged(Raised, name);
            }

            /// <summary>Verifies that announcement inferences propagate from base mod widgets to derived mod widgets.</summary>
            public class DerivedShapeWidget : ShapeWidget
            {
                public DerivedShapeWidget(TaleWorlds.GauntletUI.UIContext context) : base(context) { }
            }

            """;

        private static (string, string) Prefab(string attributes, string tag = "ShapeWidget") => ("GUI/Prefabs/ModOwnWidget.xml", $$"""
            <Prefab>
              <Window>
                <{{tag}} {{attributes}} />
              </Window>
            </Prefab>
            """);

        /// <summary>
        /// Verifies write-back type incompatibility when custom widget <c>OnPropertyChanged</c> calls announce types that the ViewModel setter cannot accept.
        /// </summary>
        [Test]
        public async Task WhatItsSourceAnnounces_ThatTheViewModelPropertyCannotTake_IsAnError_WithoutAPackage()
        {
            await VerifyAsync(Mod, Prefab("{|UIX0025:Shape|}=\"@Round\""));
            // CallerMemberName resolves to Amount, and OnPropertyChanged(float) announces float which cannot bind to an int setter
            await VerifyAsync(Mod, Prefab("{|UIX0025:Amount|}=\"@Count\""));
            await VerifyAsync(Mod, Prefab("{|UIX0025:Shape|}=\"@Round\"", tag: "DerivedShapeWidget"));
        }

        [Test]
        public async Task WhatItsSourceAnnounces_IsCheckedWithAPackageToo()
        {
            await VerifyAsync(Mod, [TestGame.Base()], Prefab("{|UIX0025:Amount|}=\"@Count\""));
        }

        /// <summary>
        /// Verifies that matching types, object properties, and dynamic property names report no diagnostics.
        /// </summary>
        [Test]
        public async Task WhatFitsOrCannotBeTold_ReportsNothing()
        {
            // Shape binds bidirectionally to Shape?: runtime reflection ignores nullable reference annotations
            await VerifyAsync(Mod, Prefab("Shape=\"@Holder\" Amount=\"@Total\""));
            // Object properties accept any announced type
            await VerifyAsync(Mod, Prefab("Shape=\"@Anything\""));
            // Loose announces System.Object; Raise uses non-constant property names which cannot be statically checked
            await VerifyAsync(Mod, Prefab("Loose=\"@Text\" Raised=\"@Count\""));
        }
    }

    /// <summary>Tests diagnostic UIX0026, warning on multi-hop dataSource path expressions that fail to update when intermediate properties change.</summary>
    public class RefreshedWhenReplaced
    {
        private const string ViewModels = """
            [assembly: PrefabLink("ModOwner", typeof(OwnerVM))]

            public class OwnerVM : ViewModel
            {
                public HintVM Hint { get; set; } = new();
                public VisualVM Visual { get; set; } = new();
                public VisualVM FixedVisual { get; } = new();
            }

            public class HintVM : ViewModel
            {
                public VisualVM Inner { get; set; } = new();
            }

            public class VisualVM : ViewModel
            {
                public string Label { get; set; } = "";
            }

            """;

        [Test]
        public async Task AReplaceablePropertyReachedThroughAnotherScope_IsAnError()
        {
            await VerifyAsync(ViewModels, ("GUI/Prefabs/ModOwner.xml", """
                <Prefab>
                  <Window>
                    <Widget>
                      <Children>
                        <Widget DataSource="{Hint}">
                          <Children>
                            <Widget {|UIX0026:DataSource|}="{..\Visual}" />
                          </Children>
                        </Widget>
                        <Widget {|UIX0026:DataSource|}="{Hint\Inner}" />
                      </Children>
                    </Widget>
                  </Window>
                </Prefab>
                """));
        }

        /// <summary>Verifies that single-step dataSource paths, get-only properties, and direct parent traversals without property access report no diagnostics.</summary>
        [Test]
        public async Task WhatXmlRefreshesOrCannotBeReplaced_ReportsNothing()
        {
            await VerifyAsync(ViewModels, ("GUI/Prefabs/ModOwner.xml", """
                <Prefab>
                  <Window>
                    <Widget>
                      <Children>
                        <Widget DataSource="{Visual}" />
                        <Widget DataSource="{Hint}">
                          <Children>
                            <Widget DataSource="{..\FixedVisual}" />
                            <Widget DataSource="{..}" />
                            <Widget DataSource="{Inner}" />
                          </Children>
                        </Widget>
                      </Children>
                    </Widget>
                  </Window>
                </Prefab>
                """));
        }
    }

    /// <summary>Tests diagnostic UIX0027, detecting parameter references in child elements passed into a LogicalChildrenLocation where outer parameter scope is lost.</summary>
    public class PassedChildren
    {
        private const string Outer = """
            <Prefab>
              <Parameters><Parameter Name="Title" DefaultValue="Untitled" /></Parameters>
              <Window>
                <ModFrame>
                  <Children>
                    <TextWidget {|UIX0027:Text|}="*Title" />
                  </Children>
                </ModFrame>
              </Window>
            </Prefab>
            """;

        private const string Frame = """
            <Prefab>
              <Window>
                <Widget>
                  <Children>
                    <Widget><LogicalChildrenLocation /></Widget>
                  </Children>
                </Widget>
              </Window>
            </Prefab>
            """;

        [Test]
        public async Task AParameterReadInPassedChildren_IsAnError()
        {
            await VerifyAsync("", ("GUI/Prefabs/ModPanel.xml", Outer), ("GUI/Prefabs/ModFrame.xml", Frame));
        }

        /// <summary>Verifies that explicitly passing the parameter down to the target widget resolves UIX0027 without triggering UIX0014.</summary>
        [Test]
        public async Task PassedOn_ReportsNothing()
        {
            await VerifyAsync("", ("GUI/Prefabs/ModPanel.xml", Outer.Replace("<ModFrame>", "<ModFrame Parameter.Title=\"*Title\">").Replace("{|UIX0027:Text|}", "Text")),
                ("GUI/Prefabs/ModFrame.xml", Frame));
        }

        /// <summary>Verifies that widgets without a LogicalChildrenLocation retain direct parameter scope for their children.</summary>
        [Test]
        public async Task WithoutALogicalChildrenLocation_ReportsNothing()
        {
            await VerifyAsync("", ("GUI/Prefabs/ModPanel.xml", Outer.Replace("{|UIX0027:Text|}", "Text")),
                ("GUI/Prefabs/ModFrame.xml", Frame.Replace("<LogicalChildrenLocation />", "")));
        }
    }

    /// <summary>Tests diagnostic UIX0028, warning against indexing into list dataSources because bindings do not track list mutations.</summary>
    public class IntoAListByIndex
    {
        private const string ViewModels = """
            [assembly: PrefabLink("ModPerks", typeof(PerksVM))]

            public class PerksVM : ViewModel
            {
                public MBBindingList<SlotVM> Perks { get; } = new();
                public SlotVM Selected { get; set; } = new();
            }

            public class SlotVM : ViewModel
            {
                public MBBindingList<SlotVM> CandidatePerks { get; } = new();
                public string Name { get; set; } = "";
            }

            """;

        [Test]
        public async Task AnIndexIntoAList_IsAnError()
        {
            await VerifyAsync(ViewModels, ("GUI/Prefabs/ModPerks.xml", """
                <Prefab>
                  <Window>
                    <Widget>
                      <Children>
                        <ListPanel {|UIX0028:DataSource|}="{Perks\0\CandidatePerks}">
                          <ItemTemplate><TextWidget Text="@Name" /></ItemTemplate>
                        </ListPanel>
                        <ListPanel DataSource="{Perks}">
                          <ItemTemplate>
                            <Widget>
                              <Children>
                                <TextWidget {|UIX0028:DataSource|}="{..\0}" Text="@Name" />
                              </Children>
                            </Widget>
                          </ItemTemplate>
                        </ListPanel>
                      </Children>
                    </Widget>
                  </Window>
                </Prefab>
                """));
        }

        /// <summary>Verifies that indexed list dataSource paths passed via parameters are reported at the call site providing the argument.</summary>
        [Test]
        public async Task AnIndexHandedAsAParameter_IsReportedWhereItIsWritten()
        {
            await VerifyAsync(ViewModels,
                ("GUI/Prefabs/ModPerks.xml", """
                    <Prefab>
                    <Window>
                        <ModPopup {|UIX0028:Parameter.Source|}="{Perks\1\CandidatePerks}" />
                    </Window>
                    </Prefab>
                    """),
                ("GUI/Prefabs/ModPopup.xml", """
                    <Prefab>
                      <Parameters><Parameter Name="Source" DefaultValue="" /></Parameters>
                      <Window>
                        <ListPanel DataSource="*Source">
                          <ItemTemplate><TextWidget Text="@Name" /></ItemTemplate>
                        </ListPanel>
                      </Window>
                    </Prefab>
                    """));
        }

        /// <summary>Verifies that binding to the list container directly or to individual ViewModel properties reports no diagnostic.</summary>
        [Test]
        public async Task TheListOrAPropertyOfItsOwn_ReportsNothing()
        {
            await VerifyAsync(ViewModels, ("GUI/Prefabs/ModPerks.xml", """
                <Prefab>
                  <Window>
                    <Widget>
                      <Children>
                        <ListPanel DataSource="{Perks}">
                          <ItemTemplate><TextWidget Text="@Name" /></ItemTemplate>
                        </ListPanel>
                        <TextWidget DataSource="{Selected}" Text="@Name" />
                      </Children>
                    </Widget>
                  </Window>
                </Prefab>
                """));
        }
    }

    /// <summary>Tests diagnostic UIX0029, detecting unrecognized XML tags that fall back to standard <c>Widget</c> instances at runtime.</summary>
    public class UnknownTag
    {
        /// <summary>Defines game widget types and prefabs recorded in the mock GUI package.</summary>
        private static TestGame Game() => TestGame.Base()
            .Widget("TaleWorlds.MountAndBlade.GauntletUI.Widgets.GameOnlyWidget", "TaleWorlds.GauntletUI.BaseTypes.Widget")
            .Prefab("GameOnlyPrefab", "<Prefab><Window><Widget /></Window></Prefab>");

        private const string Panel = """
            <Prefab>
              <Window>
                <Widget>
                  <Children>
                    <{|UIX0029:ClanIncomeItem|} />
                    <TextWidget />
                    <GameOnlyWidget />
                    <GameOnlyPrefab />
                    <ModPart />
                  </Children>
                </Widget>
              </Window>
            </Prefab>
            """;

        private const string Part = "<Prefab><Window><Widget /></Window></Prefab>";

        /// <summary>Verifies that unknown tags report a diagnostic while compiled widgets, package widgets, game prefabs, and mod prefabs resolve correctly.</summary>
        [Test]
        public async Task ANameNothingDefines_IsAWarning()
        {
            await VerifyAsync("", [Game()], ("GUI/Prefabs/ModPanel.xml", Panel), ("GUI/Prefabs/ModPart.xml", Part));
        }

        /// <summary>Verifies that unknown tag checks are skipped when game widget package metadata is unavailable.</summary>
        [Test]
        public async Task WithoutTheGamesWidgets_ReportsNothing()
        {
            await VerifyAsync("", ("GUI/Prefabs/ModPanel.xml", Panel.Replace("{|UIX0029:ClanIncomeItem|}", "ClanIncomeItem")), ("GUI/Prefabs/ModPart.xml", Part));
        }
    }
}