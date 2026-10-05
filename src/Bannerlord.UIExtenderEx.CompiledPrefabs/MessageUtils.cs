using System.Diagnostics;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>Provides diagnostic messaging utilities to log warnings and display user-facing notifications.</summary>
internal static class MessageUtils
{
    /// <summary>Logs a diagnostic trace warning and presents a formatted warning message in the in-game notification log.</summary>
    public static void DisplayUserWarning(string text, params object[] args)
    {
        Trace.TraceWarning(text, args);
        InformationManager.DisplayMessage(new($"UIExtender: {string.Format(text, args)}", Colors.Yellow));
    }
}