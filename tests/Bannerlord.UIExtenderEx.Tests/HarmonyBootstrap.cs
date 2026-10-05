using Bannerlord.UIExtenderEx.Tests;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

/// <summary>
/// Configures TaleWorlds base path redirections to point at the assembly <c>Assets</c> directory, exposing <c>Modules/TestModule</c>,
/// then initializes UIExtenderEx via <see cref="TestRuntime"/>.
/// <para>
/// Establishes global assembly-level setup before test execution, ensuring <c>ModuleInfoHelper</c> discovers test modules
/// during its initial process-wide discovery scan.
/// </para>
/// <para>
/// Declared without an enclosing namespace so that NUnit applies this <see cref="SetUpFixtureAttribute"/> across the entire test assembly.
/// </para>
/// </summary>
[SetUpFixture]
public class HarmonyBootstrap
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AssetsBasePath(ref string __result)
    {
        __result = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
        return false;
    }

    [OneTimeSetUp]
    public void BringUpTheDetourRuntime()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());

        var harmony = new Harmony("Bannerlord.UIExtenderEx.Tests.BasePath");
        harmony.Patch(SymbolExtensions2.GetMethodInfo(() => TaleWorlds.Engine.Utilities.GetBasePath()),
            prefix: new HarmonyMethod(typeof(HarmonyBootstrap), nameof(AssetsBasePath)));
        harmony.Patch(SymbolExtensions2.GetPropertyGetter(() => TaleWorlds.Library.BasePath.Name),
            prefix: new HarmonyMethod(typeof(HarmonyBootstrap), nameof(AssetsBasePath)));

        TestRuntime.Start();
    }

    [OneTimeTearDown]
    public void FlushTheLog() => Trace.Flush();
}