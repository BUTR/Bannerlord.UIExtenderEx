﻿using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.Patches;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.ResourceManager
{
    public static class WidgetFactoryManager
    {
        private static readonly AccessTools.FieldRef<WidgetFactory, IDictionary>? LiveCustomTypesFieldRef =
            AccessTools2.FieldRefAccess<WidgetFactory, IDictionary>("_liveCustomTypes");

        private static readonly Dictionary<string, Func<WidgetPrefab?>> CustomTypes = new();
        private static readonly Dictionary<string, Type> BuiltinTypes = new();
        private static readonly Dictionary<string, WidgetPrefab> LiveCustomTypes = new();
        private static readonly Dictionary<string, int> LiveInstanceTracker = new();

        public static WidgetPrefab Create(XmlDocument doc)
        {
            return WidgetPrefabPatch.LoadFromDocument(
                UIResourceManager.WidgetFactory.PrefabExtensionContext,
                UIResourceManager.WidgetFactory.WidgetAttributeContext,
                string.Empty,
                doc);
        }

        public static void Register(Type widgetType)
        {
            BuiltinTypes[widgetType.Name] = widgetType;
            WidgetInfo.ReLoad();
        }

        public static void Register(string name, Func<WidgetPrefab?> create)
        {
            CustomTypes[name] = create;
        }

        public static void CreateAndRegister(string name, XmlDocument xmlDocument) => Register(name, () => Create(xmlDocument));

        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(WidgetFactory), nameof(WidgetFactory.GetCustomType)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(WidgetFactoryManager), nameof(GetCustomTypePrefix))));

            harmony.Patch(
                AccessTools.Method(typeof(WidgetFactory), nameof(WidgetFactory.CreateBuiltinWidget)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(WidgetFactoryManager), nameof(CreateBuiltinWidgetPrefix))));

            harmony.Patch(
                AccessTools.Method(typeof(WidgetFactory), nameof(WidgetFactory.GetWidgetTypes)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(WidgetFactoryManager), nameof(GetWidgetTypesPostfix))));

            harmony.Patch(
                AccessTools.Method(typeof(WidgetFactory), nameof(WidgetFactory.IsCustomType)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(WidgetFactoryManager), nameof(IsCustomTypePrefix))));

            harmony.TryPatch(
                AccessTools.Method(typeof(WidgetFactory), "OnUnload"),
                prefix: AccessTools.Method(typeof(WidgetFactoryManager), nameof(OnUnloadPrefix)));

            // GetCustomType is too complex to be inlined
            // CreateBuiltinWidget is too complex to be inlined
            // GetWidgetTypes is not used?
            // Preventing inlining IsCustomType
            harmony.TryPatch(
                AccessTools.Method(typeof(WidgetTemplate), "CreateWidgets"),
                transpiler: AccessTools.Method(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
            harmony.TryPatch(
                AccessTools.Method(typeof(WidgetTemplate), "OnRelease"),
                transpiler: AccessTools.Method(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
            // Preventing inlining GetCustomType
            //harmony.Patch(
            //    AccessTools.Method(typeof(GauntletMovie), "LoadMovie"),
            //    transpiler: new HarmonyMethod(AccessTools.Method(typeof(WidgetFactoryManager), nameof(BlankTranspiler))));
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GetWidgetTypesPostfix(ref IEnumerable<string> __result)
        {
            __result = __result.Concat(BuiltinTypes.Keys).Concat(CustomTypes.Keys);
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CreateBuiltinWidgetPrefix(UIContext context, string typeName, ref Widget? __result)
        {
            if (!BuiltinTypes.TryGetValue(typeName, out var type))
                return true;

            __result = type.GetConstructor(AccessTools.all, null, new[] { typeof(UIContext) }, null)?.Invoke(new object[] { context }) as Widget;
            return false;
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsCustomTypePrefix(string typeName, ref bool __result)
        {
            if (!CustomTypes.ContainsKey(typeName))
            {
                return true;
            }

            __result = true;
            return false;
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GetCustomTypePrefix(WidgetFactory __instance, string typeName, ref WidgetPrefab __result)
        {
            // Post154
            if (LiveCustomTypesFieldRef is not null && LiveCustomTypesFieldRef(__instance).Contains(typeName))
            {
                return true;
            }
            // Post154

            if (!CustomTypes.ContainsKey(typeName))
            {
                return true;
            }

            if (LiveCustomTypes.TryGetValue(typeName, out var liveWidgetPrefab))
            {
                LiveInstanceTracker[typeName]++;
                __result = liveWidgetPrefab;
                return false;
            }

            if (CustomTypes[typeName]?.Invoke() is { } widgetPrefab)
            {
                LiveCustomTypes.Add(typeName, widgetPrefab);
                LiveInstanceTracker[typeName] = 1;

                __result = widgetPrefab;
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool OnUnloadPrefix(string typeName)
        {
            if (LiveCustomTypes.ContainsKey(typeName))
            {
                LiveInstanceTracker[typeName]--;
                if (LiveInstanceTracker[typeName] == 0)
                {
                    LiveCustomTypes.Remove(typeName);
                    LiveInstanceTracker.Remove(typeName);
                }
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<CodeInstruction> BlankTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;
    }
}