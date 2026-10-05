using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies <see cref="EventManagerPatch"/> behavior ensuring that widgets leaving the visual tree are removed
/// from the event manager's visual definition update list and re-registered upon reinsertion.
/// </summary>
public class EventManagerPatchTests
{
    private PrefabWorkspace _workspace = null!;
    private UIContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        // Initialize Gauntlet gamepad navigation manager required during widget tree departure callbacks.
        TaleWorlds.GauntletUI.GamepadNavigation.GauntletGamepadNavigationManager.Initialize();
        _workspace = new PrefabWorkspace();
        _context = new TestUIContext(_workspace.ResourceDepot, TestContext.CurrentContext.Test.Name).Context;
    }

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    private static VisualDefinition NewVisualDefinition() =>
        new("EventManagerPatchTests", 0.1f, 0f, AnimationInterpolation.Type.Linear, AnimationInterpolation.Function.Sine);

    /// <summary>Retrieves the widgets actively tracked by the event manager for visual definition updates.</summary>
    private List<Widget> UpdatedVisualDefinitions() => Updated("VisualDefinition");

    /// <summary>Retrieves the widgets actively tracked by the event manager in the container of that type.</summary>
    private List<Widget> Updated(string containerType)
    {
        var type = Enum.Parse(AccessTools2.TypeByName("TaleWorlds.GauntletUI.WidgetContainer+ContainerType")!, containerType);
        var container = AccessTools.Field(typeof(EventManager), "_widgetContainers")?.GetValue(_context.EventManager) switch
        {
            IDictionary dictionary => dictionary[type], // v1.3.14+
            Array array => array.GetValue((int) type), // v1.3.12, v1.3.13
            _ => AccessTools.Field(typeof(EventManager), $"_widgetsWith{containerType}sContainer").GetValue(_context.EventManager), // up to v1.3.11
        };
        var widgets = (IEnumerable) (AccessTools.Field(container!.GetType(), "_backList")?.GetValue(container)
                                     ?? AccessTools.Method(container.GetType(), "GetCurrentList").Invoke(container, null))!;
        return [.. widgets.Cast<Widget>()];
    }

    [Test]
    public void AWidgetWithAVisualDefinition_LeavesTheUpdatesWhenItLeavesTheTree()
    {
        var widget = new Widget(_context);
        _context.Root.AddChild(widget);
        widget.VisualDefinition = NewVisualDefinition();
        Assert.That(UpdatedVisualDefinitions(), Does.Contain(widget), "updated while in the tree");

        _context.Root.RemoveChild(widget);

        Assert.That(UpdatedVisualDefinitions(), Does.Not.Contain(widget), "and held by the event manager no more");
    }

    [Test]
    public void AReleasedTree_LeavesNoWidgetInTheUpdates()
    {
        // Assemble and attach a widget subtree containing visual definitions to the root context.
        var root = new Widget(_context);
        var child = new BrushWidget(_context) { VisualDefinition = NewVisualDefinition() };
        root.AddChild(child);
        _context.Root.AddChild(root);
        Assert.That(UpdatedVisualDefinitions(), Does.Contain(child), "registered when the tree is attached");

        root.ParentWidget = null;

        Assert.That(UpdatedVisualDefinitions(), Does.Not.Contain(child));
    }

    [Test]
    public void AWidgetPutBack_IsUpdatedAgain()
    {
        var widget = new Widget(_context);
        _context.Root.AddChild(widget);
        widget.VisualDefinition = NewVisualDefinition();
        _context.Root.RemoveChild(widget);

        _context.Root.AddChild(widget);

        Assert.That(UpdatedVisualDefinitions(), Does.Contain(widget));
    }

    [Test]
    public void AWidgetWithTweenPosition_LeavesTheUpdatesWhenItLeavesTheTree()
    {
        // TweenPosition is gone from v1.3.14
        if (AccessTools.Property(typeof(Widget), "TweenPosition") is not { } tweenPosition)
        {
            Assert.Ignore("this game has no TweenPosition");
            return;
        }

        var widget = new Widget(_context);
        _context.Root.AddChild(widget);
        tweenPosition.SetValue(widget, true);
        Assert.That(Updated("TweenPosition"), Does.Contain(widget), "updated while in the tree");

        _context.Root.RemoveChild(widget);

        Assert.That(Updated("TweenPosition"), Does.Not.Contain(widget), "and held by the event manager no more");
    }

    [Test]
    public void AVisualDefinitionTakenAway_LeavesTheUpdates()
    {
        // Up to v1.3.11 the game keeps the widget here; from v1.3.12 it lets it go itself
        var widget = new Widget(_context);
        _context.Root.AddChild(widget);
        widget.VisualDefinition = NewVisualDefinition();

        widget.VisualDefinition = null;

        Assert.That(UpdatedVisualDefinitions(), Does.Not.Contain(widget));
    }

    [Test]
    public void AVisualDefinitionTakenAway_ThenTheTreeReleased_LeavesNoWidgetInTheUpdates()
    {
        var widget = new Widget(_context);
        _context.Root.AddChild(widget);
        widget.VisualDefinition = NewVisualDefinition();
        widget.VisualDefinition = null;

        _context.Root.RemoveChild(widget);

        Assert.That(UpdatedVisualDefinitions(), Does.Not.Contain(widget));
    }

    [Test]
    public void AVisualDefinitionTakenAway_AndGivenAgain_IsUpdatedAgain()
    {
        var widget = new Widget(_context);
        _context.Root.AddChild(widget);
        widget.VisualDefinition = NewVisualDefinition();
        widget.VisualDefinition = null;

        widget.VisualDefinition = NewVisualDefinition();

        Assert.That(UpdatedVisualDefinitions(), Does.Contain(widget));
    }

    [Test]
    public void ATweenPositionTakenAway_LeavesTheUpdates()
    {
        // TweenPosition is gone from v1.3.14
        if (AccessTools.Property(typeof(Widget), "TweenPosition") is not { } tweenPosition)
        {
            Assert.Ignore("this game has no TweenPosition");
            return;
        }

        var widget = new Widget(_context);
        _context.Root.AddChild(widget);
        tweenPosition.SetValue(widget, true);

        tweenPosition.SetValue(widget, false);

        Assert.That(Updated("TweenPosition"), Does.Not.Contain(widget));
    }
}