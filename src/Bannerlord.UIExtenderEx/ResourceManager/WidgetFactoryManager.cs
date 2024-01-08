using Bannerlord.UIExtenderEx.Patches;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.ResourceManager;

public static class WidgetFactoryManager
{
    private delegate void ReloadDelegate();
    private static readonly ReloadDelegate? Reload =
        AccessTools2.GetDeclaredDelegate<ReloadDelegate>(typeof(WidgetInfo), "Reload");

    private static readonly AccessTools.FieldRef<WidgetFactory, IDictionary>? _liveCustomTypes =
        AccessTools2.FieldRefAccess<WidgetFactory, IDictionary>("_liveCustomTypes");

    private delegate Widget WidgetConstructor(UIContext uiContext);
    private static readonly ConcurrentDictionary<Type, WidgetConstructor?> WidgetConstructors = new();
    private static readonly Dictionary<string, Func<WidgetPrefab?>> CustomTypes = new();
    private static readonly Dictionary<string, Type> BuiltinTypes = new();
    private static readonly Dictionary<string, WidgetPrefab> LiveCustomTypes = new();
    private static readonly Dictionary<string, int> LiveInstanceTracker = new();

    public static WidgetPrefab? Create(XmlDocument doc)
    {
        return WidgetPrefabPatch.LoadFromDocument(
            UIResourceManager.WidgetFactory.PrefabExtensionContext,
            UIResourceManager.WidgetFactory.WidgetAttributeContext,
            string.Empty,
            doc);
    }
    public static WidgetPrefab? Create(string name, XmlDocument doc)
    {
        return WidgetPrefabPatch.LoadFromDocument(
            UIResourceManager.WidgetFactory.PrefabExtensionContext,
            UIResourceManager.WidgetFactory.WidgetAttributeContext,
            name,
            doc);
    }

    public static void Register(Type widgetType)
    {
        if (Reload is null) return;

        BuiltinTypes[widgetType.Name] = widgetType;
        Reload();
    }
    public static void Register(string name, Func<WidgetPrefab?> create) => CustomTypes.Add(name, create);
    public static void CreateAndRegister(string name, XmlDocument xmlDocument) => Register(name, () => Create($"{name}.xml", xmlDocument));

    public static void Patch(Harmony harmony)
    {
        harmony.Patch(
            AccessTools2.DeclaredMethod(typeof(WidgetFactory), "GetCustomType"),
            prefix: new HarmonyMethod(typeof(WidgetFactoryManager), nameof(GetCustomTypePrefix)));

        harmony.Patch(
            AccessTools2.DeclaredMethod(typeof(WidgetFactory), "CreateBuiltinWidget"),
            prefix: new HarmonyMethod(typeof(WidgetFactoryManager), nameof(CreateBuiltinWidgetPrefix)));

        harmony.Patch(
            AccessTools2.DeclaredMethod(typeof(WidgetFactory), "GetWidgetTypes"),
            prefix: new HarmonyMethod(typeof(WidgetFactoryManager), nameof(GetWidgetTypesPostfix)));

        harmony.Patch(
            AccessTools2.DeclaredMethod(typeof(WidgetFactory), "IsCustomType"),
            prefix: new HarmonyMethod(typeof(WidgetFactoryManager), nameof(IsCustomTypePrefix)));

#pragma warning disable BHA0001
        harmony.TryPatch(
            AccessTools2.DeclaredMethod(typeof(WidgetFactory), "OnUnload"),
            prefix: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(OnUnloadPrefix)));

        // GetCustomType is too complex to be inlined
        // CreateBuiltinWidget is too complex to be inlined
        // GetWidgetTypes is not used?
        // Preventing inlining IsCustomType
        harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:CreateWidgets"),
            transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
        harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:OnRelease"),
            transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
        // Preventing inlining GetCustomType
        harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.Data.GauntletMovie:LoadMovie"),
            transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
#pragma warning restore BHA0001
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
    private static bool CreateBuiltinWidgetPrefix(UIContext context, string typeName, ref object? __result)
    {
        if (!BuiltinTypes.TryGetValue(typeName, out var type))
            return true;

        var ctor = WidgetConstructors.GetOrAdd(type, static x => AccessTools2.GetDeclaredConstructorDelegate<WidgetConstructor>(x, new[] { typeof(UIContext) }));
        if (ctor is null)
            return true;

        __result = ctor(context);
        return false;
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsCustomTypePrefix(string typeName, ref bool __result)
    {
        if (!CustomTypes.ContainsKey(typeName))
            return true;

        __result = true;
        return false;
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GetCustomTypePrefix(WidgetFactory __instance, string typeName, ref WidgetPrefab __result)
    {
        if (_liveCustomTypes?.Invoke(__instance) is { } ____liveCustomTypes &&
            ____liveCustomTypes.Contains(typeName) || !CustomTypes.ContainsKey(typeName))
            return true;

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