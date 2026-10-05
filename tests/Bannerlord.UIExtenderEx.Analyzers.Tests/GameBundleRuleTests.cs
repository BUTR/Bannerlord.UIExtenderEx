using NUnit.Framework;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.PrefabVerifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

public partial class GamePrefabRuleTests
{
    /// <summary>
    /// Tests for <c>GUI.v3.All</c> bundle integration: multi-version prefab and patch verification,
    /// version filtering, and UIX0024 diagnostic reporting across version subsets.
    /// </summary>
    public class Bundles
    {
        private const string Panel = "\"descendant::ListPanel[@Id='Panel']\"";

        /// <summary>Creates a mock game configuration for the specified version with the panel element ID modified.</summary>
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

        /// <summary>Verifies that bundled packages produce identical diagnostics to standalone per-build packages.</summary>
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

        /// <summary>Verifies that sequences of three or more consecutive versions are formatted as contiguous ranges in UIX0024 messages.</summary>
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

        /// <summary>Verifies that diagnostics isolated to a DLC configuration report both the version and the DLC name.</summary>
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

        /// <summary>Verifies that absent an explicit supported versions list, the current target build version is checked.</summary>
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

        /// <summary>
        /// Verifies that supported game versions absent from referenced GUI bundles trigger UIX0030 warnings.
        /// </summary>
        [Test]
        public async Task AVersionTheBundleDoesNotHave_IsUIX0030()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            var bundle = new TestBundle(Of("v1.3.4"), Of("v1.4.8"));
            Assert.That(await MessagesAsync(mod, Build("1.5.3", "v1.4.8;v1.5.1;v1.5.3"), bundle), Is.EqualTo(new[]
            {
                "UIX0030: [v1.5.3] The game's GUI data has no v1.5.1, v1.5.3, so the patches are not checked against them; the newest the bundle holds is v1.4.8. Reference Bannerlord.ReferenceAssemblies.GUI.v3.All with Version=\"*\" for a newer bundle, or the build's Bannerlord.ReferenceAssemblies.GUI.v3 package.",
            }));
            Assert.That(await MessagesAsync(mod, new Dictionary<string, string> { ["UIExtenderExInferredGameVersion"] = "v1.5.3" }, bundle),
                Has.Exactly(1).StartsWith("UIX0030: [v1.5.3] The game's GUI data has no v1.5.3, so the patches are not checked against it;"));
            Assert.That(await MessagesAsync(mod, Build("1.5.3"), Of("v1.5.3"), bundle), Is.Empty, "The build's own package");
            Assert.That(await MessagesAsync(mod, Build("1.5.3", "v1.4.8;v1.5.3"), Of("v1.4.8")), Is.Empty, "No bundle");
        }

        /// <summary>Verifies that in multi-target matrix builds, UIX0030 is reported only once during the newest missing version build.</summary>
        [Test]
        public async Task InTheSdksBuildPerVersion_UIX0030_IsReportedOnce()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            var bundle = new TestBundle(Of("v1.3.4"), Of("v1.4.8"));
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.4.8;v1.5.3", oneOfSeveral: true), bundle), Is.Empty);
            Assert.That(await MessagesAsync(mod, Build("1.5.3", "v1.4.8;v1.5.3", oneOfSeveral: true), bundle), Has.Exactly(1).StartsWith("UIX0030: "));
        }

        /// <summary>
        /// Verifies that when no explicit version is defined, the version inferred from assembly references takes precedence.
        /// </summary>
        [Test]
        public async Task WithoutAVersion_TheInferredOneIsChecked()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            var bundle = new TestBundle(WithoutPanel("v1.3.4"), Of("v1.4.8"));
            Assert.That(await MessagesAsync(mod, new Dictionary<string, string>(), bundle), Is.Empty, "The bundle's newest");
            Assert.That(await MessagesAsync(mod, new Dictionary<string, string> { ["UIExtenderExInferredGameVersion"] = "v1.3.4" }, bundle), Is.EqualTo(new[]
            {
                "UIX0020: [v1.3.4] 'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie'; the patch is not applied",
            }));
            Assert.That(await MessagesAsync(mod, new Dictionary<string, string> { ["UIExtenderExInferredGameVersion"] = "v1.3.4", ["GameVersion"] = "1.4.8" }, bundle), Is.Empty);
        }

        /// <summary>Verifies that an explicitly referenced per-build package takes precedence over bundled versions for the matching version.</summary>
        [Test]
        public async Task TheReferencedPackage_WinsOverTheBundleForItsVersion()
        {
            var mod = Mod + Insert("Page", "HostMovie", Panel, "Child", "<Widget />");
            var bundle = new TestBundle(WithoutPanel("v1.4.8"));
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.4.8"), Game(), bundle), Is.Empty);
            Assert.That(await MessagesAsync(mod, Build("1.4.8", "v1.4.8"), bundle), Has.Count.EqualTo(1), "The bundle alone reports");
        }

        /// <summary>
        /// Verifies that in multi-target matrix builds, version-specific diagnostics are emitted only by the build corresponding to the newest affected version.
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
        /// Verifies that patch content verified across multiple versions reports common diagnostics once rather than redundantly per version.
        /// </summary>
        [Test]
        public async Task TheContentOfAPatch_IsReportedOnceForEveryVersion()
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

        /// <summary>Verifies that UIX0024 diagnostics are highlighted directly on the patch XPath expression.</summary>
        [Test]
        public async Task UIX0024_IsReportedOnTheXPath()
        {
            await VerifyAsync(Mod + Insert("Page", "HostMovie", "{|UIX0024:" + Panel + "|}", "Child", "<Widget />"),
                [new TestBundle(WithoutPanel("v1.3.4"), Of("v1.4.8"))], Build("1.4.8", "v1.3.4;v1.4.8"));
        }
    }
}