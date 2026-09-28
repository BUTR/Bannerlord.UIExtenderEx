using NUnit.Framework;

using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.PrefabVerifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// UIX0020 to UIX0023, and the binding rules against the game's scope: patches applied to the game's own prefabs, from
/// GUI packages in the layout of <c>Bannerlord.ReferenceAssemblies.GUI.v2</c>.
/// </summary>
public partial class GamePrefabRuleTests
{
    /// <summary>The game's ViewModels, in the mod's compilation as the reference assemblies put them there.</summary>
    private const string Mod = """
        public class HostVM : ViewModel
        {
            public string Title { get; set; } = "";
            public ChildVM Child { get; } = new();
            public MBBindingList<ItemVM> Items { get; } = new();
        }

        public class ChildVM : ViewModel
        {
            public string Label { get; set; } = "";
        }

        public class ItemVM : ViewModel
        {
            public string Name { get; set; } = "";
        }

        """;

    private const string HostMovie = """
        <Prefab>
          <Window>
            <Widget Id="Root">
              <Children>
                <ListPanel Id="Panel" DataSource="{Child}">
                  <Children>
                    <TextWidget Id="Label" Text="@Label" />
                    <HostRow Parameter.RowSource="{..\Items}" />
                  </Children>
                </ListPanel>
                <ListPanel Id="Rows" DataSource="{Items}">
                  <ItemTemplate>
                    <Widget Id="Row" />
                  </ItemTemplate>
                </ListPanel>
                <TextWidget Id="Twin" />
                <TextWidget Id="Twin" />
              </Children>
            </Widget>
          </Window>
        </Prefab>
        """;

    /// <summary>A prefab the movie uses by tag, bound to what it is handed.</summary>
    private const string HostRow = """
        <Prefab>
          <Parameters>
            <Parameter Name="RowSource" DefaultValue="" />
          </Parameters>
          <Window>
            <ListPanel Id="RowRoot" DataSource="*RowSource">
              <ItemTemplate>
                <Widget Id="RowItem" />
              </ItemTemplate>
            </ListPanel>
          </Window>
        </Prefab>
        """;

    private static TestGame Game() => TestGame.Base()
        .Movie("HostMovie", "HostVM")
        .ViewModel("HostVM", null, ("Title", "System.String"), ("Child", "ChildVM"), ("Items", "TaleWorlds.Library.MBBindingList<ItemVM>"))
        .ViewModel("ChildVM", null, ("Label", "System.String"))
        .ViewModel("ItemVM", null, ("Name", "System.String"))
        .Prefab("HostMovie", HostMovie)
        .Prefab("HostRow", HostRow);

    private static string Insert(string name, string movie, string xpathMarkup, string type, string xml) => $$"""
        [PrefabExtension("{{movie}}", {{xpathMarkup}})]
        public sealed class {{name}} : PrefabExtensionInsertPatch
        {
            public override InsertType Type => InsertType.{{type}};
            [PrefabExtensionText] public string Content => "{{xml}}";
        }

        """;

    private static string SetAttribute(string name, string xpath, string attributeMarkup) => $$"""
        [PrefabExtension("HostMovie", "{{xpath}}")]
        public sealed class {{name}} : PrefabExtensionSetAttributePatch
        {
            public override List<Attribute> Attributes => [{{attributeMarkup}}];
        }

        """;

    public class XPaths
    {
        [Test]
        public async Task AnXPathThatSelectsANode_ReportsNothing()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Panel']\"", "Child", "<TextWidget Text=\\\"@Label\\\" />"), [Game()]);
        }

        [Test]
        public async Task AnXPathThatSelectsNothing_IsReported()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "{|UIX0020:\"descendant::ListPanel[@Id='Pannel']\"|}", "Child", "<Widget />"), [Game()]);
        }

        [Test]
        public async Task AnXPathThatSelectsSeveralNodes_IsReported()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "{|UIX0021:\"descendant::TextWidget[@Id='Twin']\"|}", "Append", "<Widget />"), [Game()]);
        }

        [Test]
        public async Task AnXPathThatDoesNotParse_IsAnError_WithoutAnyGamePackage()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "{|UIX0023:\"descendant::ListPanel[@Id='Panel'\"|}", "Child", "<Widget />"));
            await VerifyAsync(Mod + Insert("Count", "HostMovie", "{|UIX0023:\"count(//Widget)\"|}", "Child", "<Widget />"));
        }

        /// <summary>
        /// The core passes SelectSingleNode an empty XPath for a patch that names none, which throws.
        /// <c>[PrefabExtension("Movie")]</c> does not compile, as the constructors are ambiguous, but a null does.
        /// </summary>
        [Test]
        public async Task APatchWithoutAnXPath_IsAnError()
        {
            await VerifyAsync(Mod + """
                [PrefabExtension("HostMovie", {|UIX0023:null|})]
                public sealed class Page : PrefabExtensionInsertPatch
                {
                    public override InsertType Type => InsertType.Child;
                    [PrefabExtensionText] public string Content => "<Widget />";
                }
                """);
            var messages = await MessagesAsync(Mod + """
                [PrefabExtension("HostMovie", null)]
                public sealed class Page : PrefabExtensionInsertPatch
                {
                    public override InsertType Type => InsertType.Child;
                    [PrefabExtensionText] public string Content => "<Widget />";
                }

                [PrefabExtension("HostMovie", "count(//Widget)")]
                public sealed class Count : PrefabExtensionInsertPatch
                {
                    public override InsertType Type => InsertType.Child;
                    [PrefabExtensionText] public string Content => "<Widget />";
                }
                """);
            Assert.That(messages, Is.EquivalentTo(new[]
            {
                "UIX0023: The XPath cannot be applied: the patch names none, and SelectSingleNode throws on an empty one",
                "UIX0023: The XPath 'count(//Widget)' cannot be applied: it does not select nodes",
            }));
        }

        /// <summary>Format 1 carried the game's XML; this analyzer reads format 2 only, and leaves the package out.</summary>
        [Test]
        public async Task APackageOfAnotherFormat_IsNotRead()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Pannel']\"", "Child", "<Widget />"), [Game().Format(1)]);
        }

        /// <summary>
        /// The game loads a prefab without its comments, so a position counts the nodes the game counts: in the game's
        /// trees, which carry none, and in the mod's own prefab, whose comments the check leaves out.
        /// </summary>
        [Test]
        public async Task CommentsDoNotCountForPositions()
        {
            const string prefab = "<Prefab><Window><Widget Id=\"Root\"><Children><!-- first --><TextWidget Id=\"A\" /><TextWidget Id=\"B\" /></Children></Widget></Window></Prefab>";
            const string xpath = "\"descendant::Widget[@Id='Root']/Children/node()[2][@Id='B']\"";
            await VerifyAsync(Mod + Insert("Page", "HostMovie", xpath, "Append", "<Widget />"), [TestGame.Base().Prefab("HostMovie", prefab)]);
            await VerifyAsync(Mod + Insert("Page", "HostMovie", xpath, "Append", "<Widget />"), [], ("GUI/Prefabs/HostMovie.xml", prefab));
        }

        [Test]
        public async Task WithoutAGamePackage_TheGamesPrefabsAreNotChecked()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Pannel']\"", "Child", "<Widget />"));
        }

        [Test]
        public async Task APrefabNeitherTheGameNorTheModHas_IsNotChecked()
        {
            await VerifyAsync(Mod + Insert("Page", "AnotherModsMovie", "\"descendant::Widget[@Id='Anything']\"", "Child", "<Widget />"), [Game()]);
        }

        [Test]
        public async Task ANodeAnotherPatchOfTheModInserts_CountsAsThere()
        {
            await VerifyAsync(Mod
                + Insert("First", "HostMovie", "\"descendant::ListPanel[@Id='Panel']\"", "Child", "<Widget Id=\\\"MyModPanel\\\" />")
                + Insert("Second", "HostMovie", "\"descendant::Widget[@Id='MyModPanel']\"", "Child", "<Widget />"), [Game()]);
        }

        [Test]
        public async Task TheModsOwnPrefabOfTheSameName_IsTheOneChecked()
        {
            var messages = await MessagesAsync(Mod + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Panel']\"", "Child", "<Widget />"));
            Assert.That(messages, Is.Empty);

            await VerifyAsync(Mod + Insert("Page", "HostMovie", "{|UIX0020:\"descendant::ListPanel[@Id='Panel']\"|}", "Child", "<Widget />"), [Game()],
                ("GUI/Prefabs/HostMovie.xml", "<Prefab><Window><Widget Id=\"Other\" /></Window></Prefab>"));
        }
    }

    public class Scopes
    {
        [Test]
        public async Task ContentInsertedAsAChild_BindsTheTargetsDataSource()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Panel']\"", "Child",
                "<TextWidget Text=\\\"@Label\\\" {|UIX0015:IntText|}=\\\"@Title\\\" />"), [Game()]);
        }

        [Test]
        public async Task ContentInsertedNextToTheTarget_BindsTheScopeAroundIt()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Panel']\"", "Prepend",
                "<TextWidget Text=\\\"@Title\\\" {|UIX0015:IntText|}=\\\"@Label\\\" />"), [Game()]);
        }

        [Test]
        public async Task ContentInAnItemTemplate_BindsTheListsElements()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "\"descendant::Widget[@Id='Row']\"", "Child",
                "<TextWidget Text=\\\"@Name\\\" {|UIX0015:IntText|}=\\\"@Title\\\" />"), [Game()]);
        }

        /// <summary>A patch on a prefab the movie uses by tag, with its DataSource handed in as a parameter.</summary>
        [Test]
        public async Task APrefabUsedByTag_BindsWhereItIsUsed()
        {
            await VerifyAsync(Mod + Insert("Page", "HostRow", "\"descendant::Widget[@Id='RowItem']\"", "Child",
                "<TextWidget Text=\\\"@Name\\\" {|UIX0015:IntText|}=\\\"@Label\\\" />"), [Game()]);
        }

        /// <summary>Without the game's scope the patch's names would be taken from the mod's mixins, and it has none here.</summary>
        [Test]
        public async Task TheGamesScope_ReplacesTheOneTakenFromTheMixins()
        {
            const string mixin = """
                [ViewModelMixin]
                public sealed class ItemVMMixin : BaseViewModelMixin<ItemVM>
                {
                    public ItemVMMixin(ItemVM vm) : base(vm) { }
                    [DataSourceProperty] public string MyMod_Tag { get; set; } = "";
                }

                """;
            await VerifyAsync(Mod + mixin + Insert("Page", "HostMovie", "\"descendant::Widget[@Id='Row']\"", "Child",
                "<TextWidget Text=\\\"@MyMod_Tag\\\" IntText=\\\"@Name\\\" />"), [Game()]);
        }

        [Test]
        public async Task AGameViewModelTheModDoesNotReference_ChecksNothingBelowIt()
        {
            var game = TestGame.Base().Movie("HostMovie", "Game.UnreferencedVM").ViewModel("Game.UnreferencedVM", null).Prefab("HostMovie", HostMovie);
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "\"descendant::Widget[@Id='Root']\"", "Child", "<TextWidget Text=\\\"@Anything\\\" />"), [game]);
        }
    }

    public class SetAttributes
    {
        [Test]
        public async Task AnAttributeTheTargetWidgetDoesNotHave_IsReported()
        {
            await VerifyAsync(Mod + SetAttribute("Width", "descendant::TextWidget[@Id='Label']", "new Attribute({|UIX0012:\"Txet\"|}, \"x\")"), [Game()]);
        }

        [Test]
        public async Task ABindingTheTargetsScopeDoesNotHave_IsReported()
        {
            await VerifyAsync(Mod + SetAttribute("Width", "descendant::TextWidget[@Id='Label']", "new Attribute(\"Text\", {|UIX0015:\"@Lable\"|})"), [Game()]);
        }
    }

    public class Dlc
    {
        private static TestGame Naval(string hostMovie) => TestGame.NavalDlc().Prefab("HostMovie", hostMovie);

        /// <summary>A DLC ships its own copy of the prefab, without the node: the patch fails for players who own it.</summary>
        [Test]
        public async Task ADlcPrefabWithoutTheNode_IsReportedForTheDlcOnly()
        {
            var messages = await MessagesAsync(Mod + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Panel']\"", "Child", "<Widget />"),
                Game(), Naval("<Prefab><Window><Widget Id=\"Root\" /></Window></Prefab>"));
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0020: 'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie' (with NavalDLC); the patch is not applied",
            }));
        }

        /// <summary>
        /// An entry in the DLC package whose class is a base module's adds a ViewModel to a base screen; it replaces
        /// nothing. Replaced, the base screen's HostVM would be gone with the DLC, and @Title would be reported against
        /// OtherVM alone.
        /// </summary>
        [Test]
        public async Task ADlcEntryOfABaseClass_AddsToTheBaseEntries()
        {
            const string other = "public class OtherVM : ViewModel { }\n";
            var game = TestGame.Base()
                .Movie("HostMovie", "HostVM", overrideView: "View.Overlay")
                .ViewModel("HostVM", null, ("Title", "System.String"))
                .Prefab("HostMovie", HostMovie);
            var naval = TestGame.NavalDlc().Movie("HostMovie", "OtherVM", overrideView: "View.Overlay", module: "SandBox").ViewModel("OtherVM", null);
            await VerifyAsync(Mod + other + Insert("Page", "HostMovie", "\"descendant::Widget[@Id='Root']\"", "Child", "<TextWidget Text=\\\"@Title\\\" />"), [game, naval]);
        }

        [Test]
        public async Task AMovieOnlyTheDlcHas_IsCheckedWithTheDlc()
        {
            var messages = await MessagesAsync(Mod + Insert("Page", "PortScreen", "\"descendant::Widget[@Id='Nope']\"", "Child", "<Widget />"),
                Game(), TestGame.NavalDlc().Prefab("PortScreen", "<Prefab><Window><Widget Id=\"Port\" /></Window></Prefab>"));
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0020: 'descendant::Widget[@Id='Nope']' matches no node of 'PortScreen'; the patch is not applied",
            }));
        }
    }

    /// <summary>
    /// The v1 <c>Prefabs</c> patches go in at the XPath as the <c>Prefabs2</c> ones do, so their XPaths are checked the same
    /// way. Their content is an <c>XmlDocument</c> built at runtime, read only where the class hands <c>LoadXml</c> a literal
    /// or names a file of the mod.
    /// </summary>
    public class V1Patches
    {
        private const string V1 = "Bannerlord.UIExtenderEx.Prefabs.";

        private static string V1Insert(string name, string xpathMarkup, string content) => $$"""
            [PrefabExtension("HostMovie", {{xpathMarkup}})]
            public sealed class {{name}} : {{V1}}PrefabExtensionInsertPatch
            {
                public override string Id => "{{name}}";
                public override int Position => PositionLast;
                public override XmlDocument GetPrefabExtension() { {{content}} }
            }

            """;

        private static string LoadXml(string xml) => $$"""var document = new XmlDocument(); document.LoadXml("{{xml}}"); return document;""";

        [Test]
        public async Task AnXPathOfEachKind_IsChecked()
        {
            await VerifyAsync(Mod + V1Insert("Insert", "{|UIX0020:\"descendant::ListPanel[@Id='Pannel']\"|}", LoadXml("<Widget />")) + $$"""
                [PrefabExtension("HostMovie", {|UIX0023:"descendant::ListPanel["|})]
                public sealed class Replace : {{V1}}PrefabExtensionReplacePatch
                {
                    public override string Id => "Replace";
                    public override XmlDocument GetPrefabExtension() { {{LoadXml("<Widget />")}} }
                }

                [PrefabExtension("HostMovie", {|UIX0021:"descendant::TextWidget[@Id='Twin']"|})]
                public sealed class Sibling : {{V1}}PrefabExtensionInsertAsSiblingPatch
                {
                    public override string Id => "Sibling";
                    public override InsertType Type => InsertType.Prepend;
                    public override XmlDocument GetPrefabExtension() { {{LoadXml("<Widget />")}} }
                }

                """, [Game()]);
        }

        /// <summary>It may insert anything, so it is the only patch of the movie whose XPath is reported missing.</summary>
        [Test]
        public async Task ACustomPatchOfANode_IsChecked()
        {
            await VerifyAsync(Mod + $$"""
                [PrefabExtension("HostMovie", {|UIX0020:"descendant::Widget[@Id='Nowhere']"|})]
                public sealed class Custom : {{V1}}CustomPatch<XmlNode>
                {
                    public override string Id => "Custom";
                    public override void Apply(XmlNode node) { }
                }

                """ + Insert("OnTop", "HostMovie", "\"descendant::Widget[@Id='FromCustom']\"", "Child", "<Widget />"), [Game()]);
        }

        [Test]
        public async Task ACustomPatchOfTheWholeDocument_HasNoXPathToCheck()
        {
            await VerifyAsync(Mod + $$"""
                [PrefabExtension("HostMovie", null)]
                public sealed class Whole : {{V1}}CustomPatch<XmlDocument>
                {
                    public override string Id => "Whole";
                    public override void Apply(XmlDocument document) { }
                }

                """, [Game()]);
        }

        [Test]
        public async Task ASetAttributePatch_IsCheckedAgainstTheWidget()
        {
            await VerifyAsync(Mod + $$"""
                [PrefabExtension("HostMovie", "descendant::TextWidget[@Id='Label']")]
                public sealed class Text : {{V1}}PrefabExtensionSetAttributePatch
                {
                    public override string Id => "Text";
                    public override string Attribute => {|UIX0012:"Txet"|};
                    public override string Value => "x";
                }

                """, [Game()]);
        }

        [Test]
        public async Task TheXmlALoadXmlLiteralInserts_CountsAsThere()
        {
            await VerifyAsync(Mod
                + V1Insert("Base", "\"descendant::ListPanel[@Id='Panel']\"", LoadXml("<Widget Id=\\\"FromOld\\\" />"))
                + Insert("OnTop", "HostMovie", "\"descendant::Widget[@Id='FromOld']\"", "Child", "<Widget />")
                + Insert("Missing", "HostMovie", "{|UIX0020:\"descendant::Widget[@Id='FromNowhere']\"|}", "Child", "<Widget />"), [Game()]);
        }

        [Test]
        public async Task TheFileAModulePatchNames_IsItsContent()
        {
            await VerifyAsync(Mod + $$"""
                [PrefabExtension("HostMovie", "descendant::ListPanel[@Id='Panel']")]
                public sealed class FromFile : {{V1}}ModulePrefabExtensionInsertPatch
                {
                    public FromFile() : base("OldPanel", "MyMod") { }
                    public override string Id => "FromFile";
                    public override int Position => PositionLast;
                }

                """
                + Insert("OnTop", "HostMovie", "\"descendant::Widget[@Id='FromFile']\"", "Child", "<Widget />")
                + Insert("Missing", "HostMovie", "{|UIX0020:\"descendant::Widget[@Id='FromNowhere']\"|}", "Child", "<Widget />"),
                [Game()], ("GUI/PrefabExtensions/OldPanel.xml", "<Widget Id=\"FromFile\" />"));
        }
    }

    /// <summary>A patch whose XML the build cannot read may insert the node another patch goes into.</summary>
    public class UnreadChanges
    {
        [Test]
        public async Task ANodeAPatchBuiltAtRuntimeMayInsert_IsNotReported()
        {
            await VerifyAsync(Mod + """
                [PrefabExtension("HostMovie", {|UIX0020:"descendant::ListPanel[@Id='Pannel']"|})]
                public sealed class Built : PrefabExtensionInsertPatch
                {
                    public override InsertType Type => InsertType.Child;
                    [PrefabExtensionXmlNode] public XmlNode GetContent() => Build();
                    private static XmlNode Build() => new XmlDocument().CreateElement("Widget");
                }

                """ + Insert("OnTop", "HostMovie", "\"descendant::Widget[@Id='Built']\"", "Child", "<Widget />"), [Game()]);
        }

        [Test]
        public async Task ACustomPatchOfTheWholeDocument_MayInsertAnyNode()
        {
            await VerifyAsync(Mod + """
                [PrefabExtension("HostMovie", null)]
                public sealed class Whole : Bannerlord.UIExtenderEx.Prefabs.CustomPatch<XmlDocument>
                {
                    public override string Id => "Whole";
                    public override void Apply(XmlDocument document) { }
                }

                """ + Insert("OnTop", "HostMovie", "\"descendant::Widget[@Id='Custom']\"", "Child", "<Widget />"), [Game()]);
        }

        [Test]
        public async Task AnotherMoviesUnreadPatch_ChangesNothingHere()
        {
            await VerifyAsync(Mod + """
                [PrefabExtension("OtherMovie", null)]
                public sealed class Whole : Bannerlord.UIExtenderEx.Prefabs.CustomPatch<XmlDocument>
                {
                    public override string Id => "Whole";
                    public override void Apply(XmlDocument document) { }
                }

                """ + Insert("OnTop", "HostMovie", "{|UIX0020:\"descendant::Widget[@Id='Custom']\"|}", "Child", "<Widget />"), [Game()]);
        }
    }

    public class Links
    {
        [Test]
        public async Task ALinkToAnotherViewModelThanTheGamesAtTheNode_IsReported()
        {
            await VerifyAsync("[assembly: PrefabLink(typeof(Page), {|UIX0022:typeof(ItemVM)|})]\n" + Mod
                + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Panel']\"", "Child", "<TextWidget Text=\\\"@Name\\\" />"), [Game()]);
        }

        [Test]
        public async Task ALinkThatAgreesWithTheGame_ReportsNothing()
        {
            await VerifyAsync("[assembly: PrefabLink(typeof(Page), typeof(ChildVM))]\n" + Mod
                + Insert("Page", "HostMovie", "\"descendant::ListPanel[@Id='Panel']\"", "Child", "<TextWidget Text=\\\"@Label\\\" />"), [Game()]);
        }
    }
}
