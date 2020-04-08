using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Library;

namespace UIExtenderLibModule.ViewModel
{
	public static class ViewModelPatchUtil
	{
		/**
		 * Generalized transpiler which will replace standard view models with their extended counterparts.
		 * Should be applied to methods that call constructors of view models after extensions have been registered.
		 */
		internal static IEnumerable<CodeInstruction> TranspilerForVMInstantiation(IEnumerable<CodeInstruction> input)
		{
			return input.Select(op =>
			{
				var component = UIExtenderLibModule.SharedInstance.ViewModelComponent;
				if (op.opcode == OpCodes.Newobj)
				{
					var constructor = op.operand as ConstructorInfo;
					if (constructor.DeclaringType.IsSubclassOf(typeof(TaleWorlds.Library.ViewModel)) && component.ExtendsViewModelType(constructor.DeclaringType))
					{
						op.operand = component.ExtendedViewModelForType(constructor.DeclaringType).GetConstructor(constructor.GetParameters().Types());
					}
				}

				return op;
			});
		}

		public static object GetMixinInstanceForType(Type t)
		{
			return UIExtenderLibModule.SharedInstance.ViewModelComponent.MixinInstanceForType(t);
		}

		public static void InitializeMixinsForModel(Type t, object instance)
		{
			UIExtenderLibModule.SharedInstance.ViewModelComponent.InitializeMixinsForViewModelInstance(t, instance);
		}

		public static MethodInfo FindExecuteCommandMethod(Type t, string name, BindingFlags flags)
		{
			if (t == null)
			{
				return null;
			}
			
			var method = t.GetMethod(name, flags | BindingFlags.FlattenHierarchy);
			return method != null ? method : FindExecuteCommandMethod(t.BaseType, name, flags);
		}
	}
}