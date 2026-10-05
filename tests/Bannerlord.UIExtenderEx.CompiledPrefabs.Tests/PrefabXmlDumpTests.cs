using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Prefabs;

using NUnit.Framework;

using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Defines a sample XML extension patch applied to a dumped widget prefab.
/// </summary>
[PrefabExtension(PrefabXmlDumpTests.Prefab, "descendant::Widget[@Id='dumped']")]
internal sealed class DumpSetAttributePatch : PrefabExtensionSetAttributePatch
{
    public override string Id => "dumped";
    public override string Attribute => "PatchedAttribute";
    public override string Value => "yes";
}

/// <summary>
/// Verifies XML prefab serialization and dump generation performed by <see cref="PrefabXmlDump"/>.
/// <para>
/// Captures patched XML documents during factory loading into module folder structures consumable by external corpus tools.
/// </para>
/// </summary>
[NonParallelizable]
public class PrefabXmlDumpTests
{
    internal const string Prefab = "DumpedPrefab";

    private PrefabWorkspace? _workspace;
    private UIExtender? _extender;
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "UIExtenderEx-dump-" + Guid.NewGuid().ToString("N"));
        _workspace = new PrefabWorkspace((Prefab, "<Prefab><Window><Widget Id=\"dumped\" /></Window></Prefab>"));
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.Dump");
        _extender.Register([typeof(DumpSetAttributePatch)]);
        _extender.Enable();
    }

    [TearDown]
    public void TearDown()
    {
        _extender?.Deregister();
        _workspace?.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    /// <summary>
    /// Verifies that parsed prefabs write to disk reflecting active extension patches and module metadata.
    /// </summary>
    [Test]
    public void AParsedPrefab_IsWrittenWithItsPatches()
    {
        using (PrefabXmlDump.Install(_directory, ["Native", "SomeMod"]))
            _workspace!.WidgetFactory.GetCustomType(Prefab);

        var file = Path.Combine(_directory, "GUI", "Prefabs", Prefab + ".xml");
        Assert.That(File.Exists(file), Is.True);
        var document = new XmlDocument();
        document.Load(file);
        Assert.That(document.SelectSingleNode("descendant::Widget[@Id='dumped']/@PatchedAttribute")?.Value, Is.EqualTo("yes"), "as the game parsed it, patched");
        Assert.That(File.ReadAllLines(Path.Combine(_directory, PrefabXmlDump.ModulesFileName)), Is.EqualTo(new[] { "Native", "SomeMod" }));
    }

    /// <summary>
    /// Verifies that <see cref="PrefabXmlDump.LoadAll(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory)"/> dumps all known prefabs with patches applied without opening screens.
    /// </summary>
    [Test]
    public void LoadAll_DumpsEveryPrefabTheFactoryKnows()
    {
        using (PrefabXmlDump.Install(_directory, ["Native"]))
        {
            var (loaded, failed) = PrefabXmlDump.LoadAll(_workspace!.WidgetFactory);
            Assert.That((loaded, failed), Is.EqualTo((_workspace.WidgetFactory.GetPrefabNames().Count(), 0)));
        }

        var file = Path.Combine(_directory, "GUI", "Prefabs", Prefab + ".xml");
        Assert.That(File.Exists(file), Is.True);
        var document = new XmlDocument();
        document.Load(file);
        Assert.That(document.SelectSingleNode("descendant::Widget[@Id='dumped']/@PatchedAttribute")?.Value, Is.EqualTo("yes"));
    }

    /// <summary>
    /// Verifies that <see cref="PrefabXmlDump.LoadAll(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory)"/> no-ops when no dump session is actively installed.
    /// </summary>
    [Test]
    public void LoadAll_WithoutADump_LoadsNothing()
    {
        Assert.That(PrefabXmlDump.LoadAll(_workspace!.WidgetFactory), Is.EqualTo((0, 0)));
        Assert.That(Directory.Exists(_directory), Is.False);
    }

    /// <summary>
    /// Verifies that disposing a dump session unhooks factory listeners and stops further disk writes.
    /// </summary>
    [Test]
    public void AStoppedDump_WritesNothingMore()
    {
        PrefabXmlDump.Install(_directory, []).Dispose();

        _workspace!.WidgetFactory.GetCustomType(Prefab);

        Assert.That(File.Exists(Path.Combine(_directory, "GUI", "Prefabs", Prefab + ".xml")), Is.False);
    }
}
