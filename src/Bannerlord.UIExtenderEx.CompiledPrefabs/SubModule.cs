using System;
using System.Diagnostics;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Initializes the compiled prefab runtime submodule during game launch.
/// </summary>
public class SubModule : MBSubModuleBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubModule"/> class and registers the compiled prefab runtime.
    /// If runtime installation fails, unhandled errors are logged and execution falls back to XML prefab interpretation.
    /// </summary>
    public SubModule()
    {
        try
        {
            CompiledPrefabRuntime.Install();
        }
        catch (Exception e)
        {
            Trace.TraceError("UIExtenderEx: the compiled prefab runtime could not be installed, patched movies load from XML: {0}", e);
        }
    }

    /// <summary>
    /// Executes submodule loading tasks and initiates background compiler warm-up.
    /// </summary>
    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        CompiledPrefabRuntime.WarmUp();
    }

    private bool _mainMenuSet;

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        base.OnBeforeInitialModuleScreenSetAsRoot();
        _mainMenuSet = true;
    }

    /// <summary>
    /// Executes on the first frame tick following main menu initialization to trigger diagnostic XML prefab exports when enabled.
    /// </summary>
    protected override void OnApplicationTick(float dt)
    {
        base.OnApplicationTick(dt);
        if (!_mainMenuSet)
            return;
        _mainMenuSet = false;
        try
        {
            PrefabXmlDump.LoadAll(TaleWorlds.Engine.GauntletUI.UIResourceManager.WidgetFactory);
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: the prefab dump could not load every prefab: {0}", e.Message);
        }
    }
}