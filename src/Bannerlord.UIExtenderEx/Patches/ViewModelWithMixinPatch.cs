using Bannerlord.UIExtenderEx.Components;
using Bannerlord.UIExtenderEx.Extensions;

using HarmonyLib;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches
{
    internal static class ViewModelWithMixinPatch
    {
        private static ConcurrentDictionary<Type, object?> RegisteredViewModels { get; } = new ConcurrentDictionary<Type, object?>();

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "IteratorMethodResultIsIgnored")]
        public static void Patch(Harmony harmony, Type viewModelType, IEnumerable<Type> mixins, string? refreshMethodName = null)
        {
            if (RegisteredViewModels.TryAdd(viewModelType, null)) // first initialization
            {
                foreach (var constructor in mixins.SelectMany(m => m.GetConstructors()))
                {
                    harmony.Patch(
                        constructor,
                        transpiler: new HarmonyMethod(SymbolExtensions.GetMethodInfo(() => ViewModel_Constructor_Transpiler(null!))));
                }

                if (refreshMethodName is not null && AccessTools.Method(viewModelType, refreshMethodName) is { } method)
                {
                    harmony.Patch(
                        method,
                        transpiler: new HarmonyMethod(SymbolExtensions.GetMethodInfo(() => ViewModel_Refresh_Transpiler(null!))));
                }

                // TODO: recursion
                harmony.Patch(
                    AccessTools.DeclaredMethod(viewModelType, nameof(ViewModel.OnFinalize)) ?? SymbolExtensions.GetMethodInfo((ViewModel vm) => vm.OnFinalize()),
                    transpiler: new HarmonyMethod(SymbolExtensions.GetMethodInfo(() => ViewModel_Finalize_Transpiler(null!))));
            }
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<CodeInstruction> ViewModel_Constructor_Transpiler(IEnumerable<CodeInstruction> instructions) => InsertMethodAtEnd(instructions, SymbolExtensions.GetMethodInfo(() => Constructor(null!)));
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Constructor(ViewModel viewModel)
        {
            foreach (var runtime in UIExtender.GetAllRuntimes())
            {
                if (!runtime.ViewModelComponent.Enabled)
                    continue;

                runtime.ViewModelComponent.InitializeMixinsForVMInstance(viewModel.GetType(), viewModel);

                if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(ViewModelComponent.MixinCacheKey(viewModel), out var list))
                    continue;

                foreach (var mixin in list)
                {
                    foreach (var (key, value) in runtime.ViewModelComponent.MixinInstancePropertyCache[mixin])
                    {
                        viewModel.AddProperty(key, value);
                    }
                }
            }
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<CodeInstruction> ViewModel_Refresh_Transpiler(IEnumerable<CodeInstruction> instructions) => InsertMethodAtEnd(instructions, SymbolExtensions.GetMethodInfo(() => Refresh(null!)));
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Refresh(ViewModel viewModel)
        {
            foreach (var runtime in UIExtender.GetAllRuntimes())
            {
                if (!runtime.ViewModelComponent.Enabled || !runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(ViewModelComponent.MixinCacheKey(viewModel), out var list))
                    continue;

                foreach (var mixin in list)
                    mixin.OnRefresh();
            }
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<CodeInstruction> ViewModel_Finalize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ret)
                {
                    var labels = instruction.labels;
                    instruction.labels = new List<Label>();
                    yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = labels };
                    yield return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => Finalize(null!)));
                }

                yield return instruction;
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Finalize(ViewModel viewModel)
        {
            foreach (var runtime in UIExtender.GetAllRuntimes())
            {
                if (!runtime.ViewModelComponent.Enabled || !runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(ViewModelComponent.MixinCacheKey(viewModel), out var list))
                    continue;

                foreach (var mixin in list)
                    mixin.OnFinalize();
            }
        }

        private static IEnumerable<CodeInstruction> InsertMethodAtEnd(IEnumerable<CodeInstruction> instructions, MethodInfo method)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ret)
                {
                    var labels = instruction.labels;
                    instruction.labels = new List<Label>();
                    yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = labels };
                    yield return new CodeInstruction(OpCodes.Call, method);
                }

                yield return instruction;
            }
        }
    }
}