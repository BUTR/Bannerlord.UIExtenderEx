using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace UIExtenderLibModule.ViewModel.Patches
{
    /**
     * Patch for ViewModel.ExecuteCommand which will make it look up method in base classes as well.
     * Default implementation only looks at top object, which doesn't work with extended view model classes.
     */
    [HarmonyPatch(typeof(TaleWorlds.Library.ViewModel), "ExecuteCommand")]
    public class ViewModelExecuteCommandPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
        {
            var replacedMethod = typeof(Type).GetMethod(nameof(Type.GetMethod), new Type[] {typeof(string), typeof(BindingFlags)});
            var targetMethod = typeof(ViewModelPatchUtil).GetMethod(nameof(ViewModelPatchUtil.FindExecuteCommandMethod));
            
            var index = input.TakeWhile(i => !(i.opcode == OpCodes.Callvirt && (i.operand as MethodInfo) == replacedMethod)).Count();
            Utils.CompatiblityAssert(index < input.Count(), $"Failed to find GetType() call in ViewModel.ExecuteCommand!");
            
            var list = new List<CodeInstruction>(input);
            list[index].opcode = OpCodes.Call;
            list[index].operand = targetMethod;
            return list;
        }
    }
}