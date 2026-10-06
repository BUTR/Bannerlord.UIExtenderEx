using Bannerlord.BUTR.Shared.Helpers;

using Bannerlord.UIExtenderEx.Runtimes;

using BUTR.MessageBoxPInvoke.Helpers;

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx;

public class SubModule : MBSubModuleBase
{
    static SubModule()
    {
        // Fallback: disables native pre-compiled prefab resolution if requested via DisableGeneratedPrefabs.
        // Synchronized with UIConfig.DoNotUseGeneratedPrefabs via UIConfigPatch.
        if (UIExtenderExSettings.Instance.DisableGeneratedPrefabs)
        {
            // Disable pre-compiled prefabs as early as possible.
            try
            {
                // Force load TaleWorlds.Engine.GauntletUI to ensure UIConfig is available.
                System.Reflection.Assembly.Load("TaleWorlds.Engine.GauntletUI");
            }
            catch (Exception e)
            {
                Utils.MessageUtils.Fail($"Failed to load 'TaleWorlds.Engine.GauntletUI'! Exception: {e}");
            }

            TaleWorlds.Engine.GauntletUI.UIConfig.DoNotUseGeneratedPrefabs = true;
        }
    }

    // We can't rely on EN since the game assumes that the default locale is always English
    private const string SWarningTitle =
        @"{=eySpdc25EE}Warning from Bannerlord.UIExtenderEx!";
    private const string SMessageContinue =
        @"{=eXs6FLm5DP}It's strongly recommended to terminate the game now. Do you wish to terminate it?";

    public SubModule()
    {
        // The game loads the prefab runtimes' DLLs after constructing this, and crashes on one Windows refuses to load
        try
        {
            RuntimeSubModules.RejectUnloadable(typeof(SubModule));
        }
        catch (Exception e)
        {
            Trace.TraceError("UIExtenderEx: the prefab runtime DLLs could not be checked, one that cannot be loaded crashes the game: {0}", e);
        }

        ValidateLoadOrder();
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        if (!UIExtenderExSettings.Instance.DisableGeneratedPrefabs)
        {
            // Execute UIExtender's static constructor eagerly to initialize core Harmony patches.
            // Ensures movie patches are installed before dependent runtimes initialize during OnSubModuleLoad.
            try
            {
                RuntimeHelpers.RunClassConstructor(typeof(UIExtender).TypeHandle);
            }
            catch (Exception e)
            {
                Utils.MessageUtils.DisplayUserError("Failed to apply UIExtenderEx patches! Exception: {0}", e);
            }
        }
    }

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        base.OnBeforeInitialModuleScreenSetAsRoot();
        RuntimeSubModules.ReportRejected();
    }

    private static void ValidateLoadOrder()
    {
        var loadedModules = ModuleInfoHelper.GetLoadedModules().ToList();
        if (loadedModules.Count == 0) return;

        var sb = new StringBuilder();
        if (!ModuleInfoHelper.ValidateLoadOrder(typeof(SubModule), out var report))
        {
            sb.AppendLine(report);
            sb.AppendLine();
            sb.AppendLine(new TextObject(SMessageContinue)?.ToString() ?? "ERROR");
            switch (MessageBoxDialog.Show(sb.ToString(), new TextObject(SWarningTitle)?.ToString() ?? "ERROR", MessageBoxButtons.YesNo))
            {
                case MessageBoxResult.Yes:
                    Environment.Exit(1);
                    break;
            }
        }
    }
}