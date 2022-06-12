using Bannerlord.BUTR.Shared.Extensions;
using Bannerlord.UIExtenderEx.Extensions;

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

namespace Bannerlord.UIExtenderEx.Patches
{
    internal static class ViewModelWithMixinPatch
    {
        private static ConcurrentDictionary<Type, object?> RegisteredViewModels { get; } = new();

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
                        transpiler: new HarmonyMethod(SymbolExtensions2.GetMethodInfo(() => ViewModel_Constructor_Transpiler(null!))));
                }

                if (refreshMethodName is not null && AccessTools2.Method(viewModelType, refreshMethodName) is { } method)
                {
                    harmony.Patch(
                        method,
                        transpiler: new HarmonyMethod(SymbolExtensions2.GetMethodInfo(() => ViewModel_Refresh_Transpiler(null!))));
                }

                // TODO: recursion
                harmony.Patch(
                    AccessTools.DeclaredMethod(viewModelType, nameof(ViewModel.OnFinalize)) ??
                    SymbolExtensions2.GetMethodInfo((ViewModel vm) => vm.OnFinalize()),
                    transpiler: new HarmonyMethod(SymbolExtensions2.GetMethodInfo(() => ViewModel_Finalize_Transpiler(null!))));
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

                if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list))
                    continue;

                foreach (var mixin in list)
                {
                    if (!runtime.ViewModelComponent.MixinInstancePropertyCache.TryGetValue(mixin, out var propertyExtensions))
                        continue;

                    foreach (var (key, value) in propertyExtensions)
                        viewModel.AddProperty(key, value);
                }

                // Call Refresh on Constructor end if it was called within it
                if (runtime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.TryGetValue(viewModel, out _))
                {
                    foreach (var mixin in list)
                        mixin.OnRefresh();
                    runtime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.Remove(viewModel);
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
                if (!runtime.ViewModelComponent.Enabled)
                    continue;

                // Refresh was called from VM Constructor, delay the call to Refresh()
                if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list))
                {
                    runtime.ViewModelComponent.MixinInstanceRefreshFromConstructorCache.GetOrAdd(viewModel, _ => null!);
                    continue;
                }

                foreach (var mixin in list)
                    mixin.OnRefresh();
            }
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<CodeInstruction> ViewModel_Finalize_Transpiler(IEnumerable<CodeInstruction> instructions) => InsertMethodAtEnd(instructions, SymbolExtensions.GetMethodInfo(() => Finalize(null!)));
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Finalize(ViewModel viewModel)
        {
            foreach (var runtime in UIExtender.GetAllRuntimes())
            {
                if (!runtime.ViewModelComponent.Enabled || !runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list))
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