using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using System;
using System.Collections.Generic;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Provides convenience wrappers over <see cref="PrefabFingerprint"/> for test suites that omit direct fingerprint input tracking.
/// </summary>
internal static class TestFingerprint
{
    public static string? Compute(WidgetFactory widgetFactory, string movieName, Type viewModelType) =>
        PrefabFingerprint.Compute(widgetFactory, movieName, viewModelType, out _);

    public static HashSet<string> CollectClosure(WidgetFactory widgetFactory, string movieName)
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(widgetFactory);
        return PrefabFingerprint.CollectClosureAndWidgetTypes(widgetFactory, movieName).Prefabs;
    }
}
