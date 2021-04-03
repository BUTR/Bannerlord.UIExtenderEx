using Bannerlord.BUTR.Shared.Helpers;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx
{
    public class SubModule : MBSubModuleBase
    {
        private delegate void SetDoNotUseGeneratedPrefabs(bool value);

        // We can't rely on EN since the game assumes that the default locale is always English
        private const string SErrorHarmonyNotFound =
@"{=EEVJa5azpB}Bannerlord.Harmony module was not found!";
        private const string SMessageContinue =
@"{=eXs6FLm5DP}It's strongly recommended to terminate the game now. Do you wish to terminate it?";
        private const string SWarningTitle =
@"{=eySpdc25EE}Warning from Bannerlord.UIExtenderEx!";
        private const string SErrorUIExtenderExNotFound =
@"{=YjsGP3mUaj}Bannerlord.UIExtenderEx module was not found!";
        private const string SErrorOfficialModulesLoadedBefore =
@"{=UZ8zfvudMs}UIExtenderEx is loaded after the official modules!
Make sure UIExtenderEx is loaded before them!";
        private const string SErrorOfficialModules =
@"{=F62r44tj2C}The following modules were loaded before UIExtenderEx:";

        private static Type _ignore = typeof(UIResourceManager); // trigger assembly loading

        public SubModule()
        {
            CheckLoadOrder();
        }

        private static void CheckLoadOrder()
        {
            var loadedModules = ModuleInfoHelper.GetLoadedModules().ToList();

            var sb = new StringBuilder();

            var harmonyModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.Harmony");
            var harmonyModuleIndex = harmonyModule is not null ? loadedModules.IndexOf(harmonyModule) : -1;
            if (harmonyModuleIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create(SErrorHarmonyNotFound)?.ToString() ?? "ERROR");
            }

            var uiExtenderModule = loadedModules.SingleOrDefault(x => x.Id == "Bannerlord.UIExtenderEx");
            var uiExtenderIndex = uiExtenderModule is not null ? loadedModules.IndexOf(uiExtenderModule) : -1;
            if (uiExtenderIndex == -1)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create(SErrorUIExtenderExNotFound)?.ToString() ?? "ERROR");
            }

            var officialModules = loadedModules.Where(x => x.IsOfficial).Select(x => (Module: x, Index: loadedModules.IndexOf(x)));
            var modulesLoadedBefore = officialModules.Where(tuple => tuple.Index < uiExtenderIndex).ToList();
            if (modulesLoadedBefore.Count > 0)
            {
                if (sb.Length != 0) sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create(SErrorOfficialModulesLoadedBefore)?.ToString() ?? "ERROR");
                sb.AppendLine(TextObjectHelper.Create(SErrorOfficialModules)?.ToString() ?? "ERROR");
                foreach (var (module, _) in modulesLoadedBefore)
                    sb.AppendLine(module.Id);
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine(TextObjectHelper.Create(SMessageContinue)?.ToString() ?? "ERROR");

                switch (MessageBox.Show(sb.ToString(), TextObjectHelper.Create(SWarningTitle)?.ToString() ?? "ERROR", MessageBoxButtons.YesNo))
                {
                    case DialogResult.Yes:
                        Environment.Exit(1);
                        break;
                }
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            var gv = ApplicationVersionHelper.GameVersion() ?? ApplicationVersion.Empty;
            if (gv.Major == 1 && gv.Minor == 5 && gv.Revision >= 9 || gv.Major == 1 && gv.Minor > 5)
            {
                DisableAutoGens();
            }
        }

        private static void DisableAutoGens()
        {
            var type = AccessTools.TypeByName("TaleWorlds.Engine.GauntletUI.UIConfig");
            var property = AccessTools.Property(type, "DoNotUseGeneratedPrefabs");
            var setter = AccessTools2.GetDelegate<SetDoNotUseGeneratedPrefabs>(property?.SetMethod);
            setter?.Invoke(true);
        }
    }
}