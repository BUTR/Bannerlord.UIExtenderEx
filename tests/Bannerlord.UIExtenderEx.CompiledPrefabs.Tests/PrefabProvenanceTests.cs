using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;

using NUnit.Framework;

using System;
using System.Linq;
using System.Reflection;
using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

public class PrefabProvenanceTests
{
    [SetUp]
    public void SetUp()
    {
        _ = UIExtender.Create("TestModule.Provenance");
        PrefabFingerprint.ClearCache();
    }

    private static WidgetPrefab Parse(WidgetFactory factory, string id, string name = "")
    {
        var xml = new XmlDocument();
        xml.LoadXml($"<Prefab><Window><Widget Id=\"{id}\" /></Window></Prefab>");
        return WidgetPrefabPatch.LoadFromDocument(factory.PrefabExtensionContext, factory.WidgetAttributeContext, name, xml)!;
    }

    [Test]
    public void UnnamedReplacement_HasItsOwnHashInsteadOfThePriorFilesHash()
    {
        var movie = "Replacement" + Guid.NewGuid().ToString("N");
        using var workspace = new PrefabWorkspace((movie, PrefabWorkspace.PlainPrefab));
        var factory = workspace.WidgetFactory;
        var before = TestFingerprint.Compute(factory, movie, typeof(CodegenTestVM));
        WidgetFactoryManager.Register(movie, () => Parse(factory, "replacement"));

        using var snapshot = PrefabCompilationSnapshot.Begin(factory, movie, typeof(CodegenTestVM));

        Assert.That(snapshot, Is.Not.Null);
        Assert.That(snapshot!.CollectInputs().Fingerprint, Is.Not.EqualTo(before));
        Assert.That(PrefabXmlRegistry.TryGetHash(movie, out var oldHash), Is.True);
        Assert.That(snapshot.Section.Hashes[movie], Is.Not.EqualTo(oldHash), "the prior file's name entry is not provenance");
    }

    [Test]
    public void UnrecordedReplacement_CannotBorrowThePriorFilesHash()
    {
        var movie = "Unrecorded" + Guid.NewGuid().ToString("N");
        using var workspace = new PrefabWorkspace((movie, PrefabWorkspace.PlainPrefab));
        var factory = workspace.WidgetFactory;
        Assert.That(TestFingerprint.Compute(factory, movie, typeof(CodegenTestVM)), Is.Not.Null);
        var parsed = Parse(factory, "replacement");
        var unrecorded = (WidgetPrefab) typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(parsed, null)!;
        WidgetFactoryManager.Register(movie, () => unrecorded);

        using var snapshot = PrefabCompilationSnapshot.Begin(factory, movie, typeof(CodegenTestVM), out var failure);

        Assert.That(snapshot, Is.Null);
        Assert.That(failure, Does.Contain(movie));
    }

    [Test]
    public void RuntimeFactory_IsRevalidatedAfterItsParsedCopyIsReleased()
    {
        var movie = "Dynamic" + Guid.NewGuid().ToString("N");
        using var workspace = new PrefabWorkspace();
        var factory = workspace.WidgetFactory;
        var id = "old";
        WidgetFactoryManager.Register(movie, () => Parse(factory, id, movie + ".xml"));
        var before = TestFingerprint.Compute(factory, movie, typeof(CodegenTestVM));
        id = "new";

        var after = TestFingerprint.Compute(factory, movie, typeof(CodegenTestVM));
        using var snapshot = PrefabCompilationSnapshot.Begin(factory, movie, typeof(CodegenTestVM));

        Assert.That(after, Is.Not.EqualTo(before));
        Assert.That(after, Is.EqualTo(snapshot!.CollectInputs().Fingerprint));
        Assert.That(PrefabFingerprint.GetPrefabSection(factory, movie)!.IsCurrent(factory), Is.False, "a tree with a runtime-registered prefab is re-read on every open");
    }

    public class ProvenanceCollisionWidget : Widget
    {
        public ProvenanceCollisionWidget(UIContext context) : base(context) { }
    }

    [Test]
    public void SwitchingFactories_DropsSectionsForMoviesNotReopened()
    {
        using var oldWorkspace = new PrefabWorkspace(("OldFactoryMovie", PrefabWorkspace.PlainPrefab));
        var old = PrefabFingerprint.GetPrefabSection(oldWorkspace.WidgetFactory, "OldFactoryMovie");
        using var newWorkspace = new PrefabWorkspace(("NewFactoryMovie", PrefabWorkspace.PlainPrefab));
        Assert.That(PrefabFingerprint.GetPrefabSection(newWorkspace.WidgetFactory, "NewFactoryMovie"), Is.Not.Null);

        Assert.That(PrefabFingerprint.GetPrefabSection(oldWorkspace.WidgetFactory, "OldFactoryMovie"), Is.Not.SameAs(old));
    }

    [Test]
    public void PrefabAndWidgetWithTheSameName_UseThePrefabForChildrenAndRoots()
    {
        var name = nameof(ProvenanceCollisionWidget);
        WidgetFactoryManager.Register(typeof(ProvenanceCollisionWidget));
        using var workspace = new PrefabWorkspace(
            (name, "<Prefab><Window><TextWidget Text=\"from prefab\" /></Window></Prefab>"),
            ("CollisionChild", $"<Prefab><Window><Widget><Children><{name} /></Children></Widget></Window></Prefab>"),
            ("CollisionRoot", $"<Prefab><Window><{name} /></Window></Prefab>"));

        Assert.That(workspace.WidgetFactory.IsBuiltinTypeIncludingRegistered(name), Is.False);
        Assert.That(PrefabFingerprint.DescribeWidgetType(workspace.WidgetFactory, name), Is.EqualTo("(prefab)"));
        foreach (var movie in new[] { "CollisionChild", "CollisionRoot" })
        {
            var sources = string.Join("\n", workspace.Generate(movie, typeof(CodegenTestVM)).Select(x => x.Content));
            Assert.That(sources, Does.Contain("from prefab"));
            Assert.That(sources, Does.Not.Contain(typeof(ProvenanceCollisionWidget).FullName!));
            Assert.That(TestFingerprint.CollectClosure(workspace.WidgetFactory, movie), Does.Contain(name));
        }
    }
}
