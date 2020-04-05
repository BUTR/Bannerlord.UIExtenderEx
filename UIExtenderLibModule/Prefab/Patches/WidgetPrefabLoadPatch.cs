using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace UIExtenderLibModule.Prefab.Patches
{
    [HarmonyPatch(typeof(WidgetPrefab), "LoadFrom")]
    internal class WidgetPrefabLoadFromHook
    {
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
        {
            var instructions = new List<CodeInstruction>(input);
            var additions = new List<CodeInstruction>();
            
            var getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
            var processIfNeeded = typeof(PrefabComponent).GetMethod(nameof(PrefabComponent.ProcessMovieDocumentIfNeeded));
            
            additions.Add(new CodeInstruction(OpCodes.Ldtoken, typeof(PrefabComponent)));
            additions.Add(new CodeInstruction(OpCodes.Call, getTypeFromHandle));
            additions.Add(new CodeInstruction(OpCodes.Ldarg_2));
            additions.Add(new CodeInstruction(OpCodes.Ldloc_0));
            additions.Add(new CodeInstruction(OpCodes.Call, processIfNeeded));
            additions.Add(new CodeInstruction(OpCodes.Pop));

            var xmlSettingsCtor = "System.Xml.XmlReaderSettings..ctor()";
            var widgetPrefabCtor = "TaleWorlds.GauntletUI.PrefabSystem.WidgetPrefab..ctor()";

            var from = input.TakeWhile(i => !(i.opcode == OpCodes.Newobj && (i.operand as ConstructorInfo).FullDescription() == xmlSettingsCtor)).Count() + 2;
            var to = from + input.Skip(from).TakeWhile(i => !(i.opcode == OpCodes.Newobj && (i.operand as ConstructorInfo).FullDescription() == widgetPrefabCtor)).Count();
            var count = to - from;

            instructions.RemoveRange(from, count);
            instructions.InsertRange(from, additions);

            from = from + additions.Count;
            count = to - from;
            
            Debug.Assert(count > 0, "Don't have enough NOP space in the IL!'");
            instructions.InsertRange(from, Enumerable.Repeat(new CodeInstruction(OpCodes.Nop), count));
            return instructions;
        }
    }
}