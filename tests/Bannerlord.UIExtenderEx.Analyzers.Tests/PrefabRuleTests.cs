using NUnit.Framework;

using System;
using System.Linq;
using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.PrefabVerifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>UIX0011 to UIX0019: the prefab rules, on a mod shaped like MCM's options page.</summary>
public class PrefabRuleTests
{
    /// <summary>
    /// A game ViewModel with a mixin that adds a child ViewModel and a width, a patch that inserts the mod's page under the
    /// child, and the page's items in a list.
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

        /// <summary>The runtime binds the member to a delegate returning the attribute's type, which a covariant return satisfies.</summary>
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
        /// Until UIExtenderEx 3.0 made it obsolete and sent it the XmlNode way, [PrefabExtensionXmlDocument] binds a delegate
        /// returning XmlDocument, which an XmlNode does not satisfy.
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

        /// <summary>A prefab may be rooted at Window, without Prefab around it, as 36 of the game's are.</summary>
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

        /// <summary>A mod prefab inserted where its names do not resolve: the patch's scope has none of them.</summary>
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

        /// <summary>The loader takes every child of Parameters, whatever its tag; the game's own prefabs misspell it.</summary>
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

        /// <summary>MCM's selectors: the list's DataSource handed in as a parameter, and the scope followed through it.</summary>
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

        /// <summary>MCM's patches bind one name each; a misspelt one matches no mixin, which is itself the report.</summary>
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

        /// <summary>A patch binding a game member alongside a mixin's is checked on the mixin's ViewModel, the game member included.</summary>
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

        /// <summary>A patch binding only a game ViewModel's members: inferred it would be UIX0016, linked it is checked against that ViewModel.</summary>
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
}
