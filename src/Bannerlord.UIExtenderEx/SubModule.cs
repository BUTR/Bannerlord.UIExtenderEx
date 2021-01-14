using Bannerlord.BUTR.Shared.Helpers;

using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx
{
    public class SubModule : MBSubModuleBase
    {
        private const string SWarningTitle =
@"{=eySpdc25EE}Warning from Bannerlord.UIExtenderEx!";
        private const string SErrorHarmonyNotFound =
@"{=EEVJa5azpB}Bannerlord.Harmony module was not found!";
        private const string SErrorUIExtenderExNotFound =
@"{=YjsGP3mUaj}Bannerlord.UIExtenderEx module was not found!";
        private const string SErrorOfficialModulesLoadedBefore =
@"{=UZ8zfvudMs}UIExtenderEx is loaded after the official modules!
Make sure UIExtenderEx is loaded before them!";
        private const string SErrorOfficialModules =
@"{=F62r44tj2C}The following modules were loaded before UIExtenderEx:";
        private const string SMessageContinue =
@"{=eXs6FLm5DP}It's strongly recommended to terminate the game now. Do you wish to terminate it?";

        public SubModule()
        {
            CheckLoadOrder();
        }

        private static void CheckLoadOrder()
        {
            var loadedModules = ModuleUtils.GetLoadedModules().ToList();

            var sb = new StringBuilder();

            var harmonyModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.Harmony");
            var harmonyModuleIndex = harmonyModule is not null ? loadedModules.IndexOf(harmonyModule) : -1;
            if (harmonyModuleIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectUtils.Create(SErrorHarmonyNotFound)?.ToString() ?? "ERROR");
            }

            var uiExtenderModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.UIExtenderEx");
            var uiExtenderIndex = uiExtenderModule is not null ? loadedModules.IndexOf(uiExtenderModule) : -1;
            if (uiExtenderIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectUtils.Create(SErrorUIExtenderExNotFound)?.ToString() ?? "ERROR");
            }

            var officialModules = loadedModules.Where(x => x.IsOfficial).Select(x => (Module: x, Index: loadedModules.IndexOf(x)));
            var modulesLoadedBefore = officialModules.Where(tuple => tuple.Index < uiExtenderIndex).ToList();
            if (modulesLoadedBefore.Count > 0)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectUtils.Create(SErrorOfficialModulesLoadedBefore)?.ToString() ?? "ERROR");
                sb.AppendLine(TextObjectUtils.Create(SErrorOfficialModules)?.ToString() ?? "ERROR");
                foreach (var (module, _) in modulesLoadedBefore)
                    sb.AppendLine(module.Id);
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(TextObjectUtils.Create(SMessageContinue)?.ToString() ?? "ERROR");

                switch (MessageBox.Show(sb.ToString(), TextObjectUtils.Create(SWarningTitle)?.ToString() ?? "ERROR", MessageBoxButtons.YesNo))
                {
                    case DialogResult.Yes:
                        Environment.Exit(1);
                        break;
                }
            }
        }


    }
}