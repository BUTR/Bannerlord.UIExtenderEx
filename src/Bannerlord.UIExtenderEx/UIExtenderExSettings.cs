using Bannerlord.UIExtenderEx.Settings;

namespace Bannerlord.UIExtenderEx;

public class UIExtenderExSettings
{
    public static UIExtenderExSettings Instance { get; } = new();

    /// <summary>The module's one settings store, shared with the prefab runtimes (<see cref="Runtimes.RuntimeSettings"/>).</summary>
    internal SettingsSubModuleXml Store { get; } = new();

    public bool DumpXML { get => Store.DumpXML; set => Store.DumpXML = value; }
    public bool DisableGeneratedPrefabs { get => Store.DisableGeneratedPrefabs; set => Store.DisableGeneratedPrefabs = value; }

    private UIExtenderExSettings() { }
}