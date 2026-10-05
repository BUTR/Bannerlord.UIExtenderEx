using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.PrefabVerifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Tests UIX0025 against real, on-disk extracted GUI packages for manual validation and integration verification.
/// Configured via <c>UIEXTENDEREX_GUI_PACKAGES</c> (semicolon-separated package directories) or
/// <c>UIEXTENDEREX_GUI_BUNDLE</c> (path to an extracted <c>GUI.v3.All</c> directory).
/// </summary>
[Explicit("Needs extracted GUI packages; set UIEXTENDEREX_GUI_PACKAGES")]
public class RealGuiPackageTests
{
    private sealed class ExtractedPackage(string folder) : ITestPackage
    {
        private readonly string _id = XDocument.Load(Directory.GetFiles(folder, "*.nuspec").Single()).Descendants().First(x => x.Name.LocalName == "id").Value;

        public IEnumerable<(string Path, string Text, string Package)> Files() =>
            Directory.EnumerateFiles(Path.Combine(folder, "gui"), "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".json", StringComparison.Ordinal) || path.EndsWith(".jsonl", StringComparison.Ordinal))
                .Select(path => (path.Replace('\\', '/'), File.ReadAllText(path), _id));
    }

    private static ITestPackage[] Packages(string variable = "UIEXTENDEREX_GUI_PACKAGES")
    {
        var folders = Environment.GetEnvironmentVariable(variable)?.Split([';'], StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (folders.Length == 0)
            Assert.Ignore($"Set {variable} to the extracted package folders.");
        return folders.Select(x => (ITestPackage) new ExtractedPackage(x)).ToArray();
    }

    private const string ViewModels = """
        [assembly: PrefabLink("ModPanel", typeof(PanelVM))]

        public class PanelVM : ViewModel
        {
            public TaleWorlds.GauntletUI.VerticalAlignment Align { get; set; }
            public object AlignObject { get; set; } = null!;
            public int Width { get; set; }
            public float FloatWidth { get; set; }
            public bool IsOn { get; set; }
            public string Label { get; set; } = "";
            public int Count { get; set; }
        }

        """;

    private static (string, string) Prefab(string body) => ("GUI/Prefabs/ModPanel.xml", $$"""
        <Prefab>
          <Window>
            <Widget>
              <Children>
                {{body}}
              </Children>
            </Widget>
          </Window>
        </Prefab>
        """);

    [Test]
    public async Task WhatTheGameAnnounces_IsCheckedAgainstTheViewModel()
    {
        await VerifyAsync(ViewModels, Packages(), Prefab("""
            <Widget {|UIX0025:VerticalAlignment|}="@Align" {|UIX0025:SuggestedWidth|}="@Width" />
            <ButtonWidget {|UIX0025:VerticalAlignment|}="@Align" />
            <Widget VerticalAlignment="@AlignObject" SuggestedWidth="@FloatWidth" IsVisible="@IsOn" />
            <TextWidget Text="@Label" />
            <TextWidget IntText="@Count" />
            """));
    }

    /// <summary>
    /// Verifies bundle metadata across supported versions: v1.0 announced alignments as enums, so enum properties
    /// fail starting in v1.1.0 (triggering UIX0024), whereas <c>SuggestedWidth</c> consistently announces floats (triggering UIX0025).
    /// </summary>
    [Test]
    public async Task FromTheBundle_EachSupportedVersionIsChecked()
    {
        var bundle = Packages("UIEXTENDEREX_GUI_BUNDLE");
        var properties = new Dictionary<string, string> { ["UIExtenderExGameVersions"] = "v1.0.3,v1.2.12,v1.4.8" };
        await VerifyAsync(ViewModels, bundle, properties, Prefab("""
            <Widget {|UIX0024:VerticalAlignment|}="@Align" {|UIX0025:SuggestedWidth|}="@Width" />
            <Widget VerticalAlignment="@AlignObject" SuggestedWidth="@FloatWidth" />
            """));

        properties["UIExtenderExGameVersions"] = "v1.0.0,v1.0.3";
        await VerifyAsync(ViewModels, bundle, properties, Prefab("""<Widget VerticalAlignment="@Align" />"""));
    }
}