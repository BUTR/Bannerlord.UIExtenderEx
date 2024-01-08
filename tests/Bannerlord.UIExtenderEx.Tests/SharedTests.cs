using Bannerlord.UIExtenderEx.Tests.Prefabs2;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bannerlord.UIExtenderEx.Tests;

public class SharedTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MockedGetBasePathPath(ref string __result)
    {
        __result = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
        return false;
    }

    private Harmony _harmony;

    [OneTimeSetUp]
    public void SharedOneTimeSetUp()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());

        _harmony = new Harmony($"{nameof(PrefabComponentPrefabs2Tests)}");
        _harmony.Patch(SymbolExtensions2.GetMethodInfo(() => TaleWorlds.Engine.Utilities.GetBasePath()),
            prefix: new HarmonyMethod(typeof(PrefabComponentPrefabs2Tests), nameof(MockedGetBasePathPath)));
        _harmony.Patch(SymbolExtensions2.GetPropertyGetter(() => TaleWorlds.Library.BasePath.Name),
            prefix: new HarmonyMethod(typeof(PrefabComponentPrefabs2Tests), nameof(MockedGetBasePathPath)));
    }

    [OneTimeTearDown]
    public void SharedOneTimeTearDown()
    {
        Trace.Flush();

        foreach (var patchedMethod in _harmony.GetPatchedMethods())
        {
            if (patchedMethod is not MethodInfo patchedMethodInfo)
                continue;

            if (Harmony.GetOriginalMethod(patchedMethodInfo) is not { } originalMethodInfo)
                continue;

            _harmony.Unpatch(originalMethodInfo, patchedMethodInfo);
        }
    }
}