using System;
using System.Diagnostics;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx.GamePrefabs;

/// <summary>
/// Initializes the native game prefab preservation runtime during module load.
/// </summary>
public class SubModule : MBSubModuleBase
{
    /// <summary>
    /// Installs <see cref="GamePrefabRuntime"/> to evaluate pre-compiled Gauntlet prefab variants.
    /// </summary>
    public SubModule()
    {
        try
        {
            GamePrefabRuntime.Install();
        }
        catch (Exception e)
        {
            Trace.TraceError("UIExtenderEx: the game prefab runtime could not be installed, the game's pre-compiled prefabs are not used: {0}", e);
        }
    }
}