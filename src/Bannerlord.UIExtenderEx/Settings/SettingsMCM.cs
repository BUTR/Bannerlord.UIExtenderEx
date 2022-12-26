/*
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

using TaleWorlds.Localization;

namespace Bannerlord.UIExtenderEx.Settings
{
    internal sealed class Settings : AttributeGlobalSettings<Settings>, ISettingsProvider
    {
        public override string Id => "UIExtenderEx_v1";
        public override string FolderName => "UIExtenderEx";
        public override string FormatType => "json2";
        public override string DisplayName => new TextObject("{=y8RwnbIN}UIExtenderEx {VERSION}", new()
        {
            { "VERSION", typeof(Settings).Assembly.GetName().Version?.ToString(3) ?? "ERROR" }
        }).ToString();

        /// <summary>
        /// When set to true, patches that are loaded from file (<see cref="PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute"/>)
        /// will be reloaded every time their target view is reloaded.<br/>
        /// This is slower, so should only be enabled while in a development environment.
        /// </summary>
        //[SettingPropertyGroup("{=ODH5APqM}Debug")]
        //[SettingPropertyBool("{=6UPFzw9M}Reload UI", HintText = "{=s4czNYpu}Whether to reload from file the prefab patch after each.", RequireRestart = false)]
        //public bool ReloadUI { get; set; } = false;

        [SettingPropertyGroup("{=ODH5APqM}Debug")]
        [SettingPropertyBool("{=FP3G0cfi}Dump XML", HintText = "{=rBkPEIL7}Whether to dump the resulting XML prefab after a patch.", RequireRestart = false)]
        public bool DumpXML { get; set; } = false;
    }
}
*/