namespace Bannerlord.UIExtenderEx.Runtimes;

/// <summary>
/// Provides access to prefab runtime settings persisted in the module's <c>&lt;Settings&gt;</c> declaration block
/// within <c>SubModule.xml</c>.
/// </summary>
public static class RuntimeSettings
{
    /// <summary>
    /// Retrieves the current boolean value for the specified setting key, returning <paramref name="defaultValue"/>
    /// if <c>SubModule.xml</c> does not define the key or cannot be parsed. File changes are detected and reloaded
    /// dynamically during runtime.
    /// </summary>
    public static bool Get(string key, bool defaultValue) => UIExtenderExSettings.Instance.Store.Get(key, defaultValue);

    /// <summary>
    /// Updates the specified setting key, persisting the change to <c>SubModule.xml</c> if declared, or maintaining
    /// it in-memory for the current session if the file cannot be updated.
    /// </summary>
    public static void Set(string key, bool value) => UIExtenderExSettings.Instance.Store.Set(key, value);
}