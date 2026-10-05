using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ResourceManager;
using Bannerlord.UIExtenderEx.Runtimes;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies runtime prefab registration and resolution through <see cref="WidgetFactoryManager"/> and <see cref="WidgetFactory"/>.
/// </summary>
public class WidgetFactoryManagerTests
{
    private const string Xml = "<Prefab><Window><Widget /></Window></Prefab>";

    private readonly List<PrefabWorkspace> _workspaces = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var workspace in _workspaces)
            workspace.Dispose();
        _workspaces.Clear();
    }

    private PrefabWorkspace Workspace(params (string Name, string Xml)[] prefabs)
    {
        var workspace = new PrefabWorkspace(prefabs);
        _workspaces.Add(workspace);
        return workspace;
    }

    // Parses a fresh XML instance per invocation to track distinct factory resolution and parsing cycles.
    private static Func<WidgetPrefab?> Parses(WidgetFactory factory, string name, List<WidgetPrefab> parsed) => () =>
    {
        var document = new XmlDocument();
        document.LoadXml(Xml);
        var prefab = WidgetPrefabPatch.LoadFromDocument(factory.PrefabExtensionContext, factory.WidgetAttributeContext, $"{name}.xml", document);
        parsed.Add(prefab!);
        return prefab;
    };

    [Test]
    public void RegisteringANameAgain_ReplacesTheFirstRegistration()
    {
        const string name = "WfmReRegistered";
        var factory = Workspace().WidgetFactory;
        var first = new List<WidgetPrefab>();
        var second = new List<WidgetPrefab>();

        WidgetFactoryManager.Register(name, Parses(factory, name, first));
        Assert.That(factory.GetCustomType(name), Is.SameAs(first[0]));

        Assert.DoesNotThrow(() => WidgetFactoryManager.Register(name, Parses(factory, name, second)));

        Assert.That(factory.GetCustomType(name), Is.SameAs(second[0]), "the copy of the first registration in use was forgotten");
    }

    [Test]
    public void RegisteringAGameName_TakesOverTheGameCopyInUse()
    {
        const string name = "WfmTakenOver";
        var workspace = Workspace((name, Xml));
        var factory = workspace.WidgetFactory;
        var gameCopy = factory.GetCustomType(name);
        Assert.That(workspace.LivePrefabNames, Does.Contain(name), "the game parsed its own file");

        var registered = new List<WidgetPrefab>();
        WidgetFactoryManager.Register(name, Parses(factory, name, registered));

        Assert.That(workspace.LivePrefabNames, Does.Not.Contain(name), "the game's copy was forgotten");
        var served = factory.GetCustomType(name);
        Assert.That(served, Is.SameAs(registered[0]));
        Assert.That(served, Is.Not.SameAs(gameCopy));
    }

    [PrefabExtension("WfmPatchedRegistration", "descendant::Widget")]
    private sealed class RegistrationPatch : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => [new("Patched", "true")];
    }

    [Test]
    public void APrefabBuiltFromADocument_GetsTheEnabledPatches()
    {
        const string name = "WfmPatchedRegistration";
        var factory = Workspace().WidgetFactory;
        var extender = UIExtender.Create(nameof(APrefabBuiltFromADocument_GetsTheEnabledPatches));
        extender.Register([typeof(RegistrationPatch)]);
        extender.Enable();
        try
        {
            var received = new List<XmlDocument>();
            void OnParsed(WidgetPrefab _, string prefabName, XmlDocument document)
            {
                if (prefabName == name)
                    received.Add(document);
            }

            var document = new XmlDocument();
            document.LoadXml(Xml);
            PrefabSource.Parsed += OnParsed;
            try
            {
                // Load twice from the same document instance to verify reloads without mutating source XML.
                Assert.That(WidgetPrefabPatch.LoadFromDocument(factory.PrefabExtensionContext, factory.WidgetAttributeContext, $"{name}.xml", document), Is.Not.Null);
                Assert.That(WidgetPrefabPatch.LoadFromDocument(factory.PrefabExtensionContext, factory.WidgetAttributeContext, $"{name}.xml", document), Is.Not.Null);
            }
            finally
            {
                PrefabSource.Parsed -= OnParsed;
            }

            Assert.That(received, Has.Count.EqualTo(2));
            Assert.That(received.Select(x => x.SelectSingleNode("descendant::Widget/@Patched")?.Value), Is.All.EqualTo("true"));
            Assert.That(document.OuterXml, Is.EqualTo(Xml), "the caller's document is left as it was");
        }
        finally
        {
            extender.Deregister();
        }
    }

    [Test]
    public void EachFactory_KeepsItsOwnCopies()
    {
        const string name = "WfmPerFactory";
        var oldFactory = Workspace().WidgetFactory;
        var newFactory = Workspace().WidgetFactory;
        var parsed = new List<WidgetPrefab>();
        WidgetFactoryManager.Register(name, Parses(oldFactory, name, parsed));

        var oldCopy = oldFactory.GetCustomType(name);
        oldFactory.GetCustomType(name);
        var newCopy = newFactory.GetCustomType(name);

        Assert.That(parsed, Has.Count.EqualTo(2), "one parse per factory, reused within each");
        Assert.That(newCopy, Is.Not.SameAs(oldCopy), "a new factory does not get the copy parsed for the old one");

        // Track references independently per factory; releasing from one factory does not invalidate the other.
        newFactory.OnUnload(name);
        oldFactory.OnUnload(name);
        Assert.That(oldFactory.GetCustomType(name), Is.SameAs(oldCopy), "one of the old factory's users is left");

        oldFactory.OnUnload(name);
        oldFactory.OnUnload(name);
        Assert.That(oldFactory.GetCustomType(name), Is.Not.SameAs(oldCopy), "released by every user, parsed again");
    }
}