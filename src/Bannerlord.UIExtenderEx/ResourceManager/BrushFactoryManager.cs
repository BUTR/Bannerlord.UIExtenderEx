using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

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
    public static class BrushFactoryManager
    {
        private static readonly Dictionary<string, Brush> CustomBrushes = new();

        private delegate Brush LoadBrushFromDelegate(BrushFactory instance, XmlNode brushNode);

        private static readonly LoadBrushFromDelegate? LoadBrushFrom =
            AccessTools2.GetDelegate<LoadBrushFromDelegate>(typeof(BrushFactory), "LoadBrushFrom");

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
                AccessTools.PropertyGetter(typeof(BrushFactory), nameof(BrushFactory.Brushes)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(BrushFactoryManager), nameof(GetBrushesPostfix))));

            harmony.Patch(
                AccessTools.Method(typeof(BrushFactory), nameof(BrushFactory.GetBrush)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(BrushFactoryManager), nameof(GetBrushPrefix))));

            // Preventing inlining GetBrush
            harmony.TryPatch(
                AccessTools.Method(typeof(ConstantDefinition), "GetValue"),
                transpiler: AccessTools.Method(typeof(BrushFactoryManager), nameof(BlankTranspiler)));
            harmony.TryPatch(
                AccessTools.Method(typeof(WidgetExtensions), "SetWidgetAttributeFromString"),
                transpiler: AccessTools.Method(typeof(BrushFactoryManager), nameof(BlankTranspiler)));
            harmony.TryPatch(
                AccessTools.Method(typeof(UIContext), "GetBrush"),
                transpiler: AccessTools.Method(typeof(BrushFactoryManager), nameof(BlankTranspiler)));
            harmony.TryPatch(
                AccessTools.Method(typeof(WidgetExtensions), "ConvertObject"),
                transpiler: AccessTools.Method(typeof(BrushFactoryManager), nameof(BlankTranspiler)));
            //harmony.Patch(
            //    AccessTools.Method(typeof(BoolBrushChanger), "OnBooleanUpdated"),
            //    transpiler: new HarmonyMethod(AccessTools.Method(typeof(BrushFactoryManager), nameof(BlankTranspiler))));
            // Preventing inlining GetBrush
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
        private static bool GetBrushPrefix(string name, Dictionary<string, Brush> ____brushes, ref Brush __result)
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
}