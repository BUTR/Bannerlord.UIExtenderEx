using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using HarmonyLib;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace UIExtenderLib.CodePatcher.StaticLibrary
{
    /// <summary>
    /// Library of general transpilers and postfixes. Used in both CorePatches and ViewModelPatches.
    /// Most of those methods require additional positional argument `moduleName`, hence they're not usable
    /// in itself with Harmony. Instead they are used in generated assembly managed by `CodePatcherComponent`.
    /// </summary>
    public class UIExtenderPatchLib
    {
        /// <summary>
        /// Transpiler which replaces constructors of view models with their expanded counterparts.
        /// Only replaces constructors which are affected by current mod.
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static IEnumerable<CodeInstruction> InstantiationCallsiteTranspiler(string moduleName, IEnumerable<CodeInstruction> input)
        {
            return input.Select(op =>
            {
                var component = UIExtender.RuntimeFor(moduleName).ViewModelComponent;
                if (op.opcode != OpCodes.Newobj)
                {
                    return op;
                }
                
                var constructor = op.operand as ConstructorInfo;
                var type = constructor.DeclaringType;

                if (type == null || !type.IsSubclassOf(typeof(TaleWorlds.Library.ViewModel)))
                {
                    return op;
                }

                var baseType = component.BaseTypeForPossiblyExtendedType(type);
                if (baseType != null && component.ExtendsViewModelType(baseType))
                {
                    op.operand = component.ExtendedViewModelTypeForType(baseType, type).GetConstructor(constructor.GetParameters().Types());
                }

                return op;
            });
        }

        /// <summary>
        /// Postfix that is used to call `OnRefresh()` on attached mixins
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="__instance"></param>
        public static void RefreshPostfix(string moduleName, object __instance)
        {
            UIExtender.RuntimeFor(moduleName).ViewModelComponent.RefreshMixinForVMInstance(__instance);
        }
        
        /// <summary>
        /// Transpiler for `LoadFrom` method that apply patches to loaded XML file
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static IEnumerable<CodeInstruction> PrefabLoadTranspiler(string moduleName, IEnumerable<CodeInstruction> input)
        {
            var nopMarkName = "UIExtenderLib";
            
            var loadXmlMethod = typeof(UIExtenderRuntimeLib).GetMethod(nameof(UIExtenderRuntimeLib.LoadXmlDocument));
            var list = new List<CodeInstruction>(input);

            DynamicMethod CreateDynamicMethod(MethodInfo parentMethod)
            {
                var method = new DynamicMethod($"{Guid.NewGuid()}", null, new [] { typeof(string), typeof(string), typeof(XmlDocument) });
                var gen = method.GetILGenerator();

                if (parentMethod != null)
                {
                    gen.Emit(OpCodes.Ldarg_0);
                    gen.Emit(OpCodes.Ldarg_1);
                    gen.Emit(OpCodes.Ldarg_2);
                    gen.EmitCall(OpCodes.Call, parentMethod, null);
                }

                var processMethod = typeof(UIExtenderRuntimeLib).GetMethod(nameof(UIExtenderRuntimeLib.ProcessMovieDocumentIfNeeded));
                Debug.Assert(processMethod != null);
                gen.Emit(OpCodes.Ldstr, moduleName);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Ldarg_2);
                gen.EmitCall(OpCodes.Call, processMethod, null);
                gen.Emit(OpCodes.Ret);

                return method;
            }

            int FindAlreadyPatchedIndex()
            {
                for (var i = 0; i < list.Count(); i++)
                {
                    if (list[i].opcode == OpCodes.Nop && (string) list[i].operand == nopMarkName)
                    {
                        return i;
                    }
                }

                return -1;
            }

            void ApplyInitialPatch(DynamicMethod method)
            {
                var additions = new List<CodeInstruction>();

                additions.Add(new CodeInstruction(OpCodes.Ldstr, moduleName));
                additions.Add(new CodeInstruction(OpCodes.Ldarg_2));
                additions.Add(new CodeInstruction(OpCodes.Ldloc_0));
                additions.Add(new CodeInstruction(OpCodes.Call, loadXmlMethod));
                
                // @TODO: replace NOP with Label?
                additions.Add(new CodeInstruction(OpCodes.Nop, nopMarkName));
                additions.Add(new CodeInstruction(OpCodes.Ldstr, moduleName));
                additions.Add(new CodeInstruction(OpCodes.Ldarg_2));
                additions.Add(new CodeInstruction(OpCodes.Ldloc_0));
                additions.Add(new CodeInstruction(OpCodes.Call, method));

                // @TODO: reformat/rewrite
                var from = list.TakeWhile(i => !(i.opcode == OpCodes.Newobj && (i.operand as ConstructorInfo).DeclaringType == typeof(XmlReaderSettings))).Count() + 2;
                var to = from + list.Skip(from).TakeWhile(i => !(i.opcode == OpCodes.Newobj && (i.operand as ConstructorInfo).DeclaringType == typeof(WidgetPrefab))).Count();
                var count = to - from;

                list.RemoveRange(from, count);
                list.InsertRange(from, additions);

                from = from + additions.Count;
                count = to - from;

                if (Utils.SoftAssert(count > 0, "Don't have enought NOP space in the IL!"))
                {
                    list.InsertRange(from, Enumerable.Repeat(new CodeInstruction(OpCodes.Nop), count));
                }
                else
                {
                    UIExtender.RuntimeFor(moduleName).AddUserError($"Failed to patch {moduleName} (outdated).");
                }
            }

            var index = FindAlreadyPatchedIndex();
            if (index == -1)
            {
                var method = CreateDynamicMethod(null);
                ApplyInitialPatch(method);
            }
            else
            {
                var offset = list.Skip(index).TakeWhile(i => i.opcode != OpCodes.Call).Count();
                var instruction = list[index + offset];
                
                if (Utils.SoftAssert(instruction.opcode == OpCodes.Call, $"Invalid instruction found at marker!"))
                {
                    instruction.operand = CreateDynamicMethod((MethodInfo)instruction.operand);
                }
                else
                {
                    UIExtender.RuntimeFor(moduleName).AddUserError($"Failed to patch {moduleName} (outdated).");
                }
            }
            
            return list;
        }

        /// <summary>
        /// Transpiler which fixes `ViewModel.ExecuteCommand` method to not only look at top-level functions, but also recursively
        /// look in base classes.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static IEnumerable<CodeInstruction> ViewModelExecuteTranspiler(IEnumerable<CodeInstruction> input)
        {
            var replacedMethod = typeof(Type).GetMethod(nameof(Type.GetMethod), new Type[] {typeof(string), typeof(BindingFlags)});
            var targetMethod = typeof(UIExtenderRuntimeLib).GetMethod(nameof(UIExtenderRuntimeLib.FindExecuteCommandTargetMethod));
            
            var index = input.TakeWhile(i => !(i.opcode == OpCodes.Callvirt && (i.operand as MethodInfo) == replacedMethod)).Count();
            if (index >= input.Count())
            {
                // already patched
                return input;
            }
            
            var list = new List<CodeInstruction>(input);
            list[index].opcode = OpCodes.Call;
            list[index].operand = targetMethod;
            return list;
        }
    }
}