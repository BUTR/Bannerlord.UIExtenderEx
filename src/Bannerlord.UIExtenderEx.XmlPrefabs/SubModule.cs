using System;
using System.Diagnostics;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx.XmlPrefabs;

/// <summary>
/// Initializes XML loader patches during module load before downstream runtimes initialize.
/// </summary>
public class SubModule : MBSubModuleBase
{
    /// <summary>
    /// Installs <see cref="XmlPrefabRuntime"/> patches.
    /// </summary>
    public SubModule()
    {
        try
        {
            XmlPrefabRuntime.Install();
        }
        catch (Exception e)
        {
            Trace.TraceError("UIExtenderEx: the XML prefab runtime could not be installed, the game's XML loader is used unfixed: {0}", e);
        }
    }
}