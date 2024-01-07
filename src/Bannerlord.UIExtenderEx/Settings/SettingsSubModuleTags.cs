using Bannerlord.BUTR.Shared.Helpers;

using System;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Settings;

public class SettingsSubModuleTags : ISettingsProvider
{
    public bool DumpXML { get; set; } = true;

    public SettingsSubModuleTags()
    {
        try
        {
            if (ModuleInfoHelper.GetModuleByType(typeof(SettingsSubModuleTags)) is not { } module) return;
            if (module.SubModules.FirstOrDefault(x => x.Name == "UIExtenderEx") is not { } subModule) return;
            DumpXML = subModule.Tags.TryGetValue(nameof(DumpXML), out var dumpXmlVal) && bool.TryParse(dumpXmlVal.FirstOrDefault(), out var dumpXml) && dumpXml;

        }
        catch (Exception) { /* ignore */ }
    }
}