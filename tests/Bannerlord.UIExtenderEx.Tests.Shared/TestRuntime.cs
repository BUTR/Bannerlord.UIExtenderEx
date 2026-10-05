using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GamePrefabs;
using Bannerlord.UIExtenderEx.XmlPrefabs;

using HarmonyLib;

using System.Linq;
using System.Runtime.CompilerServices;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Initializes a <see cref="Harmony"/> instance and starts UIExtenderEx runtimes in game-identical order for test assemblies.
/// <para>
/// On .NET 6, initializes MonoMod detour infrastructure ahead of reflection lookups to guarantee static field accessor resolution
/// for <see cref="TaleWorlds.Library.ViewModel"/> extensions and GauntletUI binding tables.
/// </para>
/// <para>
/// Installs <see cref="XmlPrefabRuntime"/>, <see cref="GamePrefabRuntime"/>, and <see cref="CompiledPrefabRuntime"/> prior to
/// initializing <see cref="UIExtender"/> to mirror the SubModule initialization sequence from <c>SubModule.xml</c>, ensuring
/// prefab parse events and fingerprint hashes register reliably.
/// </para>
/// </summary>
public static class TestRuntime
{
    public static void Start()
    {
        // Disable interactive modal dialogs on assertion failures under .NET Framework to prevent hanging headless test runs.
        foreach (var listener in System.Diagnostics.Trace.Listeners.OfType<System.Diagnostics.DefaultTraceListener>())
            listener.AssertUiEnabled = false;

        _ = new Harmony("Bannerlord.UIExtenderEx.Tests.Bootstrap");

        XmlPrefabRuntime.Install();
        GamePrefabRuntime.Install();
        CompiledPrefabRuntime.Install();
        RuntimeHelpers.RunClassConstructor(typeof(UIExtender).TypeHandle);
    }
}