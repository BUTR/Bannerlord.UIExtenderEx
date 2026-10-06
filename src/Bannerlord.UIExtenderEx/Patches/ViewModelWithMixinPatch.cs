using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Components;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.Utils;
using Bannerlord.UIExtenderEx.ViewModels;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Installs lifecycle hooks into target ViewModel constructors, refresh methods, and <c>OnFinalize</c> to manage
/// mixin instantiation, refresh notifications, and cleanup.
/// <para>
/// In inheritance hierarchies, base constructors and virtual method overrides chain into each other. To prevent
/// premature mixin initialization (such as initializing mixins before derived constructors complete) or duplicate
/// executions across base calls, transpiled hooks verify whether the executing method is the most derived governing
/// implementation for the active runtime instance.
/// </para>
/// </summary>
internal static class ViewModelWithMixinPatch
{
    private static ConcurrentDictionary<Type, object?> ViewModelInitializations { get; } = new();
    private static ConcurrentDictionary<string, object?> ViewModelsRefreshPatches { get; } = new();

    private static readonly object Lock = new();

    // Registry of all patched methods. Transpiled hook calls identify the active method by index.
    private static readonly List<MethodBase> Patched = [];
    private static readonly Dictionary<MethodBase, int> PatchedIndex = new();

    // Caches whether a patched method represents the governing entry point for a specific instance type
    private static readonly ConcurrentDictionary<(Type Type, int Index), bool> Governs = new();

    private static readonly ConcurrentDictionary<Type, string?> RefreshMethodNames = new();

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "IteratorMethodResultIsIgnored")]
    public static void Patch(Harmony harmony, Type viewModelType, string? refreshMethodName = null)
    {
        if (ViewModelInitializations.TryAdd(viewModelType, null)) // first initialization
        {
            foreach (var constructor in AccessTools.GetDeclaredConstructors(viewModelType, false))
            {
                if (!harmony.TryPatch(constructor, transpiler: AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), nameof(ViewModel_Constructor_Transpiler))))
                    MessageUtils.DisplayUserWarning("Failed to patch a constructor of {0}! What mods add to that screen will be missing.", viewModelType.FullName!);
            }

            // The override the type resolves to, which may be declared on any type between it and ViewModel
            if (!harmony.TryPatch(DeclaredImplementation(viewModelType, nameof(ViewModel.OnFinalize)), transpiler: AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), nameof(ViewModel_Finalize_Transpiler))))
                MessageUtils.DisplayUserWarning("Failed to patch {0}.OnFinalize! What mods add to that screen will not be cleaned up when it closes, and the game may slow down over time.", viewModelType.FullName!);
        }

        if (ViewModelsRefreshPatches.TryAdd($"{viewModelType.FullName}:{refreshMethodName}", null)) // first initialization
        {
            // multiple mixins have their own name
            if (refreshMethodName is null)
            {
                return;
            }

            if (DeclaredImplementation(viewModelType, refreshMethodName) is not { } method)
            {
                // A method of that name that was not resolved is overloaded, with no overload that takes no parameters to hook
                var overloaded = false;
                for (var type = viewModelType; type is not null && !overloaded; type = type.BaseType)
                    overloaded = AccessTools.GetDeclaredMethods(type).Any(x => x.Name == refreshMethodName);
                MessageUtils.DisplayUserWarning(overloaded
                    ? "{0} has no method {1} to hook: it is overloaded, and no overload takes no parameters! Information mods add to that screen will not update while it is open."
                    : "{0} has no method {1}! Information mods add to that screen will not update while it is open.", viewModelType.FullName!, refreshMethodName);
                return;
            }

            if (!harmony.TryPatch(method, transpiler: AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), nameof(ViewModel_Refresh_Transpiler))))
                MessageUtils.DisplayUserWarning("Failed to patch {0}.{1}! Information mods add to that screen will not update while it is open.", viewModelType.FullName!, refreshMethodName);
        }
    }

    private static MethodInfo? DeclaredImplementation(Type type, string name)
    {
        var method = AccessTools2.Method(type, name, logErrorInTrace: false);
        while (method is not null && !method.IsDeclaredMember())
            method = method.GetDeclaredMember();
        return method;
    }

    private static int IndexOf(MethodBase method)
    {
        lock (Lock)
        {
            if (PatchedIndex.TryGetValue(method, out var index))
                return index;

            PatchedIndex[method] = index = Patched.Count;
            Patched.Add(method);
            return index;
        }
    }

    private static MethodBase PatchedAt(int index)
    {
        lock (Lock)
            return Patched[index];
    }

    private static bool IsCallOn(ViewModel viewModel, int index) => Governs.GetOrAdd((viewModel.GetType(), index), static key =>
    {
        var patched = PatchedAt(key.Index);

        // A base constructor ends before the derived one has run its body; the mixins wait for the instance's own
        if (patched is ConstructorInfo)
            return patched.DeclaringType == key.Type;

        // A method nothing overrides is the call wherever it ends
        if (patched is not MethodInfo { IsVirtual: true } method)
            return true;

        // The most derived override of the same slot, which an override's base call lands below
        var slot = method.GetBaseDefinition();
        var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();
        for (var type = key.Type; type is not null; type = type.BaseType)
        {
            var candidate = type.GetMethod(method.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, parameters, null);
            if (candidate is not null && candidate.GetBaseDefinition().MethodHandle == slot.MethodHandle)
                return candidate.MethodHandle == method.MethodHandle;
        }
        return false;
    });

    private static string? RefreshMethodNameOf(IViewModelMixin mixin) =>
        RefreshMethodNames.GetOrAdd(mixin.GetType(), static x => x.GetCustomAttribute<ViewModelMixinAttribute>()?.RefreshMethodName);

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> ViewModel_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method) =>
        InsertMethodAtEnd(instructions, method, AccessTools2.DeclaredMethod(typeof(ViewModelWithMixinPatch), nameof(Constructor)));
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Constructor(ViewModel viewModel, int patched)
    {
        if (!IsCallOn(viewModel, patched))
            return;

        foreach (var runtime in UIExtender.GetAllRuntimes())
        {
            runtime.ViewModelComponent.InitializeMixinsForVMInstance(viewModel);

            if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list))
            {
                continue;
            }

            // Call Refresh on Constructor end if it was called within it
            // Only if it was: an OnRefresh after every construction was rejected. Of the game's ~535 ViewModels that override
            // RefreshValues, about 100 do not refresh in their constructor. They wait for a method that hands them their data
            // (GameMenuItemVM.InitializeWith, SceneNotificationVM.SetData, ScoreboardBaseVM.Initialize), and a forced refresh
            // would reach the mixin before the host has anything to show.
            if (runtime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.TryGetValue(viewModel, out var calledRefresMethods))
            {
                foreach (var mixin in list)
                {
                    var refreshMethodName = RefreshMethodNameOf(mixin);
                    foreach (var methodName in calledRefresMethods)
                    {
                        if (methodName == refreshMethodName)
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
    private static void Refresh(ViewModel viewModel, int patched)
    {
        if (!IsCallOn(viewModel, patched))
            return;

        var methodName = PatchedAt(patched).Name;
        foreach (var runtime in UIExtender.GetAllRuntimes())
        {
            // A ViewModel this runtime has no mixins for has nothing to refresh, now or once constructed
            if (!runtime.ViewModelComponent.Mixins.ContainsKey(viewModel.GetType()))
                continue;

            // Refresh was called from VM Constructor, delay the call to Refresh()
            if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list))
            {
                // Once per name: the mixins are refreshed once after the constructor however often it refreshed. An
                // instance constructed before this module registered has no constructor end left to drain its entry, and
                // would otherwise grow it on every refresh for as long as it lives.
                var deferred = runtime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.GetOrAdd(viewModel, _ => []);
                if (!deferred.Contains(methodName))
                    deferred.Add(methodName);
                continue;
            }

            foreach (var mixin in list)
            {
                if (methodName == RefreshMethodNameOf(mixin))
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
    private static void Finalize(ViewModel viewModel, int patched)
    {
        if (!IsCallOn(viewModel, patched))
            return;

        foreach (var runtime in UIExtender.GetAllRuntimes())
            FinalizeMixins(runtime.ViewModelComponent, viewModel);

        // A deregistered module's mixins stay on the instances they were attached to, and are finalized with them
        foreach (var component in ViewModelComponent.Retired)
            FinalizeMixins(component, viewModel);
    }

    private static void FinalizeMixins(ViewModelComponent component, ViewModel viewModel)
    {
        if (!component.MixinInstanceCache.TryGetValue(viewModel, out var list))
            return;

        foreach (var mixin in list)
        {
            (mixin as IViewModelMixinNotifications)?.Unsubscribe();
            mixin.OnFinalize();
        }
    }

    private static IEnumerable<CodeInstruction> InsertMethodAtEnd(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod, MethodInfo? method)
    {
        var patched = IndexOf(originalMethod);
        foreach (var instruction in instructions)
        {
            if (method is not null && instruction.opcode == OpCodes.Ret)
            {
                var labels = instruction.labels;
                instruction.labels = [];
                yield return new(OpCodes.Ldarg_0) { labels = labels };
                yield return new(OpCodes.Ldc_I4, patched);
                yield return new(OpCodes.Call, method);
            }

            yield return instruction;
        }
    }
}