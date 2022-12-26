using Bannerlord.UIExtenderEx.Settings;

namespace Bannerlord.UIExtenderEx
{
    public class UIExtenderExSettings
    {
        public static UIExtenderExSettings Instance { get; } = new();

        private readonly ISettingsProvider _provider;
        public bool DumpXML { get => _provider.DumpXML; set => _provider.DumpXML = value; }

        private UIExtenderExSettings()
        {
            /*
            if (CustomSettings.Instance is not null)
            {
                _provider = CustomSettings.Instance;
            }
            else
            {
                _provider = new HardcodedCustomSettings();
            }
            */
            _provider = new SettingsSubModuleTags();
        }
    }
}