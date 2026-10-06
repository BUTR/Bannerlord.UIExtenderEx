using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace Bannerlord.UIExtenderEx.Runtimes;

/// <summary>
/// Stops a prefab runtime DLL that cannot be loaded from crashing the game.
/// <para>
/// The XML, game and compiled prefab runtimes are submodules of their own, and each is optional: without one, movies load
/// the way they would without it. The game does not treat them as optional. When Windows refuses a DLL (Smart App Control,
/// or another Application Control policy, blocks an unsigned file it has no reputation for: HRESULT 0x800711C7),
/// <c>AssemblyLoader.LoadFrom</c> shows "Cannot load" and returns <see langword="null"/>. From v1.4.0
/// <c>Module.HandleSubmoduleLoadError</c> then shows a second message box and throws. Up to v1.3.15
/// <c>Module.AddSubModule</c> gets the <see langword="null"/> assembly and throws a <see cref="NullReferenceException"/>.
/// </para>
/// <para>
/// The game constructs the core's <c>SubModule</c> before it loads the next submodule's DLL, and it checks each submodule's
/// tags first: <c>Module.CheckIfSubmoduleCanBeLoadable</c> skips one with a <c>RejectedPlatform</c> tag for the current
/// platform, the same way it skips a platform-specific submodule. So the core loads each runtime DLL itself, and gives the
/// one that fails that tag. The game skips that DLL without a message box, and the failure is shown in the main menu.
/// The core's own DLL and other modules' DLLs fail the way the game makes them fail.
/// </para>
/// </summary>
internal static class RuntimeSubModules
{
    /// <summary>The runtime submodules by their <c>SubModuleClassType</c>, with what the game does without each.</summary>
    private static readonly Dictionary<string, string> Runtimes = new()
    {
        ["Bannerlord.UIExtenderEx.XmlPrefabs.SubModule"] = "the game's XML loader is used unfixed",
        ["Bannerlord.UIExtenderEx.GamePrefabs.SubModule"] = "the game's pre-compiled prefabs are not used",
        ["Bannerlord.UIExtenderEx.CompiledPrefabs.SubModule"] = "patched movies load from XML",
    };

    private static readonly List<(string Dll, string Consequence, string Reason)> Rejected = [];

    /// <summary>The DLLs of the runtime submodules that could not be loaded, in load order.</summary>
    internal static IEnumerable<string> RejectedDlls => Rejected.Select(x => x.Dll);

    /// <summary>Rejects the runtime submodules of the module the core is loaded from whose DLL cannot be loaded.</summary>
    public static void RejectUnloadable(Type coreSubModule)
    {
        var module = ModuleHelper.GetModules().FirstOrDefault(x => x.SubModules.Any(y => y.SubModuleClassTypeName == coreSubModule.FullName));
        if (module is null)
        {
            Trace.TraceWarning("UIExtenderEx: the module of {0} was not found, a prefab runtime DLL that cannot be loaded crashes the game", coreSubModule.FullName);
            return;
        }

        // The folder the game loads submodule DLLs from (Module.LoadSubModules)
        RejectUnloadable(module.SubModules, Path.Combine(module.FolderPath, "bin", Common.ConfigName));
    }

    internal static void RejectUnloadable(IEnumerable<SubModuleInfo> subModules, string binFolder)
    {
        foreach (var subModule in subModules)
        {
            if (!Runtimes.TryGetValue(subModule.SubModuleClassTypeName, out var consequence))
                continue;

            // A missing DLL is the game's "Cannot find" message box, which does not stop it
            var path = Path.Combine(binFolder, subModule.DLLName);
            if (!File.Exists(path))
                continue;

            try
            {
                // The game loads it the same way next, and gets this assembly
                Assembly.LoadFrom(path);
            }
            catch (Exception e)
            {
                subModule.Tags.Add(new(SubModuleInfo.SubModuleTags.RejectedPlatform, ApplicationPlatform.CurrentPlatform.ToString()));
                Rejected.Add((subModule.DLLName, consequence, e.Message));
                Trace.TraceError("UIExtenderEx: {0} could not be loaded and is left out, {1}: {2}", subModule.DLLName, consequence, e);
            }
        }
    }

    /// <summary>Displays the runtimes left out, once the main menu can show messages.</summary>
    public static void ReportRejected()
    {
        foreach (var (dll, consequence, reason) in Rejected)
            Utils.MessageUtils.DisplayUserWarning("{0} could not be loaded and {1}. Windows may have blocked it: {2}", dll, consequence, reason);
    }
}
