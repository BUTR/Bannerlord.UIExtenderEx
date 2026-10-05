using System.Diagnostics;

using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Utils;

internal static class MessageUtils
{
    public static void Fail(string text)
    {
        Trace.Fail(text);
        DisplayUserError(text);
    }

    public static void Assert(bool condition, string text = "no description")
    {
        Trace.Assert(condition, $"UIExtenderEx failure: {text}.");
    }

    /// <summary>
    /// Asserts runtime compatibility with the current Bannerlord version.
    /// </summary>
    /// <param name="condition">The condition that must be met.</param>
    /// <param name="text">Failure description text.</param>
    public static void CompatAssert(bool condition, string text = "no description")
    {
        Trace.Assert(condition, $"Bannerlord compatibility failure: {text}.");
    }

    /// <summary>
    /// Displays an error message to the player and writes to the trace log.
    /// </summary>
    /// <param name="text">The message or format template.</param>
    /// <param name="args">Format arguments.</param>
    public static void DisplayUserError(string text, params object[] args)
    {
        Trace.TraceError(text, args);
        InformationManager.DisplayMessage(new($"UIExtenderEx: {string.Format(text, args)}", Colors.Red));
    }

    /// <summary>
    /// Displays a warning message to the player and writes to the trace log.
    /// </summary>
    /// <param name="text">The message or format template.</param>
    /// <param name="args">Format arguments.</param>
    public static void DisplayUserWarning(string text, params object[] args)
    {
        Trace.TraceWarning(text, args);
        InformationManager.DisplayMessage(new($"UIExtender: {string.Format(text, args)}", Colors.Yellow));
    }
}