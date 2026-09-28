using NUnit.Framework;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.PrefabVerifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

public partial class GamePrefabRuleTests
{
    /// <summary>
    /// The <c>GUI.v2.All</c> package: every game version in one, each patch checked in every version the mod supports,
    /// and a finding that holds for some of them only reported as UIX0024.
    /// </summary>
    public class Bundles
    {
        private const string Panel = "\"descendant::ListPanel[@Id='Panel']\"";

        /// <summary>The game of <see cref="Game"/>, of another version, with the node of <see cref="Panel"/> renamed.</summary>
        private static TestGame WithoutPanel(string version) => TestGame.Base().Version(version)
            .Movie("HostMovie", "HostVM")
            .ViewModel("HostVM", null, ("Title", "System.String"), ("Child", "ChildVM"), ("Items", "TaleWorlds.Library.MBBindingList<ItemVM>"))
            .ViewModel("ChildVM", null, ("Label", "System.String"))
            .ViewModel("ItemVM", null, ("Name", "System.String"))
            .Prefab("HostMovie", HostMovie.Replace("Id=\"Panel\"", "Id=\"Other\""))
            .Prefab("HostRow", HostRow);

        private static TestGame Of(string version) => Game().Version(version);

        private static Dictionary<string, string> Build(string gameVersion, string? supported = null, bool oneOfSeveral = false)
        {
            var properties = new Dictionary<string, string> { ["GameVersion"] = gameVersion };
            if (supported is not null)
                properties["UIExtenderExGameVersions"] = supported;
            if (oneOfSeveral)
                properties["OverrideGameVersion"] = "v" + gameVersion;
            return properties;
        }

        /// <summary>The bundle is read into the packages it holds: every rule reports as with the per-build package.</summary>
        [Test]
        public async Task ABundle_ReportsWhatItsPackagesReport()
        {
            var mod = Mod
                + Insert("Missing", "HostMovie", "\"descendant::ListPanel[@Id='Pannel']\"", "Child", "<Widget />")
                + Insert("Twins", "HostMovie", "\"descendant::TextWidget[@Id='Twin']\"", "Append", "<Widget />")
                + Insert("Binding", "HostMovie", Panel, "Child", "<TextWidget Text=\\\"@Lable\\\" />");
            var dlc = TestGame.NavalDlc().Prefab("HostMovie", "<Prefab><Window><Widget Id=\"Root\" /></Window></Prefab>");
            var perBuild = await MessagesAsync(mod, Game(), dlc);
            var bundled = await MessagesAsync(mod, new TestBundle(Game(), dlc));
            Assert.That(perBuild, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(bundled, Is.EquivalentTo(perBuild));
        }

        [Test]
        public async Task AnXPathMissingInSomeSupportedVersions_IsUIX0024()
        {
            var messages = await MessagesAsync(Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />"),
                Build("1.4.8", "v1.3.4;v1.2.12;v1.4.8"), new TestBundle(WithoutPanel("v1.2.12"), WithoutPanel("v1.3.4"), Of("v1.4.8")));
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0024: [v1.4.8] In v1.2.12, v1.3.4 only: 'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie'; the patch is not applied",
            }));
        }

        /// <summary>Three or more versions in a row among those checked read as a range.</summary>
        [Test]
        public async Task VersionsInARow_ReadAsARange()
        {
            var messages = await MessagesAsync(Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />"),
                Build("1.4.8", "v1.2.9,v1.2.12,v1.3.4,v1.4.7,v1.4.8"),
                new TestBundle(WithoutPanel("v1.2.9"), WithoutPanel("v1.2.12"), WithoutPanel("v1.3.4"), Of("v1.4.7"), WithoutPanel("v1.4.8")));
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0024: [v1.4.8] In v1.2.9 to v1.3.4, v1.4.8 only: 'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie'; the patch is not applied",
            }));
        }

        [Test]
        public async Task AnXPathMissingInEverySupportedVersion_IsUIX0020()
        {
            var messages = await MessagesAsync(Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />"),
                Build("1.4.8", "v1.3.4;v1.4.8"), new TestBundle(WithoutPanel("v1.3.4"), WithoutPanel("v1.4.8")));
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0020: [v1.4.8] 'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie'; the patch is not applied",
            }));
        }

        /// <summary>Within a version, a finding that holds with a DLC only says so, as for the per-build packages.</summary>
        [Test]
        public async Task AFindingOfOneVersionsDlc_NamesTheVersionAndTheDlc()
        {
            var dlc = TestGame.NavalDlc().Version("v1.4.8").Prefab("HostMovie", "<Prefab><Window><Widget Id=\"Root\" /></Window></Prefab>");
            var messages = await MessagesAsync(Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />"),
                Build("1.4.8", "v1.3.4;v1.4.8"), new TestBundle(Of("v1.3.4"), Of("v1.4.8"), dlc));
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0024: [v1.4.8] In v1.4.8 with NavalDLC only: 'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie'; the patch is not applied",
            }));
        }

        [Test]
        public async Task AVersionTheModDoesNotSupport_IsNotChecked()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            var bundle = new TestBundle(WithoutPanel("v1.3.4"), Of("v1.4.8"));
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.4.8"), bundle), Is.Empty);
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.3.4;v1.4.8"), bundle), Has.Count.EqualTo(1), "Supported, it is checked");
        }

        /// <summary>Without a list of supported versions, the version the project builds against is the one checked.</summary>
        [Test]
        public async Task WithoutSupportedVersions_TheBuildsVersionIsChecked()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            var bundle = new TestBundle(WithoutPanel("v1.3.4"), Of("v1.4.8"));
            Assert.That(await MessagesAsync(mod, Build("1.4.8"), bundle), Is.Empty);
            Assert.That(await MessagesAsync(mod, Build("1.3.4"), bundle), Is.EqualTo(new[]
            {
                "UIX0020: [v1.3.4] 'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie'; the patch is not applied",
            }));
        }

        /// <summary>For a version both have, the per-build package the project references is the one checked.</summary>
        [Test]
        public async Task TheReferencedPackage_WinsOverTheBundleForItsVersion()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            var bundle = new TestBundle(WithoutPanel("v1.4.8"));
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.4.8"), Game(), bundle), Is.Empty);
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.4.8"), bundle), Has.Count.EqualTo(1), "The bundle alone reports");
        }

        /// <summary>
        /// Bannerlord.BUTRModule.Sdk builds the module once per supported version, and each build sees every version: the
        /// finding is reported by the build of the newest version it holds for alone.
        /// </summary>
        [Test]
        public async Task InTheSdksBuildPerVersion_OnlyTheNewestVersionsBuildReports()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            var bundle = new TestBundle(WithoutPanel("v1.2.12"), WithoutPanel("v1.3.4"), Of("v1.4.8"));
            const string supported = "v1.2.12;v1.3.4;v1.4.8";
            Assert.That(await MessagesAsync(mod, Build("1.4.8", supported, oneOfSeveral: true), bundle), Is.Empty);
            Assert.That(await MessagesAsync(mod, Build("1.2.12", supported, oneOfSeveral: true), bundle), Is.Empty);
            Assert.That(await MessagesAsync(mod, Build("1.3.4", supported, oneOfSeveral: true), bundle), Is.EqualTo(new[]
            {
                "UIX0024: [v1.3.4] In v1.2.12, v1.3.4 only: 'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie'; the patch is not applied",
            }));
        }

        /// <summary>
        /// The patch's content is checked against the scope at the node in the version the compilation builds against:
        /// its types are that version's. It is reported once, not once per version.
        /// </summary>
        [Test]
        public async Task TheContentOfAPatch_IsCheckedOnceAgainstTheBuildsVersion()
        {
            var messages = await MessagesAsync(Mod + Insert("Page", "HostMovie", Panel, "Child", "<TextWidget Text=\\\"@Lable\\\" />"),
                Build("1.4.8", "v1.3.4;v1.4.8"), new TestBundle(Of("v1.3.4"), Of("v1.4.8")));
            Assert.That(messages.Where(m => m.StartsWith("UIX0015")).ToList(), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ABundleOfAnotherLayout_IsNotRead()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.4.8"), new TestBundle(WithoutPanel("v1.4.8")).Layout(2)), Is.Empty);
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.4.8"), new TestBundle(WithoutPanel("v1.4.8"))), Has.Count.EqualTo(1), "Layout 1 is read");
        }

        /// <summary>UIX0024 is placed where UIX0020 would be, on the XPath.</summary>
        [Test]
        public async Task UIX0024_IsReportedOnTheXPath()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "{|UIX0024:" + Panel + "|}", "Child", "<Widget />"),
                [new TestBundle(WithoutPanel("v1.3.4"), Of("v1.4.8"))], Build("1.4.8", "v1.3.4;v1.4.8"));
        }
    }
}
