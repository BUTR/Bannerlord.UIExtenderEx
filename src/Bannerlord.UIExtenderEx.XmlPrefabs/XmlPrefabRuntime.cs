using Bannerlord.UIExtenderEx.Patches;

using HarmonyLib;

namespace Bannerlord.UIExtenderEx.XmlPrefabs;

/// <summary>
/// Initializes loader patches and capabilities for Gauntlet's native XML prefab system.
/// </summary>
public static class XmlPrefabRuntime
{
    private static readonly Harmony Harmony = new("bannerlord.uiextender.ex.xmlprefabs");
    private static readonly object InstallLock = new();
    private static bool _installed;

    /// <summary>Installs XML loader bug fixes and optimization patches. Idempotent.</summary>
    public static void Install()
    {
        lock (InstallLock)
        {
            if (_installed)
                return;
            _installed = true;
        }

        WidgetExtensionsPatch.Patch(Harmony);
        WidgetTemplatePatch.Patch(Harmony);
    }
}