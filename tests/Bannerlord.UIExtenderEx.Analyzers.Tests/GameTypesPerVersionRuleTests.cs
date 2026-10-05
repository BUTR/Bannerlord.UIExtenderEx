using NUnit.Framework;

using System.Collections.Generic;
using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.PrefabVerifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

public partial class GamePrefabRuleTests
{
    /// <summary>
    /// Tests multi-version compatibility checks (UIX0024 wrapping UIX0012, UIX0013, UIX0015, and UIX0022),
    /// validating prefab patches and markup across all supported game versions specified in project configuration
    /// rather than only the target compilation version.
    /// </summary>
    public class TypesPerVersion
    {
        private const string Root = "\"descendant::Widget[@Id='Root']\"";
        private const string Panel = "\"descendant::ListPanel[@Id='Panel']\"";
        private const string Widget = "TaleWorlds.GauntletUI.BaseTypes.Widget";

        private static Dictionary<string, string> Build() => new()
        {
            ["GameVersion"] = "1.4.8",
            ["UIExtenderExGameVersions"] = "v1.3.4;v1.4.8",
        };

        /// <summary>Builds a v1.3.4 game fixture where HostVM lacks <c>Title</c> and <c>ExecuteDone</c>.</summary>
        private static TestGame Older() => TestGame.Base().Version("v1.3.4")
            .Movie("HostMovie", "HostVM")
            .ViewModel("HostVM", null, ("Child", "ChildVM"), ("Items", "TaleWorlds.Library.MBBindingList<ItemVM>"))
            .ViewModel("ChildVM", null, ("Label", "System.String"))
            .ViewModel("ItemVM", null, ("Name", "System.String"))
            .Prefab("HostMovie", HostMovie)
            .Prefab("HostRow", HostRow);

        private static TestGame Newer() => TestGame.Base().Version("v1.4.8")
            .Movie("HostMovie", "HostVM")
            .ViewModel("HostVM", null, ["ExecuteDone"], ("Title", "System.String"), ("Child", "ChildVM"), ("Items", "TaleWorlds.Library.MBBindingList<ItemVM>"))
            .ViewModel("ChildVM", null, ("Label", "System.String"))
            .ViewModel("ItemVM", null, ("Name", "System.String"))
            .Prefab("HostMovie", HostMovie)
            .Prefab("HostRow", HostRow);

        [Test]
        public async Task ABindingAnOlderSupportedVersionLacks_IsUIX0024()
        {
            var messages = await MessagesAsync(Mod + Insert("Page", "HostMovie", Root, "Child", "<TextWidget Text=\\\"@Title\\\" />"),
                Build(), new TestBundle(Older(), Newer()));
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0024: [v1.4.8] In v1.3.4 only: 'HostVM' has no property 'Title', and no mixin of this mod adds one",
            }));
        }

        [Test]
        public async Task ACommandAnOlderSupportedVersionLacks_IsUIX0024()
        {
            await VerifyAsync(Mod.Replace("public string Title", "public void ExecuteDone() { }\n    public string Title") + Insert("Page", "HostMovie", Root, "Child",
                "<ButtonWidget {|UIX0024:Command.Click|}=\\\"ExecuteDone\\\" />"), [new TestBundle(Older(), Newer())], Build());
        }

        /// <summary>Verifies that versions not listed in supported game versions are excluded from validation.</summary>
        [Test]
        public async Task OnlyTheSupportedVersions_AreChecked()
        {
            var properties = Build();
            properties["UIExtenderExGameVersions"] = "v1.4.8";
            await VerifyAsync(Mod + Insert("Page", "HostMovie", Root, "Child", "<TextWidget Text=\\\"@Title\\\" />"), [new TestBundle(Older(), Newer())], properties);
        }

        /// <summary>Verifies that properties introduced by mod ViewModel mixins are recognized as present across all game versions.</summary>
        [Test]
        public async Task ANameAMixinAdds_IsThereInEveryVersion()
        {
            const string mixin = """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                    [DataSourceProperty] public string MyMod_Title { get; set; } = "";
                }

                """;
            await VerifyAsync(Mod + mixin + Insert("Page", "HostMovie", Root, "Child", "<TextWidget Text=\\\"@MyMod_Title\\\" />"),
                [new TestBundle(Older(), Newer())], Build());
        }

        /// <summary>
        /// Verifies that game ViewModels are validated against GUI package type metadata: members missing in specific supported versions
        /// report UIX0024, whereas members absent from all versions report standard UIX0015 regardless of the active compilation types.
        /// </summary>
        [Test]
        public async Task TheGamesViewModels_AreCheckedAgainstThePackages()
        {
            var bundle = new TestBundle(Older(), Newer());
            await VerifyAsync(Mod + Insert("Page", "HostMovie", Panel, "Child", "<TextWidget Text=\\\"@Label\\\" />"), [bundle], Build());
            var noTitle = Mod.Replace("public string Title { get; set; } = \"\";", "");
            await VerifyAsync(noTitle + Insert("Page", "HostMovie", Root, "Child", "<TextWidget {|UIX0024:Text|}=\\\"@Title\\\" />"), [bundle], Build());
            var extra = Mod.Replace("public string Title", "public string Extra { get; set; } = \"\";\n    public string Title");
            await VerifyAsync(extra + Insert("Page", "HostMovie", Root, "Child", "<TextWidget {|UIX0015:Text|}=\\\"@Extra\\\" />"), [bundle], Build());
        }

        /// <summary>Verifies that UIX0024 is reported when a prefab link targets a ViewModel type that diverges from the game binding in an older supported version.</summary>
        [Test]
        public async Task ALinkThatDisagreesInAnOlderVersion_IsUIX0024()
        {
            var older = TestGame.Base().Version("v1.3.4")
                .Movie("HostMovie", "HostVM")
                .ViewModel("HostVM", null, ("Child", "Game.OtherVM"), ("Items", "TaleWorlds.Library.MBBindingList<ItemVM>"))
                .ViewModel("Game.OtherVM", null, ("Label", "System.String"))
                .ViewModel("ChildVM", null, ("Label", "System.String"))
                .ViewModel("ItemVM", null, ("Name", "System.String"))
                .Prefab("HostMovie", HostMovie)
                .Prefab("HostRow", HostRow);
            var messages = await MessagesAsync("[assembly: PrefabLink(typeof(Page), typeof(ChildVM))]\n" + Mod
                + Insert("Page", "HostMovie", Panel, "Child", "<TextWidget Text=\\\"@Label\\\" />"), Build(), new TestBundle(older, Newer()));
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0024: [v1.4.8] In v1.3.4 only: 'Page' is linked to 'ChildVM', but the game binds 'OtherVM' where the patch goes in",
            }));
        }

        /// <summary>Verifies that set-attribute patch properties are validated against widget metadata per supported game version.</summary>
        [Test]
        public async Task ASetAttributeAnOlderVersionsWidgetLacks_IsUIX0024()
        {
            TestGame WithWidgets(TestGame game, params (string Name, string Type)[] textWidget) => game
                .Widget(Widget, null, [("IsVisible", "System.Boolean")])
                .Widget("TaleWorlds.GauntletUI.BaseTypes.TextWidget", Widget, textWidget);
            var bundle = new TestBundle(WithWidgets(Older(), ("Text", "System.String")), WithWidgets(Newer(), ("Text", "System.String"), ("Brightness", "System.Single")));

            await VerifyAsync(Mod + SetAttribute("Bright", "descendant::TextWidget[@Id='Label']", "new Attribute({|UIX0024:\"Brightness\"|}, \"1\")"), [bundle], Build());
            // Inherited from the base Widget record, present in both versions
            await VerifyAsync(Mod + SetAttribute("Visible", "descendant::TextWidget[@Id='Label']", "new Attribute(\"IsVisible\", \"true\")"), [bundle], Build());
            await VerifyAsync(Mod + SetAttribute("Typo", "descendant::TextWidget[@Id='Label']", "new Attribute({|UIX0012:\"Txet\"|}, \"x\")"), [bundle], Build());
        }

        /// <summary>Verifies that UIX0024 is reported when an enum value is valid in one supported version but missing in another.</summary>
        [Test]
        public async Task AnEnumValueAnOlderVersionLacks_IsUIX0024()
        {
            TestGame WithKind(TestGame game, params string[] members) => game
                .Widget("TaleWorlds.GauntletUI.BaseTypes.TextWidget", null, [("Kind", "Game.TextKind")])
                .Enum("Game.TextKind", members);
            var bundle = new TestBundle(WithKind(Older(), "Plain"), WithKind(Newer(), "Plain", "Rich"));

            var messages = await MessagesAsync(Mod + SetAttribute("Rich", "descendant::TextWidget[@Id='Label']", "new Attribute(\"Kind\", \"Rich\")"), Build(), bundle);
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0024: [v1.4.8] In v1.3.4 only: 'Rich' is not a value 'Kind' can take: TextKind has no member 'Rich'",
            }));
            await VerifyAsync(Mod + SetAttribute("Plain", "descendant::TextWidget[@Id='Label']", "new Attribute(\"Kind\", \"Plain\")"), [bundle], Build());
        }

        private static string Page(string widgets) => $$"""
            <Prefab>
              <Window>
                <ListPanel>
                  <Children>
                    {{widgets}}
                  </Children>
                </ListPanel>
              </Window>
            </Prefab>
            """;

        /// <summary>Creates a test bundle with base Widget and TextWidget records across both game versions.</summary>
        private static TestBundle TextWidgets((string Name, string Type)[] older, (string Name, string Type)[] newer)
        {
            TestGame With(TestGame game, (string Name, string Type)[] properties) => game
                .Widget(Widget, null, [("IsVisible", "System.Boolean")])
                .Widget("TaleWorlds.GauntletUI.BaseTypes.TextWidget", Widget, properties);
            return new TestBundle(With(Older(), older), With(Newer(), newer));
        }

        /// <summary>
        /// Verifies that widget attributes in mod prefab files are checked across all supported game versions using bundle records,
        /// rather than only against the compilation target.
        /// </summary>
        [Test]
        public async Task AnAttributeInTheModsOwnPrefab_IsCheckedInEveryVersion()
        {
            var bundle = TextWidgets([("Text", "System.String")], [("Text", "System.String"), ("Brightness", "System.Single")]);

            await VerifyAsync(Mod, [bundle], Build(), ("GUI/Prefabs/ModPage.xml", Page("<TextWidget {|UIX0024:Brightness|}=\"1\" Text=\"x\" IsVisible=\"true\" />")));
            await VerifyAsync(Mod, [bundle], Build(), ("GUI/Prefabs/ModPage.xml", Page("<TextWidget {|UIX0012:Txet|}=\"x\" />")));
            // Validated against packages rather than compilation: TextWidget has Brush in the compilation assembly, but no version record defines it
            await VerifyAsync(Mod, [bundle], Build(), ("GUI/Prefabs/ModPage.xml", Page("<TextWidget {|UIX0012:Brush|}=\"x\" />")));
        }

        [Test]
        public async Task AnEnumValueInTheModsOwnPrefabAnOlderVersionLacks_IsUIX0024()
        {
            TestGame WithKind(TestGame game, params string[] members) => game
                .Widget("TaleWorlds.GauntletUI.BaseTypes.TextWidget", null, [("Kind", "Game.TextKind")])
                .Enum("Game.TextKind", members);
            var bundle = new TestBundle(WithKind(Older(), "Plain"), WithKind(Newer(), "Plain", "Rich"));

            await VerifyAsync(Mod, [bundle], Build(), ("GUI/Prefabs/ModPage.xml", Page("<TextWidget {|UIX0024:Kind|}=\"Rich\" />")));
        }

        /// <summary>
        /// Tests code fixes for UIX0024 diagnostics, suggesting alternatives compatible across all supported game versions.
        /// </summary>
        public class Fixes
        {
            private static CodeFixVerifier.Game Game(TestBundle bundle) => new([bundle], Build());

            [Test]
            public async Task AnAttributeSomeVersionsLack_TakesANameEveryVersionHas()
            {
                var bundle = TextWidgets([("Text", "System.String"), ("Glow", "System.Single")], [("Text", "System.String"), ("Glow", "System.Single"), ("Glows", "System.Single")]);
                await CodeFixVerifier.VerifyAsync("UIX0024",
                    Mod + SetAttribute("Glowing", "descendant::TextWidget[@Id='Label']", "new Attribute(\"Glows\", \"1\")"),
                    Mod + SetAttribute("Glowing", "descendant::TextWidget[@Id='Label']", "new Attribute(\"Glow\", \"1\")"),
                    [], "Change to 'Glow'", game: Game(bundle));
            }

            /// <summary>Verifies that no code fix is offered when a property is renamed across versions and no single identifier satisfies both.</summary>
            [Test]
            public async Task AnAttributeRenamedBetweenTheVersions_HasNoFix()
            {
                var bundle = TextWidgets([("Text", "System.String"), ("Glow", "System.Single")], [("Text", "System.String"), ("Glows", "System.Single")]);
                var titles = await CodeFixVerifier.TitlesAsync("UIX0024",
                    Mod + SetAttribute("Glowing", "descendant::TextWidget[@Id='Label']", "new Attribute(\"Glows\", \"1\")"), Game(bundle));
                Assert.That(titles, Is.Empty);
            }

            [Test]
            public async Task AnEnumValueSomeVersionsLack_TakesAMemberEveryVersionHas()
            {
                TestGame WithKind(TestGame game, params string[] members) => game
                    .Widget("TaleWorlds.GauntletUI.BaseTypes.TextWidget", null, [("Kind", "Game.TextKind")])
                    .Enum("Game.TextKind", members);
                var bundle = new TestBundle(WithKind(Older(), "Plain", "Bold"), WithKind(Newer(), "Plain", "Bold", "Bolder"));
                await CodeFixVerifier.VerifyAsync("UIX0024", Mod, Mod,
                    [("GUI/Prefabs/ModPage.xml", Page("<TextWidget Kind=\"Bolder\" />"), Page("<TextWidget Kind=\"Bold\" />"))],
                    "Change to 'Bold'", game: Game(bundle));
            }

            /// <summary>Verifies that code fixes only suggest candidate members that exist across all supported game versions.</summary>
            [Test]
            public async Task ABindingSomeVersionsLack_TakesAMemberEveryVersionHas()
            {
                TestGame With(TestGame game, params (string Name, string Type)[] properties) => game
                    .Movie("HostMovie", "HostVM")
                    .ViewModel("HostVM", null, [.. properties, ("Child", "ChildVM"), ("Items", "TaleWorlds.Library.MBBindingList<ItemVM>")])
                    .ViewModel("ChildVM", null, ("Label", "System.String"))
                    .ViewModel("ItemVM", null, ("Name", "System.String"))
                    .Prefab("HostMovie", HostMovie)
                    .Prefab("HostRow", HostRow);
                var bundle = new TestBundle(
                    With(TestGame.Base().Version("v1.3.4"), ("Caption", "System.String")),
                    With(TestGame.Base().Version("v1.4.8"), ("Caption", "System.String"), ("Captions", "System.String")));
                var mod = Mod.Replace("public string Title", "public string Caption { get; set; } = \"\";\n    public string Captions { get; set; } = \"\";\n    public string Title");
                await CodeFixVerifier.VerifyAsync("UIX0024",
                    mod + Insert("Page", "HostMovie", Root, "Child", "<TextWidget Text=\\\"@Captions\\\" />"),
                    mod + Insert("Page", "HostMovie", Root, "Child", "<TextWidget Text=\\\"@Caption\\\" />"),
                    [], "Change to 'Caption'", game: Game(bundle));
            }
        }
    }
}