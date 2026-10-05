using NUnit.Framework;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Tests game version prefix tagging on diagnostic messages, ensuring that multi-version builds clearly distinguish
/// diagnostics by targeted game release.
/// </summary>
public class GameVersionTagTests
{
    private const string Mod = """
        public class HostVM : ViewModel { }

        [PrefabExtension("HostMovie", "descendant::Widget[@Id='Nope']")]
        public sealed class Page : PrefabExtensionInsertPatch
        {
            public override InsertType Type => InsertType.Child;
            [PrefabExtensionXmlNode] public string Content => "<Widget />";
        }
        """;

    private static TestGame Game() => TestGame.Base()
        .Movie("HostMovie", "HostVM")
        .ViewModel("HostVM", null)
        .Prefab("HostMovie", "<Prefab><Window><Widget Id=\"Root\" /></Window></Prefab>");

    private static Task<IReadOnlyList<string>> MessagesAsync(Dictionary<string, string> properties) =>
        PrefabVerifier.MessagesAsync(Mod, properties, Game());

    /// <summary>Verifies that both prefab and content analyzers include the configured game version tag in their diagnostics.</summary>
    [Test]
    public async Task EveryMessage_StartsWithTheGameVersion()
    {
        var messages = await MessagesAsync(new() { ["GameVersion"] = "1.4.8" });
        Assert.That(messages, Has.Some.StartsWith("UIX0020: [v1.4.8] 'descendant::Widget[@Id='Nope']' matches no node of 'HostMovie'"));
        Assert.That(messages, Has.Some.StartsWith("UIX0017: [v1.4.8] "));
        Assert.That(messages, Has.All.Contains(": [v1.4.8] "));
    }

    [Test]
    public async Task UIExtenderExGameVersion_WinsOverGameVersion_AndKeepsItsPrefix()
    {
        var messages = await MessagesAsync(new() { ["UIExtenderExGameVersion"] = "v1.2.12", ["GameVersion"] = "1.4.8" });
        Assert.That(messages, Is.Not.Empty);
        Assert.That(messages, Has.All.Contains(": [v1.2.12] "));
    }

    /// <summary>Verifies diagnostic tagging with inferred game versions when unconfigured, preserving release prefixes such as Early Access 'e'.</summary>
    [Test]
    public async Task TheInferredVersion_TagsWhenNoneIsNamed()
    {
        Assert.That(await MessagesAsync(new() { ["UIExtenderExInferredGameVersion"] = "v1.3.4" }), Has.All.Contains(": [v1.3.4] "));
        Assert.That(await MessagesAsync(new() { ["UIExtenderExInferredGameVersion"] = "v1.3.4", ["GameVersion"] = "1.4.8" }), Has.All.Contains(": [v1.4.8] "));
        Assert.That(await MessagesAsync(new() { ["UIExtenderExInferredGameVersion"] = "e1.9.0" }), Has.All.Contains(": [e1.9.0] "));
    }

    /// <summary>Verifies that empty version properties produce standard untagged diagnostic messages.</summary>
    [Test]
    public async Task WithoutAVersion_TheMessagesAreUnchanged()
    {
        var tagged = await MessagesAsync(new() { ["GameVersion"] = "1.4.8" });
        var plain = await MessagesAsync(new() { ["GameVersion"] = " " });
        Assert.That(plain, Is.EqualTo(tagged.Select(m => m.Replace("[v1.4.8] ", "")).ToList()));
    }

    /// <summary>Verifies that re-emitting a diagnostic with a game version prefix preserves associated code fix operations.</summary>
    [Test]
    public async Task ATaggedDiagnostic_KeepsItsCodeFix()
    {
        const string host = """
            public class HostVM : ViewModel
            {
                public override void RefreshValues() { }
            }

            """;
        await CodeFixVerifier.VerifyAsync("UIX0003", host + """
            [ViewModelMixin("RefreshValuez")]
            public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
            {
                public HostVMMixin(HostVM vm) : base(vm) { }
            }
            """, host + """
            [ViewModelMixin(nameof(HostVM.RefreshValues))]
            public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
            {
                public HostVMMixin(HostVM vm) : base(vm) { }
            }
            """, [], "Use 'RefreshValues'", gameVersion: "1.4.8");
    }
}