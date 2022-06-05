using Bannerlord.BUTR.Shared.Helpers;

using HarmonyLib.BUTR.Extensions;

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx
{
    public class SubModule : MBSubModuleBase
    {
        private delegate void SetDoNotUseGeneratedPrefabsDelegate(bool value);

        static SubModule()
        {
            // Disable AutoGens as early as possible
            try
            {
                // Force load TaleWorlds.Engine.GauntletUI as it might not be loaded yet!
                Assembly.Load("TaleWorlds.Engine.GauntletUI");
            }
            catch (Exception e)
            {
                Utils.Fail($"Failed to load 'TaleWorlds.Engine.GauntletUI'! Exception: {e}");
            }
            var setDoNotUseGeneratedPrefabs = AccessTools2.GetPropertySetterDelegate<SetDoNotUseGeneratedPrefabsDelegate>("TaleWorlds.Engine.GauntletUI.UIConfig:DoNotUseGeneratedPrefabs");
            if (setDoNotUseGeneratedPrefabs is not null)
                setDoNotUseGeneratedPrefabs(true);
            else
                Utils.Fail("Failed to find 'DoNotUseGeneratedPrefabs'!");

        }

        // We can't rely on EN since the game assumes that the default locale is always English
        private const string SWarningTitle =
            @"{=eySpdc25EE}Warning from Bannerlord.UIExtenderEx!";
        private const string SMessageContinue =
            @"{=eXs6FLm5DP}It's strongly recommended to terminate the game now. Do you wish to terminate it?";

        public SubModule()
        {
            CheckLoadOrder();
        }

        private static void CheckLoadOrder()
        {
            var loadedModules = ModuleInfoHelper.GetLoadedModules().ToList();
            if (loadedModules.Count == 0) return;

            var sb = new StringBuilder();
            if (!ModuleInfoHelper.ValidateLoadOrder(typeof(SubModule), out var report))
            {
                sb.AppendLine(report);
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

        /// <inheritdoc />
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            //UIPatchSubModule.OnBeforeInitialModuleScreenSetAsRoot();
        }
    }
}