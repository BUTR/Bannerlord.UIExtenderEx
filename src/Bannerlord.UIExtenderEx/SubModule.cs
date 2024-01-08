using Bannerlord.BUTR.Shared.Helpers;
using BUTR.MessageBoxPInvoke.Helpers;

using System;
using System.Linq;
using System.Text;

using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx;

public class SubModule : MBSubModuleBase
{
    // We can't rely on EN since the game assumes that the default locale is always English
    private const string SWarningTitle =
        @"{=eySpdc25EE}Warning from Bannerlord.UIExtenderEx!";
    private const string SMessageContinue =
        @"{=eXs6FLm5DP}It's strongly recommended to terminate the game now. Do you wish to terminate it?";

    public SubModule()
    {
        ValidateLoadOrder();
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