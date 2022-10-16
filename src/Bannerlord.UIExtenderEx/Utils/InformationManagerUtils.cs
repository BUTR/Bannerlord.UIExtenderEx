using HarmonyLib.BUTR.Extensions;

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.Utils
{
    internal static class InformationManagerUtils
    {
        private delegate void DisplayMessageV1Delegate(object data);
        private static readonly DisplayMessageV1Delegate? DisplayMessageV1;

        static InformationManagerUtils()
        {
            var type = AccessTools2.TypeByName("TaleWorlds.Core.InformationManager") ??
                       AccessTools2.TypeByName("TaleWorlds.Library.InformationManager");

            foreach (var methodInfo in HarmonyLib.AccessTools.GetDeclaredMethods(type).Where(x => x.Name == "DisplayMessage"))
            {
                var @params = methodInfo.GetParameters();
                if (@params.Length == 1 && @params[0].ParameterType.Name.Equals("InformationMessage", StringComparison.Ordinal))
                {
                    DisplayMessageV1 = AccessTools2.GetDelegate<DisplayMessageV1Delegate>(methodInfo);
                }
                else
                {
                    Debug.Fail("DisplayMessage not found!");
                }
            }
        }

        public static void DisplayMessage(InformationMessageWrapper? message)
        {
            if (message is null)
            {
                return;
            }

            DisplayMessageV1?.Invoke(message.Object);
        }
    }
}