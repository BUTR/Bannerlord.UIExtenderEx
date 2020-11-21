using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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

        public SubModule()
        {
            CheckLoadOrder();
        }

        private static void CheckLoadOrder()
        {
            var loadedModules = GetLoadedModulesEnumerable().ToList();

            var sb = new StringBuilder();

            var harmonyModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.Harmony");
            var harmonyModuleIndex = harmonyModule is not null ? loadedModules.IndexOf(harmonyModule) : -1;
            if (harmonyModuleIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(new TextObject(SErrorHarmonyNotFound).ToString());
            }

            var uiExtenderModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.UIExtenderEx");
            var uiExtenderIndex = uiExtenderModule is not null ? loadedModules.IndexOf(uiExtenderModule) : -1;
            if (uiExtenderIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(new TextObject(SErrorUIExtenderExNotFound).ToString());
            }

            var officialModules = loadedModules.Where(x => x.IsOfficial).Select(x => (Module: x, Index: loadedModules.IndexOf(x)));
            var modulesLoadedBefore = officialModules.Where(tuple => tuple.Index < uiExtenderIndex).ToList();
            if (modulesLoadedBefore.Count > 0)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(new TextObject(SErrorOfficialModulesLoadedBefore).ToString());
                sb.AppendLine(new TextObject(SErrorOfficialModules).ToString());
                foreach (var (module, _) in modulesLoadedBefore)
                    sb.AppendLine(module.Id);
            }

            if (sb.Length > 0)
            {
                MessageBox.Show(sb.ToString(), new TextObject(SWarningTitle).ToString(), MessageBoxButtons.OK);
                Environment.Exit(1);
            }
        }

        private static IEnumerable<ModuleInfo> GetLoadedModulesEnumerable()
        {
            foreach (string modulesName in Utilities.GetModulesNames())
            {
                ModuleInfo moduleInfo = new ModuleInfo();
                moduleInfo.Load(modulesName);
                yield return moduleInfo;
            }
        }
    }
}