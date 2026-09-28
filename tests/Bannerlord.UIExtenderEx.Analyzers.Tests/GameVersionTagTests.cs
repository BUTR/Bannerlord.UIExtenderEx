using NUnit.Framework;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// The game version in front of every message. A module built for several game versions is compiled once per version,
/// and the reports of those builds have to say which version they are about.
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

    /// <summary>UIX0020 is reported by the prefab analyzer, UIX0017 by the content analyzer: both carry the version.</summary>
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

    /// <summary>An empty property, as a project that sets neither has in its editorconfig, leaves the messages alone.</summary>
    [Test]
    public async Task WithoutAVersion_TheMessagesAreUnchanged()
    {
        var tagged = await MessagesAsync(new() { ["GameVersion"] = "1.4.8" });
        var plain = await MessagesAsync(new() { ["GameVersion"] = " " });
        Assert.That(plain, Is.EqualTo(tagged.Select(m => m.Replace("[v1.4.8] ", "")).ToList()));
    }

    /// <summary>The tagged diagnostic is created anew; its fix still finds what the analyzer handed it.</summary>
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
