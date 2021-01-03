using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;

using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Bannerlord.UIExtenderEx
{
    /// <summary>
    /// Client class instance of which should be created for each module using this library
    /// </summary>
    public class UIExtender
    {
        private static readonly Harmony Harmony = new("bannerlord.uiextender.ex");

        static UIExtender()
        {
            ViewModelPatch.Patch(Harmony);
            WidgetPrefabPatch.Patch(Harmony);
            WidgetFactoryPatch.Patch(Harmony);
            BrushFactoryManager.Patch(Harmony);
            WidgetFactoryManager.Patch(Harmony);
        }

        /// <summary>
        /// Cache or runtime objects that will be accessed from patched code
        /// </summary>
        ///
        private static readonly Dictionary<string, UIExtenderRuntime> RuntimeInstances = new();

        /// <summary>
        /// Name of the module this instance is assigned to
        /// </summary>
        private readonly string _moduleName;

        /// <summary>
        /// Runtime instance of this extender
        /// </summary>
        private UIExtenderRuntime? _runtime;

        /// <summary>
        /// Default constructor. `moduleName` should match module folder because it will be used to look-up resources
        /// </summary>
        /// <param name="moduleName">Module name, should match module folder</param>
        public UIExtender(string moduleName)
        {
            _moduleName = moduleName;
        }

        /// <summary>
        /// Obsolete. Use <see cref="Register(Assembly)"/>.
        /// </summary>
        [Obsolete("Use explicit call Register(Assembly)", true)]
        public void Register() => Register(Assembly.GetCallingAssembly());

        /// <summary>
        /// Register extension types from specified assembly
        /// Should be called during `OnSubModuleLoad`, called by `Register`
        /// </summary>
        /// <param name="assembly"></param>
        public void Register(Assembly assembly)
        {
            var types = assembly
                .GetTypes()
                .Where(t => t.CustomAttributes.Any(a => a.AttributeType.IsSubclassOf(typeof(BaseUIExtenderAttribute))));

            if (RuntimeInstances.ContainsKey(_moduleName))
            {
                Utils.DisplayUserError($"Failed to load extension module {_moduleName} - already loaded!");
                return;
            }

            var runtime = new UIExtenderRuntime(_moduleName);
            _runtime = runtime;
            RuntimeInstances[_moduleName] = runtime;

            runtime.Register(types);
        }

        public void Enable()
        {
            if (_runtime is null)
            {
                Utils.Fail("Register() method was not called before Enable()!");
                return;
            }
            _runtime.Enable();
        }

        public void Disable()
        {
            if (_runtime is null)
            {
                Utils.Fail("Register() method was not called before Enable()!");
                return;
            }
            _runtime.Disable();
        }

        internal static UIExtenderRuntime GetRuntimeFor(string moduleName) => RuntimeInstances[moduleName];

        internal static IReadOnlyList<UIExtenderRuntime> GetAllRuntimes() => RuntimeInstances.Values.ToList();
    }
}