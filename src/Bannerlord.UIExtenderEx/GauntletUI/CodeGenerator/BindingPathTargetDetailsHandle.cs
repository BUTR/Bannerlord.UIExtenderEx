using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;
using System.Linq;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator
{
    internal readonly struct BindingPathTargetDetailsHandle
    {
        private delegate BindingPath GetBindingPathDelegate(object instance);
        private static readonly GetBindingPathDelegate? GetBindingPath;

        private delegate IEnumerable<object> GetChildrenDelegate(object instance);
        private static readonly GetChildrenDelegate? GetChildren;

        private delegate object GetParentDelegate(object instance);
        private static readonly GetParentDelegate? GetParent;

        static BindingPathTargetDetailsHandle()
        {
            GetBindingPath = AccessTools2.GetDelegateObjectInstance<GetBindingPathDelegate>(
                AccessTools.PropertyGetter(AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.BindingPathTargetDetails"), "BindingPath"));

            GetChildren = AccessTools2.GetDelegateObjectInstance<GetChildrenDelegate>(
                AccessTools.PropertyGetter(AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.BindingPathTargetDetails"), "Children"));

            GetParent = AccessTools2.GetDelegateObjectInstance<GetParentDelegate>(
                AccessTools.PropertyGetter(AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.BindingPathTargetDetails"), "Parent"));
        }

        public static BindingPathTargetDetailsHandle FromExisting(object bindingPathTargetDetails) => new(bindingPathTargetDetails);

        private readonly object _bindingPathTargetDetails;

        private BindingPathTargetDetailsHandle(object generatedPrefabContext) => _bindingPathTargetDetails = generatedPrefabContext;

        public List<BindingPathTargetDetailsHandle> Children => GetChildren is null
            ? new List<BindingPathTargetDetailsHandle>()
            : GetChildren(_bindingPathTargetDetails).Select(FromExisting).ToList();

        public BindingPath? BindingPath => GetBindingPath is null
            ? null
            : GetBindingPath(_bindingPathTargetDetails);

        public BindingPathTargetDetailsHandle? Parent => GetParent is null
            ? null
            : GetParent(_bindingPathTargetDetails) is { } parent
                ? new BindingPathTargetDetailsHandle(parent)
                : null;
    }
}