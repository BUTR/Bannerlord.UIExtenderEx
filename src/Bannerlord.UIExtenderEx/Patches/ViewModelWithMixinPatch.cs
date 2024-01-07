using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Extensions;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches;

internal static class ViewModelWithMixinPatch
{
    private static ConcurrentDictionary<Type, object?> ViewModelInitializations { get; } = new();
    private static ConcurrentDictionary<string, object?> ViewModelsRefreshPatches { get; } = new();

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "IteratorMethodResultIsIgnored")]
    public static void Patch(Harmony harmony, Type viewModelType, string? refreshMethodName = null)
    {
        if (ViewModelInitializations.TryAdd(viewModelType, null)) // first initialization
        {
            foreach (var constructor in AccessTools.GetDeclaredConstructors(viewModelType, false))
            {
                harmony.Patch(
                    constructor,
                    transpiler: new HarmonyMethod(typeof(ViewModelWithMixinPatch), nameof(ViewModel_Constructor_Transpiler)));
            }

            harmony.Patch(
                AccessTools2.DeclaredMethod(viewModelType, nameof(ViewModel.OnFinalize), logErrorInTrace: false) ??
                AccessTools2.DeclaredMethod("TaleWorlds.Library.ViewModel:OnFinalize"),
                transpiler: new HarmonyMethod(typeof(ViewModelWithMixinPatch), nameof(ViewModel_Finalize_Transpiler)));
        }

        if (ViewModelsRefreshPatches.TryAdd($"{viewModelType.FullName}:{refreshMethodName}", null)) // first initialization
        {
            // multiple mixins have their own name
            if (refreshMethodName is not null && AccessTools2.Method(viewModelType, refreshMethodName) is { } method)
            {
                harmony.Patch(
                    method,
                    transpiler: new HarmonyMethod(typeof(ViewModelWithMixinPatch), nameof(ViewModel_Refresh_Transpiler)));
            }
        }
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> ViewModel_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method) =>
        InsertMethodAtEnd(instructions, method, AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), nameof(Constructor)));
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Constructor(ViewModel viewModel, string _)
    {
        foreach (var runtime in UIExtender.GetAllRuntimes())
        {
            runtime.ViewModelComponent.InitializeMixinsForVMInstance(viewModel);

            if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list))
            {
                continue;
            }

            // Call Refresh on Constructor end if it was called within it
            if (runtime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.TryGetValue(viewModel, out var calledRefresMethods))
            {
                foreach (var mixin in list)
                {
                    var attribute = mixin.GetType().GetCustomAttribute<ViewModelMixinAttribute>();
                    foreach (var methodName in calledRefresMethods)
                    {
                        if (methodName == attribute?.RefreshMethodName)
                        {
                            mixin.OnRefresh();
                        }
                    }
                }
                runtime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.Remove(viewModel);
            }
        }
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> ViewModel_Refresh_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method) =>
        InsertMethodAtEnd(instructions, method, AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), nameof(Refresh)));
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Refresh(ViewModel viewModel, string methodName)
    {
        foreach (var runtime in UIExtender.GetAllRuntimes())
        {
            // Refresh was called from VM Constructor, delay the call to Refresh()
            if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list))
            {
                runtime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.GetOrAdd(viewModel, _ => new List<string>()).Add(methodName);
                continue;
            }

            foreach (var mixin in list)
            {
                var attribute = mixin.GetType().GetCustomAttribute<ViewModelMixinAttribute>();
                if (methodName == attribute?.RefreshMethodName)
                {
                    mixin.OnRefresh();
                }
            }
        }
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> ViewModel_Finalize_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method) =>
        InsertMethodAtEnd(instructions, method, AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), nameof(Finalize)));
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Finalize(ViewModel viewModel, string _)
    {
        foreach (var runtime in UIExtender.GetAllRuntimes())
        {
            if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list))
            {
                continue;
            }

            foreach (var mixin in list)
            {
                mixin.OnFinalize();
            }
        }
    }

    private static IEnumerable<CodeInstruction> InsertMethodAtEnd(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod, MethodInfo? method)
    {
        foreach (var instruction in instructions)
        {
            if (method is not null && instruction.opcode == OpCodes.Ret)
            {
                var labels = instruction.labels;
                instruction.labels = new List<Label>();
                yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = labels };
                yield return new CodeInstruction(OpCodes.Ldstr, originalMethod.Name);
                yield return new CodeInstruction(OpCodes.Call, method);
            }

            yield return instruction;
        }
    }
}