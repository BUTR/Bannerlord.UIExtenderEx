using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.ResourceManager;

/// <summary>
/// Manages runtime registration of custom <see cref="Brush"/> instances across all active <see cref="BrushFactory"/> objects.
/// <para>
/// Registers brushes directly into the native factory lookup dictionary to prevent JIT inlining bypasses on <c>GetBrush</c>.
/// Injects brushes into newly instantiated factories and handles cache invalidation across <see cref="BrushFactory.LoadBrushes"/> reloads.
/// </para>
/// </summary>
public static class BrushFactoryManager
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, Brush> CustomBrushes = new();

    // Tracks created factories so subsequent brush registrations populate existing instances.
    private static readonly List<WeakReference<BrushFactory>> Factories = [];

    // Identifies brushes injected by UIExtenderEx to permit overwriting during re-registration without corrupting native brushes.
    private static readonly ConditionalWeakTable<Brush, object> Registered = new();

    private static readonly AccessTools.FieldRef<BrushFactory, Dictionary<string, Brush>>? GetBrushes =
        AccessTools2.FieldRefAccess<BrushFactory, Dictionary<string, Brush>>("_brushes");

    private delegate Brush LoadBrushFromDelegate(object instance, XmlNode brushNode);

    private static readonly LoadBrushFromDelegate? LoadBrushFrom =
        AccessTools2.GetDeclaredDelegate<LoadBrushFromDelegate>("TaleWorlds.GauntletUI.BrushFactory:LoadBrushFrom");

    public static IEnumerable<Brush> Create(XmlDocument xmlDocument)
    {
        if (xmlDocument.SelectSingleNode("Brushes") is not { } brushesNode)
        {
            yield break;
        }

        foreach (XmlNode brushNode in brushesNode.ChildNodes)
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
        lock (Lock)
        {
            foreach (var brush in brushes)
            {
                CustomBrushes[brush.Name] = brush;
                Registered.Remove(brush);
                Registered.Add(brush, null!);
            }

            Factories.RemoveAll(x => !x.TryGetTarget(out _));
            foreach (var reference in Factories)
            {
                if (reference.TryGetTarget(out var factory))
                    AddTo(factory);
            }
        }
    }

    public static void CreateAndRegister(XmlDocument xmlDocument) => Register(Create(xmlDocument));

    internal static void Patch(Harmony harmony)
    {
        var constructed = harmony.TryPatch(
            AccessTools2.DeclaredConstructor("TaleWorlds.GauntletUI.BrushFactory", new Type[] { typeof(ResourceDepot), typeof(string), typeof(SpriteData), typeof(FontFactory) }),
            postfix: AccessTools2.DeclaredMethod(typeof(BrushFactoryManager), nameof(ConstructorPostfix)));
        var reloaded = harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.BrushFactory:LoadBrushes"),
            postfix: AccessTools2.DeclaredMethod(typeof(BrushFactoryManager), nameof(LoadBrushesPostfix)));

        if (!constructed || !reloaded)
            MessageUtils.DisplayUserWarning("Failed to patch BrushFactory! Screen elements that mods add may look unstyled or wrong.");
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConstructorPostfix(BrushFactory __instance)
    {
        lock (Lock)
        {
            Factories.Add(new(__instance));
            AddTo(__instance);
        }
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadBrushesPostfix(BrushFactory __instance)
    {
        lock (Lock)
        {
            AddTo(__instance);
        }
    }

    // Native brushes take precedence over colliding names; previously injected custom brushes are updated in place.
    private static void AddTo(BrushFactory factory)
    {
        if (GetBrushes?.Invoke(factory) is not { } brushes)
            return;

        foreach (var pair in CustomBrushes)
        {
            if (!brushes.TryGetValue(pair.Key, out var existing) || Registered.TryGetValue(existing, out _))
                brushes[pair.Key] = pair.Value;
        }
    }
}