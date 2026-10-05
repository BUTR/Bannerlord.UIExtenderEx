using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using HarmonyLib;

using NUnit.Framework;

using System;
using System.Linq;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.GamepadNavigation;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies gamepad navigation scope lifecycle handling, ensuring that ancestor visibility subscriptions
/// detach when widgets depart the visual tree, preventing event memory leaks on released movies or removed list items.
/// </summary>
public class NavigationScopeReleaseTests
{
    private PrefabWorkspace _workspace = null!;
    private UIContext _context = null!;

    /// <summary>Refreshes <see cref="WidgetInfo"/> metadata to register <see cref="NavigationScopeTargeter"/> prior to test execution.</summary>
    [OneTimeSetUp]
    public void KnowTheTargeter()
    {
        _ = typeof(NavigationScopeTargeter).Assembly;
        PrefabWorkspace.RefreshWidgetInfo();
    }

    [SetUp]
    public void SetUp()
    {
        GauntletGamepadNavigationManager.Initialize();
        _workspace = new PrefabWorkspace();
        _context = new TestUIContext(_workspace.ResourceDepot, TestContext.CurrentContext.Test.Name).Context;
    }

    [TearDown]
    public void TearDown()
    {
        _context.OnFinalize();
        _workspace.Dispose();
    }

    /// <summary>Initializes navigation context using the standard <c>GauntletLayer.InitializeContext</c> contract.</summary>
    private void WithTheLayersNavigationContext() =>
        _context.InitializeGamepadNavigation(new GauntletGamepadNavigationContext(_ => false, () => 0, () => true));

    private static int SubscriptionsOf(Widget widget, GamepadNavigationScope scope) =>
        (AccessTools.Field(typeof(Widget), nameof(Widget.OnVisibilityChanged)).GetValue(widget) as Delegate)?
        .GetInvocationList().Count(x => ReferenceEquals(x.Target, scope)) ?? 0;

    /// <summary>Constructs a widget hierarchy containing a list panel and an item widget scoped by a navigation targeter.</summary>
    private (Widget MovieRoot, Widget List, Widget Item, GamepadNavigationScope Scope) ListItemWithAScope()
    {
        var movieRoot = new Widget(_context);
        _context.Root.AddChild(movieRoot);
        var list = new Widget(_context);
        movieRoot.AddChild(list);
        // Attach the item widget to the tree before appending child targeters and configuring scope parents.
        var item = new Widget(_context);
        list.AddChild(item);
        var targeter = new NavigationScopeTargeter(_context);
        item.AddChild(targeter);
        targeter.ScopeParent = item;
        return (movieRoot, list, item, targeter.NavigationScope);
    }

    [Test]
    public void ARemovedListItem_IsSubscribedToNoneOfItsFormerAncestors()
    {
        WithTheLayersNavigationContext();
        var (movieRoot, list, item, scope) = ListItemWithAScope();
        Assert.That(SubscriptionsOf(list, scope) + SubscriptionsOf(movieRoot, scope) + SubscriptionsOf(_context.Root, scope), Is.EqualTo(3),
            "subscribed to the list, the movie root and the layer root while in the tree");

        list.RemoveChild(item);

        Assert.That(SubscriptionsOf(list, scope), Is.Zero);
        Assert.That(SubscriptionsOf(movieRoot, scope), Is.Zero);
        Assert.That(SubscriptionsOf(_context.Root, scope), Is.Zero);
        var scopeParents = (System.Collections.IDictionary) AccessTools.Field(typeof(GauntletGamepadNavigationManager), "_navigationScopeParents").GetValue(GauntletGamepadNavigationManager.Instance);
        Assert.That(scopeParents.Contains(item), Is.False, "and the manager holds it no more");
    }

    [Test]
    public void AReleasedMovie_LeavesNoScopeSubscribedToTheLayerRoot()
    {
        WithTheLayersNavigationContext();
        var (movieRoot, _, _, scope) = ListItemWithAScope();

        movieRoot.ParentWidget = null;

        Assert.That(SubscriptionsOf(_context.Root, scope), Is.Zero);
    }

    [Test]
    public void WithoutTheLayersNavigationContext_NoScopeIsRegistered_SoARemovedItemStaysSubscribed()
    {
        var (movieRoot, list, item, scope) = ListItemWithAScope();

        list.RemoveChild(item);

        Assert.That(SubscriptionsOf(list, scope) + SubscriptionsOf(movieRoot, scope) + SubscriptionsOf(_context.Root, scope), Is.EqualTo(3));
    }
}