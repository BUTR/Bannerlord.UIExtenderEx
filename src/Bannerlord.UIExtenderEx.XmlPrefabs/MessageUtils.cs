using System.Diagnostics;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.XmlPrefabs;

/// <summary>Displays formatted warning messages to the player and trace log.</summary>
internal static class MessageUtils
{
    /// <summary>
    /// Displays a warning message to the player and writes to the trace log.
    /// </summary>
    public static void DisplayUserWarning(string text, params object[] args)
    {
        Trace.TraceWarning(text, args);
        InformationManager.DisplayMessage(new($"UIExtender: {string.Format(text, args)}", Colors.Yellow));
    }
}