using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Settings;

using NUnit.Framework;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests.Settings;

public class SettingsSubModuleXmlTests
{
    private const string ModuleXml = """
<?xml version="1.0" encoding="UTF-8"?>
<Module>
  <Id value="My.Module" />
  <Name value="My Module" />
  <SubModules />
  <!-- keep me -->
  <Settings>
    <Bool Id="Ungrouped" Default="true" />
    <Group Name="General">
      <Bool Id="Enabled" DisplayName="Enabled" Default="false" Value="true" />
      <Integer Id="Retries" MinValue="0" MaxValue="10" Default="3" />
      <FloatingInteger Id="Scale" MinValue="0" MaxValue="2" Default="1.5" Value="0.5" />
      <Text Id="Prefix" Default="[x]" />
      <Dropdown Id="Quality" Default="1"><Option Value="Low" /><Option Value="High" /></Dropdown>
      <Group Name="Nested">
        <Bool Id="Verbose" Default="true" />
      </Group>
      <Unknown Id="Ignored" Default="true" />
      <Bool Default="true" />
    </Group>
  </Settings>
  <Settings Id="Second"><Bool Id="Other" Default="true" /></Settings>
</Module>
""";

    private static XmlDocument Load(string xml)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        document.LoadXml(xml);
        return document;
    }

    [Test]
    public void Parse_ReadsDefaultsAndValues()
    {
        var declaration = SubModuleSettingsXml.Parse(Load(ModuleXml));

        Assert.IsNotNull(declaration);
        Assert.AreEqual("My.Module", declaration.Id, "Id defaults to the module id");
        Assert.AreEqual(7, declaration.Defaults.Count, "unknown elements and elements without Id are skipped");
        Assert.AreEqual("true", declaration.Defaults["Ungrouped"]);
        Assert.AreEqual("false", declaration.Defaults["Enabled"]);
        Assert.AreEqual("true", declaration.Values["Enabled"], "a saved Value wins over Default");
        Assert.AreEqual("3", declaration.Values["Retries"], "without a saved Value the Default applies");
        Assert.AreEqual("0.5", declaration.Values["Scale"]);
        Assert.AreEqual("[x]", declaration.Values["Prefix"]);
        Assert.AreEqual("1", declaration.Values["Quality"], "a dropdown's value is the option index");
        Assert.AreEqual("true", declaration.Values["Verbose"], "nested groups are searched");
        Assert.IsFalse(declaration.Values.ContainsKey("Other"), "only the first block belongs to this provider");
    }

    [Test]
    public void Parse_ReturnsNull_WithoutDeclaration()
    {
        Assert.IsNull(SubModuleSettingsXml.Parse(Load("<Module><Id value=\"Test\" /></Module>")));
        Assert.IsNull(SubModuleSettingsXml.Parse(Load("<Module><Settings><Bool Id=\"A\" Default=\"true\" /></Settings></Module>")), "no Id anywhere");
        Assert.IsNull(SubModuleSettingsXml.Parse(Load("<Settings Id=\"Root\" />")), "only SubModule.xml is read here");
    }

    [Test]
    public void SetValues_WritesValueAttributes_AndKeepsTheRest()
    {
        var document = Load(ModuleXml);

        Assert.IsTrue(SubModuleSettingsXml.SetValues(document, new Dictionary<string, string> { ["Enabled"] = "false", ["Verbose"] = "false", ["Missing"] = "true" }));

        var declaration = SubModuleSettingsXml.Parse(document);
        Assert.AreEqual("false", declaration.Values["Enabled"]);
        Assert.AreEqual("false", declaration.Values["Verbose"]);
        Assert.AreEqual("false", declaration.Defaults["Enabled"], "Default is untouched");
        Assert.AreEqual("0.5", declaration.Values["Scale"], "other values are untouched");

        var xml = document.OuterXml;
        StringAssert.Contains("<!-- keep me -->", xml);
        StringAssert.Contains("""<Bool Id="Enabled" DisplayName="Enabled" Default="false" Value="false" />""", xml);
        StringAssert.Contains("""<Bool Id="Verbose" Default="true" Value="false" />""", xml);
        StringAssert.Contains("""<Settings Id="Second"><Bool Id="Other" Default="true" /></Settings>""", xml, "the second block is untouched");

        Assert.IsFalse(SubModuleSettingsXml.SetValues(Load("<Module><Id value=\"Test\" /></Module>"), new Dictionary<string, string> { ["A"] = "true" }));
    }

    [Test]
    public void SetValues_ReportsNoChange_ForUndeclaredPropertiesAndHeldValues()
    {
        var document = Load(ModuleXml);

        Assert.IsFalse(SubModuleSettingsXml.SetValues(document, new Dictionary<string, string> { ["Missing"] = "true" }), "an undeclared property is session-only");
        Assert.IsFalse(SubModuleSettingsXml.SetValues(document, new Dictionary<string, string> { ["Enabled"] = "true" }), "the saved Value already is that");
        Assert.IsTrue(SubModuleSettingsXml.SetValues(document, new Dictionary<string, string> { ["Retries"] = "3" }), "no Value saved yet, even if it equals the Default");
        Assert.IsFalse(SubModuleSettingsXml.SetValues(document, new Dictionary<string, string> { ["Retries"] = "3" }));
    }

    [Test]
    public void SaveDocument_RoundTripsThroughAFile()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{nameof(SaveDocument_RoundTripsThroughAFile)}.xml");
        File.WriteAllText(path, ModuleXml);
        try
        {
            var document = SubModuleSettingsXml.LoadDocument(path);
            SubModuleSettingsXml.SetValues(document, new Dictionary<string, string> { ["Retries"] = "7" });
            SubModuleSettingsXml.SaveDocument(document, path);

            var reloaded = SubModuleSettingsXml.Parse(SubModuleSettingsXml.LoadDocument(path));
            Assert.AreEqual("7", reloaded.Values["Retries"]);
            Assert.AreEqual("3", reloaded.Defaults["Retries"]);
            StringAssert.Contains("<!-- keep me -->", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void ShippedDeclaration_CoversEverySetting()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "UIExtenderEx", "SubModule.xml");
        var declaration = SubModuleSettingsXml.Parse(SubModuleSettingsXml.LoadDocument(path));

        Assert.IsNotNull(declaration, $"no <Settings> block in {path}");
        Assert.AreEqual("$moduleid$", declaration.Id, "no Id: the module id applies (a build placeholder in the source manifest)");

        // The core's through its store, and the compiled runtime's by key; outside the game each answers its code default
        var store = new SettingsSubModuleXml();
        List<(string Key, bool CodeDefault)> settings =
        [
            .. typeof(SettingsSubModuleXml).GetProperties().Select(x => (x.Name, (bool) x.GetValue(store)!)),
            (CompiledPrefabSettings.CompiledPrefabsKey, CompiledPrefabSettings.CompiledPrefabs),
            (CompiledPrefabSettings.DumpGeneratedCodeKey, CompiledPrefabSettings.DumpGeneratedCode),
            (CompiledPrefabSettings.RecordTimingsKey, CompiledPrefabSettings.RecordTimings),
        ];
        foreach (var (key, codeDefault) in settings)
        {
            Assert.IsTrue(declaration.Defaults.TryGetValue(key, out var raw), $"{key} is not declared in SubModule.xml");
            Assert.IsTrue(bool.TryParse(raw, out var declared), $"{key} has no boolean Default");
            Assert.AreEqual(declared, codeDefault, $"{key}: the code default differs from the declared one");
            Assert.AreEqual(raw, declaration.Values[key], $"{key}: the shipped manifest must not carry a saved Value");
        }

        CollectionAssert.AreEquivalent(settings.Select(x => x.Key), declaration.Defaults.Keys, "SubModule.xml declares a setting the code does not read");
    }
}