using Bannerlord.BUTR.Shared.Helpers;

using System;
using System.Windows.Forms;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx
{
    public class SubModule : MBSubModuleBase
    {
        // We can't rely on EN since the game assumes that the default locale is always English
        private const string SWarningTitle =
            @"{=eySpdc25EE}Warning from Bannerlord.UIExtenderEx!";
        private const string SMessageContinue =
            @"{=eXs6FLm5DP}It's strongly recommended to terminate the game now. Do you wish to terminate it?";

        public SubModule()
        {
            if (!ModuleInfoHelper.ValidateLoadOrder(typeof(SubModule), out var report))
            {
                report += $"{Environment.NewLine}{SMessageContinue}";
                switch (MessageBox.Show(report, TextObjectHelper.Create(SWarningTitle)?.ToString() ?? "ERROR", MessageBoxButtons.YesNo))
                {
                    case DialogResult.Yes:
                        Environment.Exit(1);
                        break;
                }
            }
        }
    }
}