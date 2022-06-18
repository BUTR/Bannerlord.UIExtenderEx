using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches
{
    internal static class ViewModelPatch
    {
        public static void Patch(Harmony harmony)
        {
            var e180 = ApplicationVersionHelper.TryParse("e1.8.0", out var e180Var) ? e180Var : ApplicationVersion.Empty;
            if (ApplicationVersionHelper.GameVersion() is { } gameVersion && gameVersion < e180)
            {
                harmony.Patch(
                    AccessTools2.DeclaredMethod("TaleWorlds.Library.ViewModel:ExecuteCommand"),
                    transpiler: new HarmonyMethod(typeof(ViewModelPatch), nameof(ViewModel_ExecuteCommand_Transpiler)));
            }
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<CodeInstruction> ViewModel_ExecuteCommand_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            var instructionsList = instructions.ToList();

            [MethodImpl(MethodImplOptions.NoInlining)]
            IEnumerable<CodeInstruction> ReturnDefault(string place)
            {
                MessageUtils.DisplayUserWarning("Failed to patch ViewModel.ExecuteCommand! {0}", place);
                return instructionsList.AsEnumerable();
            }

            if (AccessTools2.DeclaredMethod("Bannerlord.UIExtenderEx.Patches.ViewModelPatch:ExecuteCommand") is not { } executeCommand)
                return ReturnDefault("ViewModelPatch:ExecuteCommand not found");

            var jmpOriginalFlow = ilGenerator.DefineLabel();
            instructionsList[0].labels.Add(jmpOriginalFlow);

            instructionsList.InsertRange(0, new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Call, executeCommand),
                new(OpCodes.Brtrue, jmpOriginalFlow),
                new(OpCodes.Ret)
            });
            return instructionsList;
        }
        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ExecuteCommand(ViewModel viewModel, string commandName, params object[] parameters)
        {
            static object? ConvertValueTo(string value, Type parameterType)
            {
                if (parameterType == typeof(string))
                    return value;
                if (parameterType == typeof(int))
                    return Convert.ToInt32(value);
                if (parameterType == typeof(float))
                    return Convert.ToSingle(value);
                return null;
            }

            foreach (var runtime in UIExtender.GetAllRuntimes())
            {
                if (!runtime.ViewModelComponent.Enabled)
                    continue;

                var nativeMethod = AccessTools2.Method(viewModel.GetType(), commandName);
                var isNativeMethod = nativeMethod is not null;
                var hasMixins = runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var list);

                if (!isNativeMethod && !hasMixins)
                    return false; // stop original command execution
                if (isNativeMethod && !hasMixins)
                    continue; // skip to next Runtime

                foreach (var mixin in list)
                {
                    if (!runtime.ViewModelComponent.MixinInstanceMethodCache.TryGetValue(mixin, out var methodExtensions))
                        continue;

                    if (!(methodExtensions.FirstOrDefault(e => e.Key == commandName).Value is { } method))
                        continue;

                    if (method.GetParameters() is { } methodParameters && methodParameters.Length == parameters.Length)
                    {
                        var array = new object?[parameters.Length];
                        for (var i = 0; i < parameters.Length; i++)
                        {
                            var methodParameterType = methodParameters[i].ParameterType;

                            var obj = parameters[i];
                            array[i] = obj;
                            if (obj is string str && methodParameterType != typeof(string))
                            {
                                array[i] = ConvertValueTo(str, methodParameterType);
                            }
                        }

                        method.InvokeWithLog(viewModel, array);
                        return false;
                    }

                    if (method.GetParameters().Length == 0)
                    {
                        method.InvokeWithLog(viewModel, null);
                        return false;
                    }
                }
            }

            // continue original execution
            return true;
        }
    }
}