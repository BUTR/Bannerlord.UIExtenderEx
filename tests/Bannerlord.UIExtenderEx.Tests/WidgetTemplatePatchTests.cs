using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System.Collections;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies <see cref="WidgetTemplatePatch"/> behavior ensuring custom widget templates retain exactly one copy
/// of child templates across repeated instantiations and release cached templates upon unloading.
/// </summary>
public class WidgetTemplatePatchTests
{
    private const string Item = "<Prefab><Window><Widget><Children><Widget /><Widget /></Children></Widget></Window></Prefab>";
    private const string Host = "<Prefab><Window><Widget><Children><WtpItem /></Children></Widget></Window></Prefab>";

    private static readonly AccessTools.FieldRef<WidgetTemplate, List<WidgetTemplate>> Children =
        AccessTools2.FieldRefAccess<WidgetTemplate, List<WidgetTemplate>>("_children")!;
    private static readonly AccessTools.FieldRef<WidgetTemplate, List<WidgetTemplate>> CustomTypeChildren =
        AccessTools2.FieldRefAccess<WidgetTemplate, List<WidgetTemplate>>("_customTypeChildren")!;
    private static readonly AccessTools.FieldRef<WidgetFactory, IDictionary> LiveCustomTypes =
        AccessTools2.FieldRefAccess<WidgetFactory, IDictionary>("_liveCustomTypes")!;

    private PrefabWorkspace _workspace = null!;
    private WidgetFactory _factory = null!;
    private WidgetCreationData _creation = null!;

    [SetUp]
    public void SetUp()
    {
        _workspace = new PrefabWorkspace(("WtpItem", Item), ("WtpHost", Host));
        // Omit databinding extension to verify isolated template instantiation without data source bindings.
        _factory = new WidgetFactory(_workspace.ResourceDepot, "Prefabs");
        _factory.Initialize();
        _creation = new WidgetCreationData(new TestUIContext(_workspace.ResourceDepot, TestContext.CurrentContext.Test.Name).Context, _factory);
    }

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    [Test]
    public void BuildingACustomTypeAgain_DoesNotAddItsChildTemplatesAgain()
    {
        var host = _factory.GetCustomType("WtpHost");
        var itemUsage = Children(host.RootTemplate)[0];

        for (var i = 0; i < 3; i++)
        {
            var widget = host.Instantiate(_creation).Widget;
            Assert.That(widget.GetChild(0).ChildCount, Is.EqualTo(2), "every build still has the item's children");
        }

        Assert.That(CustomTypeChildren(itemUsage), Has.Count.EqualTo(2), "the item's two child templates, once each");
        Assert.That(CustomTypeChildren(itemUsage), Is.Unique);
    }

    [Test]
    public void ACustomTypeBuiltOnce_IsReleasedAsTheGameReleasesIt()
    {
        var host = _factory.GetCustomType("WtpHost");
        host.Instantiate(_creation);
        Assert.That(LivePrefabNames(), Does.Contain("WtpItem"), "the build parsed the item");

        host.OnRelease();

        Assert.That(LivePrefabNames(), Does.Not.Contain("WtpItem"), "its one user released it");
    }

    private List<string> LivePrefabNames() => [.. LiveCustomTypes(_factory).Keys.Cast<object>().Select(x => x.ToString()!)];
}