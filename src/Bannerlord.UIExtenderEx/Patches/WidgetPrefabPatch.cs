using Bannerlord.UIExtenderEx.Runtimes;
using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Patches;

internal static class WidgetPrefabPatch
{
    /// <summary>
    /// Indicates whether <see cref="LoadFromDocumentCore"/> carries the reverse-patched implementation of <see cref="WidgetPrefab.LoadFrom"/>.
    /// Until reverse patching succeeds, its body remains a stand-in placeholder.
    /// </summary>
    private static bool _loadFromDocumentAvailable;

    public static void Patch(Harmony harmony)
    {
        if (!harmony.TryPatch(
                AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetPrefab:LoadFrom"),
                transpiler: AccessTools2.DeclaredMethod(typeof(WidgetPrefabPatch), nameof(WidgetPrefab_LoadFrom_Transpiler))))
        {
            MessageUtils.DisplayUserWarning("Failed to patch WidgetPrefab.LoadFrom! Changes mods make to the game's screens will not appear.");
        }

        _loadFromDocumentAvailable = harmony.TryCreateReversePatcher(
                AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetPrefab:LoadFrom"),
                AccessTools2.DeclaredMethod(typeof(WidgetPrefabPatch), nameof(LoadFromDocumentCore)),
                out var reversePatcher) && TryPatch(reversePatcher!);
        if (!_loadFromDocumentAvailable)
            MessageUtils.DisplayUserWarning("Failed to reverse patch WidgetPrefab.LoadFrom! Screen elements that mods create will not appear.");
    }

    private static bool TryPatch(ReversePatcher reversePatcher)
    {
        try
        {
            reversePatcher.Patch();
            return true;
        }
        catch (Exception e)
        {
            Trace.TraceError("UIExtenderEx: reverse patching WidgetPrefab.LoadFrom failed: {0}", e);
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> WidgetPrefab_LoadFrom_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        var instructionsList = instructions.ToList();

        [MethodImpl(MethodImplOptions.NoInlining)]
        IEnumerable<CodeInstruction> ReturnDefault(string place)
        {
            MessageUtils.DisplayUserWarning("Failed to patch WidgetPrefab.LoadFrom ({0})! Changes mods make to the game's screens will not appear.", place);
            return instructionsList.AsEnumerable();
        }

        if (AccessTools2.DeclaredConstructor("TaleWorlds.GauntletUI.PrefabSystem.WidgetPrefab") is not { } constructor)
            return ReturnDefault("WidgetPrefab constructor not found");

        if (AccessTools2.DeclaredMethod(typeof(WidgetPrefabPatch), nameof(ProcessMovie)) is not { } processMovieMethod)
            return ReturnDefault("WidgetPrefabPatch:ProcessMovie not found");

        // Locate target members dynamically: the document is the only XmlDocument local variable, and path is the only string parameter.
        if (method.GetMethodBody()?.LocalVariables.FirstOrDefault(x => x.LocalType == typeof(XmlDocument)) is not { } documentLocal)
            return ReturnDefault("XmlDocument local not found");

        var pathParameter = method.GetParameters().FirstOrDefault(x => x.ParameterType == typeof(string));
        if (pathParameter is null)
            return ReturnDefault("Path parameter not found");

        var startIndex = -1;
        for (var i = 0; i < instructionsList.Count - 2; i++)
        {
            if (instructionsList[i + 0].opcode != OpCodes.Newobj || !Equals(instructionsList[i + 0].operand, constructor))
                continue;

            if (!instructionsList[i + 1].IsStloc())
                continue;

            startIndex = i;
            break;
        }

        if (startIndex == -1)
        {
            return ReturnDefault("Pattern not found");
        }

        // Duplicate the instantiated prefab on the evaluation stack for the subsequent stloc instruction.
        // Because LoadFrom is static, a parameter's position corresponds directly to its argument index.
        instructionsList.InsertRange(startIndex + 1, new List<CodeInstruction>
        {
            new(OpCodes.Dup),
            CodeInstruction.LoadArgument(pathParameter.Position),
            CodeInstruction.LoadLocal(documentLocal.LocalIndex),
            new(OpCodes.Call, processMovieMethod)
        });
        return instructionsList.AsEnumerable();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProcessMovie(WidgetPrefab prefab, string path, XmlDocument document)
    {
        var movieName = Path.GetFileNameWithoutExtension(path);
        ApplyPatches(movieName, document);

        // Broadcast the parsed document (whether patched or unmodified) to all registered prefab runtimes.
        PrefabSource.RaiseParsed(prefab, movieName, document);
    }

    private static void ApplyPatches(string movieName, XmlDocument document)
    {
        foreach (var runtime in UIExtender.GetAllRuntimes())
        {
            runtime.PrefabComponent.ProcessMovieIfNeeded(movieName, document);
        }
    }

    public static WidgetPrefab? LoadFromDocument(PrefabExtensionContext prefabExtensionContext, WidgetAttributeContext widgetAttributeContext, string path, XmlDocument document)
    {
        if (!_loadFromDocumentAvailable)
            return null;

        // The reverse patch invokes vanilla WidgetPrefab.LoadFrom without transpiler injections.
        // Apply prefab patches directly here so documents loaded from memory receive equivalent modifications.
        // Patches apply to a cloned document copy to preserve caller reentrancy (e.g., CreateAndRegister reloads).
        var movieName = Path.GetFileNameWithoutExtension(path);
        if (UIExtender.GetAllRuntimes().Any(x => x.PrefabComponent.HasEnabledPatches(movieName)))
        {
            document = (XmlDocument) document.CloneNode(true);
            ApplyPatches(movieName, document);
        }

        var prefab = LoadFromDocumentCore(prefabExtensionContext, widgetAttributeContext, path, document);
        if (prefab is not null)
            PrefabSource.RaiseParsed(prefab, movieName, document);
        return prefab;
    }

    /// <summary>
    /// Stand-in method replaced by Harmony's reverse patcher with the native <see cref="WidgetPrefab.LoadFrom"/> implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WidgetPrefab? LoadFromDocumentCore(PrefabExtensionContext prefabExtensionContext, WidgetAttributeContext widgetAttributeContext, string path, XmlDocument document)
    {
        // Replaces XML file stream loading with assigning the in-memory 'document' argument into the local variable.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var returnNull = new List<CodeInstruction>
            {
                new (OpCodes.Ldnull),
                new (OpCodes.Ret)
            }.AsEnumerable();

            [MethodImpl(MethodImplOptions.NoInlining)]
            IEnumerable<CodeInstruction> ReturnDefault(string place)
            {
                MessageUtils.DisplayUserWarning("Failed to reverse patch WidgetPrefab.LoadFrom ({0})! Screen elements that mods create will not appear.", place);
                return returnNull;
            }

            if (AccessTools2.DeclaredConstructor("TaleWorlds.GauntletUI.PrefabSystem.WidgetPrefab") is not { } constructor)
                return ReturnDefault("WidgetPrefab constructor not found");

            var instructionList = instructions.ToList();

            var locals = AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetPrefab:LoadFrom")?.GetMethodBody()?.LocalVariables;
            if (locals?.FirstOrDefault(x => x.LocalType == typeof(XmlDocument)) is not { } documentLocal)
                return ReturnDefault("XmlDocument local not found");

            var constructorIndex = -1;
            for (var i = 0; i < instructionList.Count; i++)
            {
                if (instructionList[i].opcode == OpCodes.Newobj && Equals(instructionList[i].operand, constructor))
                    constructorIndex = i;
            }

            // Requires at least two leading instructions before constructor invocation to store the document argument.
            if (constructorIndex < 2)
                return ReturnDefault("WidgetPrefab construction not found");

            // Instructions preceding the constructor read the file stream into the local XmlDocument.
            // Replace these instructions with NOP instructions to remove reader instantiation and disposal while preserving
            // branching labels targeted by subsequent blocks.
            for (var i = 0; i < constructorIndex; i++)
                instructionList[i] = new(OpCodes.Nop);

            // Stand-in parameter layout: prefabExtensionContext (0), widgetAttributeContext (1), path (2), document (3).
            instructionList[0] = CodeInstruction.LoadArgument(3);
            instructionList[1] = CodeInstruction.StoreLocal(documentLocal.LocalIndex);

            return instructionList.AsEnumerable();
        }

        // Harmony locates the reverse-patch transpiler via this method reference.
        _ = Transpiler([]);

        // Stand-in placeholder body replaced by Harmony when applying the reverse patch.
        // It must perform no side effects if evaluated before patching completes.
        _ = prefabExtensionContext;
        _ = widgetAttributeContext;
        _ = path;
        _ = document;
        return null;
    }
}