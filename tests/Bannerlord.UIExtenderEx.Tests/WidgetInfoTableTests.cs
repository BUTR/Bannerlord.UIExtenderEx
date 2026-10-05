using Bannerlord.UIExtenderEx.ResourceManager;

using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Collections.Generic;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies dynamic injection of widget type metadata directly into TaleWorlds <see cref="WidgetInfo"/>
/// without triggering expensive full-assembly reflection rescans.
/// </summary>
public class WidgetInfoTableTests
{
    private sealed class TableTestWidget : Widget
    {
        public TableTestWidget(UIContext context) : base(context) { }
    }

    [Test]
    public void Add_PutsTheClassIntoTheGamesTableInPlace()
    {
        // Remove the test class from the existing table to simulate late assembly loading.
        WidgetInfo.Refresh();
        var table = AccessTools2.StaticFieldRefAccess<Dictionary<Type, WidgetInfo>>(typeof(WidgetInfo), "_widgetInfos");
        Assert.That(table, Is.Not.Null, "the game renamed or removed WidgetInfo._widgetInfos; WidgetInfoTable falls back to a rescan, check the cost");
        table!().Remove(typeof(TableTestWidget));
        Assert.That(() => WidgetInfo.GetWidgetInfo(typeof(TableTestWidget)), Throws.TypeOf<KeyNotFoundException>());

        Assert.That(WidgetInfoTable.Add([typeof(TableTestWidget)]), Is.True, "edited in place, no rescan");

        Assert.That(WidgetInfo.GetWidgetInfo(typeof(TableTestWidget)).Type, Is.EqualTo(typeof(TableTestWidget)));
        Assert.That(WidgetInfoTable.Add([typeof(TableTestWidget)]), Is.True, "adding a known class again is harmless");
    }
}