using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// The generator's view of the widget factory. A mod's widget classes and prefabs are registered after the factory
/// scanned for them, so the factory's own <c>IsBuiltinType</c> and <c>IsCustomType</c> do not know them. The XML loader
/// reaches them through UIExtenderEx's patches; the generator has to reach the same set or it generates a different UI.
/// </summary>
public class WidgetFactoryLookupTests
{
    private const string RegisteredPrefabName = "LookupRegisteredPrefab";

    private static readonly AccessTools.FieldRef<WidgetFactory, Dictionary<string, Type>>? BuiltinTypes =
        AccessTools2.FieldRefAccess<WidgetFactory, Dictionary<string, Type>>("_builtinTypes");

    private PrefabWorkspace _workspace = null!;
    private UIExtender _extender = null!;

    [SetUp]
    public void SetUp()
    {
        // Loading a registered prefab runs through UIExtenderEx's own patches, so they have to be in place.
        // Register comes first even with nothing to register: Enable without it fails the module.
        _extender = UIExtender.Create("TestModule.CodeGenerator.Lookup");
        _extender.Register([]);
        _extender.Enable();
        _workspace = new PrefabWorkspace(("LookupOnDisk", PrefabWorkspace.PlainPrefab));

        WidgetFactoryManager.Register(typeof(LookupWidget));
        if (!WidgetFactoryManager.IsRegisteredCustomType(RegisteredPrefabName))
        {
            WidgetFactoryManager.Register(RegisteredPrefabName, () =>
            {
                var document = new XmlDocument();
                document.LoadXml(PrefabWorkspace.PlainPrefab);
                return WidgetPrefabPatch.LoadFromDocument(_workspace.WidgetFactory.PrefabExtensionContext,
                    _workspace.WidgetFactory.WidgetAttributeContext, RegisteredPrefabName + ".xml", document);
            });
        }

        // The mod assembly is the test assembly, so the factory found this class by itself. In the game it could not have.
        BuiltinTypes!(_workspace.WidgetFactory).Remove(nameof(LookupWidget));
    }

    [TearDown]
    public void TearDown()
    {
        _extender.Deregister();
        _workspace.Dispose();
    }

    [Test]
    public void ARegisteredWidgetClass_IsABuiltinTypeToTheGenerator()
    {
        Assert.That(_workspace.WidgetFactory.IsBuiltinType(nameof(LookupWidget)), Is.False, "test premise: the factory does not know it");

        Assert.That(_workspace.WidgetFactory.IsBuiltinTypeIncludingRegistered(nameof(LookupWidget)), Is.True);
        Assert.That(_workspace.WidgetFactory.GetBuiltinTypeIncludingRegistered(nameof(LookupWidget)), Is.EqualTo(typeof(LookupWidget)));
    }

    [Test]
    public void AWidgetClassTheFactoryFoundItself_IsStillReached()
    {
        Assert.That(_workspace.WidgetFactory.IsBuiltinTypeIncludingRegistered("TextWidget"), Is.True);
        Assert.That(_workspace.WidgetFactory.GetBuiltinTypeIncludingRegistered("TextWidget").Name, Is.EqualTo("TextWidget"));
    }

    [Test]
    public void ARegisteredPrefab_IsACustomTypeToTheGenerator()
    {
        Assert.That(_workspace.WidgetFactory.GetCustomTypePath(RegisteredPrefabName), Is.Empty, "test premise: it is not on disk");

        Assert.That(_workspace.WidgetFactory.IsCustomTypeIncludingRegistered(RegisteredPrefabName), Is.True);
        Assert.That(_workspace.WidgetFactory.GetCustomTypeIncludingRegistered(RegisteredPrefabName), Is.Not.Null);
    }

    [Test]
    public void APrefabOnDisk_IsStillReached()
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory);

        Assert.That(_workspace.WidgetFactory.IsCustomTypeIncludingRegistered("LookupOnDisk"), Is.True);
        Assert.That(_workspace.WidgetFactory.TryGetCustomTypeIncludingRegistered("LookupOnDisk", out var prefab), Is.True);
        Assert.That(prefab, Is.Not.Null);
    }

    [Test]
    public void AnUnknownName_IsReportedRatherThanDereferenced()
    {
        // The game's generator dereferences a null here and says nothing. The name is the one useful detail: the XML
        // asking for it came from a mod's patch.
        Assert.That(_workspace.WidgetFactory.IsCustomTypeIncludingRegistered("NoSuchPrefab"), Is.False);
        Assert.That(_workspace.WidgetFactory.TryGetCustomTypeIncludingRegistered("NoSuchPrefab", out _), Is.False);

        var e = Assert.Throws<InvalidOperationException>(() => _workspace.WidgetFactory.GetCustomTypeIncludingRegistered("NoSuchPrefab"));

        Assert.That(e!.Message, Does.Contain("'NoSuchPrefab'"));
        Assert.That(e.Message, Does.Contain(nameof(WidgetFactoryManager)));
    }

    /// <summary>
    /// A UIExtenderEx patch fragment is an XML file whose root is the widget to insert, not a <c>&lt;Prefab&gt;</c>.
    /// Mods keep them in <c>GUI/Prefabs</c>, which makes the game's WidgetFactory register the file name as a prefab, and
    /// parsing one throws a <see cref="NullReferenceException"/> out of the game's own loader with nothing in it. The
    /// loader never notices, because a fragment's name appears in no XML; the generator walks further and can reach one.
    /// </summary>
    [TestCase("<Widget><Children><Widget /></Children></Widget>", TestName = "AFragmentPrefab_IsReportedByName(Widget root)")]
    [TestCase("<DummyRoot><Widget /></DummyRoot>", TestName = "AFragmentPrefab_IsReportedByName(DummyRoot root)")]
    public void AFragmentPrefab_IsReportedByName(string fragment)
    {
        using var workspace = new PrefabWorkspace(("LookupFragment", fragment));
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(workspace.WidgetFactory);

        Assert.That(workspace.WidgetFactory.IsCustomTypeIncludingRegistered("LookupFragment"), Is.True, "the factory registered the file name like any other prefab");

        // A caller that can do without it carries on, instead of taking the whole feature down with an exception
        Assert.That(workspace.WidgetFactory.TryGetCustomTypeIncludingRegistered("LookupFragment", out _), Is.False);

        var e = Assert.Throws<InvalidOperationException>(() => workspace.WidgetFactory.GetCustomTypeIncludingRegistered("LookupFragment"));

        Assert.That(e!.Message, Does.Contain("'LookupFragment'"));
        Assert.That(e.Message, Does.Contain("LookupFragment.xml"), "and where the file is, so it can be found");
        Assert.That(e.Message, Does.Contain("GUI/Prefabs"), "with the reason mods hit this at all");
    }
}
