using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;

namespace Bannerlord.UIExtenderEx.ResourceManager;

public static class BrushFactoryManager
{
    private static readonly Dictionary<string, Brush> CustomBrushes = new();

    private delegate Brush LoadBrushFromDelegate(object instance, XmlNode brushNode);

    private static readonly LoadBrushFromDelegate? LoadBrushFrom =
        AccessTools2.GetDeclaredDelegate<LoadBrushFromDelegate>(typeof(BrushFactory), "LoadBrushFrom");

    public static IEnumerable<Brush> Create(XmlDocument xmlDocument)
    {
        foreach (XmlNode brushNode in xmlDocument.SelectSingleNode("Brushes")!.ChildNodes)
        {
            var brush = LoadBrushFrom?.Invoke(UIResourceManager.BrushFactory, brushNode);
            if (brush is not null)
            {
                yield return brush;
            }
        }
    }

    public static void Register(IEnumerable<Brush> brushes)
    {
        foreach (var brush in brushes)
        {
            CustomBrushes[brush.Name] = brush;
        }
    }

    public static void CreateAndRegister(XmlDocument xmlDocument) => Register(Create(xmlDocument));

    internal static void Patch(Harmony harmony)
    {
        harmony.Patch(
            AccessTools2.DeclaredPropertyGetter(typeof(BrushFactory), "Brushes"),
            postfix: new HarmonyMethod(typeof(BrushFactoryManager), nameof(GetBrushesPostfix)));

        harmony.Patch(
            AccessTools2.DeclaredMethod(typeof(BrushFactory), "GetBrush"),
            prefix: new HarmonyMethod(typeof(BrushFactoryManager), nameof(GetBrushPrefix)));

#pragma warning disable BHA0001
        var blankTranspiler = AccessTools2.DeclaredMethod(typeof(BrushFactoryManager), nameof(BlankTranspiler));

        // Preventing inlining GetBrush
        TryPatchIfFound(harmony, "TaleWorlds.GauntletUI.PrefabSystem.ConstantDefinition:GetValue", blankTranspiler);
        TryPatchIfFound(harmony, "TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions:SetWidgetAttributeFromString", blankTranspiler);
        TryPatchIfFound(harmony, "TaleWorlds.GauntletUI.UIContext:GetBrush", blankTranspiler);
        TryPatchIfFound(harmony, "TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions:ConvertObject", blankTranspiler);
        TryPatchIfFound(harmony, "TaleWorlds.MountAndBlade.GauntletUI.Widgets.BoolBrushChangerBrushWidget:OnBooleanUpdated", blankTranspiler);
        // Preventing inlining GetBrush
#pragma warning restore BHA0001
    }

    private static void TryPatchIfFound(Harmony harmony, string typeColonName, System.Reflection.MethodInfo? transpiler)
    {
        if (transpiler is not null && AccessTools2.DeclaredMethod(typeColonName, null, null, false) is { } method)
        {
            harmony.TryPatch(method, transpiler: transpiler);
        }
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GetBrushesPostfix(ref IEnumerable<Brush> __result)
    {
        __result = __result.Concat(CustomBrushes.Values);
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GetBrushPrefix(string name, IReadOnlyDictionary<string, Brush> ____brushes, ref Brush __result)
    {
        if (____brushes.ContainsKey(name) || !CustomBrushes.ContainsKey(name))
        {
            return true;
        }

        if (CustomBrushes[name] is { } brush)
        {
            __result = brush;
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> BlankTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;
}