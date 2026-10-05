using Bannerlord.UIExtenderEx.Tests.Oracle;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Oracle;

/// <summary>
/// Verifies structural inspection and comparison capabilities of <see cref="WidgetSnapshot"/>,
/// ensuring field-level differences, collection items, and event handlers are captured accurately.
/// </summary>
public class WidgetSnapshotTests
{
    /// <summary>
    /// Test widget storing internal state within private fields and write-only properties.
    /// </summary>
    public class HiddenStateWidget : Widget
    {
        public HiddenStateWidget(UIContext context) : base(context) { }

        private string? _hidden;
        public string Hidden { set => _hidden = value; }

        public List<string> Names { get; } = [];

        public event Action? Changed;
        public void Subscribe() => Changed += () => { };
    }

    private static List<string> Differences(Action<HiddenStateWidget> xml, Action<HiddenStateWidget> compiled)
    {
        using var workspace = new PrefabWorkspace();
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(WidgetSnapshotTests));
        var fromXml = new HiddenStateWidget(ui.Context);
        var fromCompiled = new HiddenStateWidget(ui.Context);
        xml(fromXml);
        compiled(fromCompiled);
        return [.. WidgetSnapshot.Compare(fromXml, fromCompiled, null, x => x).Select(x => x.Key)];
    }

    [Test]
    public void TheSameState_Matches() =>
        Assert.That(Differences(x => { x.Hidden = "a"; x.Names.Add("b"); }, x => { x.Hidden = "a"; x.Names.Add("b"); }), Is.Empty);

    [Test]
    public void APrivateField_IsCompared() =>
        Assert.That(Differences(x => x.Hidden = "a", x => x.Hidden = "b"), Is.EqualTo(new[] { "root:HiddenStateWidget._hidden" }));

    [Test]
    public void TheItemsOfACollection_AreCompared() =>
        Assert.That(Differences(x => x.Names.Add("a"), x => x.Names.Add("b")), Is.EqualTo(new[] { "root.Names[0]" }));

    [Test]
    public void TheHandlersSubscribedToAnEvent_AreCounted() =>
        Assert.That(Differences(_ => { }, x => x.Subscribe()), Is.EqualTo(new[] { "root:HiddenStateWidget.Changed" }));

    [Test]
    public void AFieldOfTheWidgetClassItself_IsCompared() =>
        Assert.That(Differences(_ => { }, x => x.DoNotAcceptEvents = !x.DoNotAcceptEvents).Where(x => x.StartsWith("root:Widget.", StringComparison.Ordinal)), Is.Not.Empty);
}
