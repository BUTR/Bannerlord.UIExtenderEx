using System.Diagnostics;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Utils
{
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
        /// Critical runtime compatibility assert. Used when Bannerlord version is not compatible and it
        /// prevents runtime from functioning
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="text"></param>
        public static void CompatAssert(bool condition, string text = "no description")
        {
            Trace.Assert(condition, $"Bannerlord compatibility failure: {text}.");
        }

        /// <summary>
        /// Display error message to the end user
        /// </summary>
        /// <param name="text"></param>
        /// <param name="args"></param>
        public static void DisplayUserError(string text, params object[] args)
        {
            Trace.TraceError(text, args);
            InformationManagerUtils.DisplayMessage(InformationMessageUtils.Create($"UIExtenderEx: {string.Format(text, args)}", Colors.Red));
        }

        /// <summary>
        /// Display warning message to the end user
        /// </summary>
        /// <param name="text"></param>
        /// <param name="args"></param>
        public static void DisplayUserWarning(string text, params object[] args)
        {
            Trace.TraceWarning(text, args);
            InformationManagerUtils.DisplayMessage(InformationMessageUtils.Create($"UIExtender: {string.Format(text, args)}", Colors.Yellow));
        }
    }
}