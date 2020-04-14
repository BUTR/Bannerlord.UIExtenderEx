using TaleWorlds.GauntletUI.PrefabSystem;

namespace UIExtenderLib.CodePatcher.BuiltInPatches
{
    /// <summary>
    /// Set of patches that provide core functionality of UIExtender.
    /// If any of those fail UIExtender lib will not function at all.
    ///
    /// Two patches in total is required in order for the library to function:
    /// - Patch to `WidgetPrefab.LoadFrom`, which apply prefab XML extensions
    /// - Patch to `ViewModel.ExecuteCommand`, which fixes inheritance problems
    /// </summary>
    internal class CorePatches
    {
        internal static bool AddTo(CodePatcherComponent comp)
        {
            var widgetLoadMethod = typeof(WidgetPrefab).GetMethod(nameof(WidgetPrefab.LoadFrom));
            var executeCommandMethod = typeof(TaleWorlds.Library.ViewModel).GetMethod(nameof(TaleWorlds.Library.ViewModel.ExecuteCommand));
            
            if (widgetLoadMethod == null || executeCommandMethod == null)
            {
                return false;
            }
            
            comp.AddWidgetLoadPatch(widgetLoadMethod);
            comp.AddViewModelExecutePatch(executeCommandMethod);
            return true;
        }
    }
}