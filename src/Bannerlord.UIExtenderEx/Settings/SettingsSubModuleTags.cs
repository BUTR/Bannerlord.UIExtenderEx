using System;

namespace Bannerlord.UIExtenderEx.Settings;

/// <summary>
/// The 2.x settings, read from the UIExtenderEx SubModule's tags. Kept so mods compiled against 2.x still load; the
/// settings now live in the <c>&lt;Settings&gt;</c> block of <c>SubModule.xml</c>, and this forwards to them.
/// </summary>
[Obsolete("Settings are declared in the <Settings> block of SubModule.xml now. Use UIExtenderExSettings.Instance.")]
public class SettingsSubModuleTags : ISettingsProvider
{
    public bool DumpXML { get => UIExtenderExSettings.Instance.DumpXML; set => UIExtenderExSettings.Instance.DumpXML = value; }
}