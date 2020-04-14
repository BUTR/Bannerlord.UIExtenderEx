using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Principal;
using System.Xml;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.PrefabSystem;
using UIExtenderLib.CodePatcher.StaticLibrary;
using UIExtenderLib.PatchAssembly;
using UIExtenderLib.ViewModel;

namespace UIExtenderLib.CodePatcher
{
    /// <summary>
    /// Component that deals with code patching using Harmony.
    /// Adds static patches to PatchAssemblyBuilder later passing them to Harmony.
    /// </summary>
    internal class CodePatcherComponent
    {
        private readonly Harmony _harmony;
        private readonly UIExtenderRuntime _runtime;
        private readonly CodePatchesAssemblyBuilder _patchesAssemblyBuilder;
        
        private readonly Dictionary<string, MethodBase> _transpilers = new Dictionary<string, MethodBase>();
        private readonly Dictionary<string, MethodBase> _postfixes = new Dictionary<string, MethodBase>();

        internal CodePatcherComponent(UIExtenderRuntime runtime)
        {
            // check runtime harmony version
            CheckHarmonyVersion();
            
            _harmony = new Harmony("net.uiextenderlib." + runtime.ModuleName);
            _runtime = runtime;
            _patchesAssemblyBuilder = new CodePatchesAssemblyBuilder(runtime.ModuleName);
        }
        
        /// <summary>
        /// Adds Harmony patch running InstantiationCallsiteTranspiler from PatchLibrary,
        /// which swaps original view model constructors with extended ones.
        /// Doesn't do actual patching, which is done in `Apply()` method.
        /// </summary>
        /// <param name="callsite"></param>
        internal void AddViewModelInstantiationPatch(MethodBase callsite)
        {
            var patchName = nameof(UIExtenderPatchLib.InstantiationCallsiteTranspiler);
            var name = patchName + Guid.NewGuid().ToString();
            _patchesAssemblyBuilder.AddTranspiler(name, GetPatchLibMethod(patchName), new object[] {_runtime.ModuleName});
            _transpilers[name] = callsite;
        }

        /// <summary>
        /// Adds Harmony patch running RefreshPostfix from PatchLibrary, which appends mixin refresh code to view model refresh methods.
        /// Doesn't do actual patching, which is done in `Apply()` method.
        /// </summary>
        /// <param name="callsite"></param>
        internal void AddViewModelRefreshPatch(MethodBase callsite)
        {
            var patchName = nameof(UIExtenderPatchLib.RefreshPostfix);
            var name = patchName + Guid.NewGuid().ToString();
            
            _patchesAssemblyBuilder.AddPostfix(
                name, 
                GetPatchLibMethod(patchName), 
                new object[] {_runtime.ModuleName}
            );
            _postfixes[name] = callsite;
        }

        /// <summary>
        /// Adds Harmony patch running PrefabLoadTranspiler from PatchLib, patching `WidgetFactory.LoadFrom` in order for it
        /// to load extensions.
        /// Doesn't do actual patching, which is done in `Apply()` method.
        /// </summary>
        /// <param name="callsite"></param>
        internal void AddWidgetLoadPatch(MethodBase callsite)
        {
            var patchName = nameof(UIExtenderPatchLib.PrefabLoadTranspiler);
            var name = patchName + Guid.NewGuid().ToString();
            
            _patchesAssemblyBuilder.AddTranspiler(name, GetPatchLibMethod(patchName), new object[] {_runtime.ModuleName});
            _transpilers[name] = callsite;
        }

        /// <summary>
        /// Adds Harmony patch running ViewModelExecuteTranspiler from PatchLib, fixing it's lookup issues brought by inheritance.
        /// Doesn't do actual patching, which is done in `Apply()` method.
        /// </summary>
        /// <param name="executeCommandMethod"></param>
        internal void AddViewModelExecutePatch(MethodBase callsite)
        {
            var patchName = nameof(UIExtenderPatchLib.ViewModelExecuteTranspiler);
            var name = patchName + Guid.NewGuid().ToString();
            _patchesAssemblyBuilder.AddTranspiler(name, GetPatchLibMethod(patchName));
            _transpilers[name] = callsite;
        }

        /// <summary>
        /// Apply added patches (actually patch game code).
        /// After this call underlying assembly will be finalized and `AddX` methods can no longer be used.
        /// </summary>
        internal void ApplyPatches()
        {
            // finalize assembly builder and load static library class from it
            var staticLibType = _patchesAssemblyBuilder.SaveAndLoadLibraryType();

            foreach (var kv in _transpilers)
            {
                var method = staticLibType.GetMethod(kv.Key);
                _harmony.Patch(kv.Value, transpiler: new HarmonyMethod(method));
            }

            foreach (var kv in _postfixes)
            {
                var method = staticLibType.GetMethod(kv.Key);
                _harmony.Patch(kv.Value, postfix: new HarmonyMethod(method));
            }
        }

        /// <summary>
        /// Helper method to find MethodInfo of specified patch from PatchLib
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private MethodInfo GetPatchLibMethod(string name)
        {
            return typeof(UIExtenderPatchLib).GetMethod(name, BindingFlags.Static | BindingFlags.Public);
        }

        /// <summary>
        /// Check loaded Harmony version.
        /// Bannerlord only loads first dll it can find without regards to it's version, meaning that the library can
        /// end up with outdated harmony at runtime.
        /// </summary>
        private void CheckHarmonyVersion()
        {
            var version = typeof(Harmony).Assembly.ImageRuntimeVersion;
            var majorVersion = int.Parse("" + version.Skip(1).ElementAt(0));
            if (majorVersion < 2)
            {
                Debug.Fail($"Loaded Harmony version {version} is not supported by UIExtenderLib.\n"+
                           "You need to get rid of modules using Harmony versions < 2.0.0 since they're not compatible.\n"+
                           "See https://github.com/shdwp/UIExtenderLib/wiki/Mismatched-Harmony-versions.");
            }
        }
    }
}