using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Patches;

using HarmonyLib;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Debug = System.Diagnostics.Debug;

namespace Bannerlord.UIExtenderEx
{
    /// <summary>
    /// Client class instance of which should be created for each module using this library
    /// </summary>
    public class UIExtender
    {
        private static readonly Harmony _harmony = new Harmony("bannerlord.uiextender.ex");

        static UIExtender()
        {
            _harmony.Patch(
                WidgetFactoryPatch.TargetMethod(),
                prefix: new HarmonyMethod(typeof(WidgetFactoryPatch), nameof(WidgetFactoryPatch.InitializePrefix)));
        }
        
        /// <summary>
        /// Cache or runtime objects that will be accessed from patched code
        /// </summary>
        /// 
        private static readonly Dictionary<string, UIExtenderRuntime> RuntimeInstances = new Dictionary<string, UIExtenderRuntime>();
        
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
        /// Register extension types from calling assembly.
        /// Should be called during `OnSubModuleLoad`
        /// </summary>
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
            if (_runtime == null)
            {
                Debug.Fail("Register() method was not called before Enable()!");
                return;
            }
            _runtime.Enable();
        }

        public void Disable()
        {
            if (_runtime == null)
            {
                Debug.Fail("Register() method was not called before Enable()!");
                return;
            }
            _runtime.Disable();
        }

        /// <summary>
        /// Get specified module runtime from the cache. Called from patches
        /// </summary>
        /// <param name="moduleName">name of the module that produced this patch</param>
        /// <returns></returns>
        internal static UIExtenderRuntime RuntimeFor(string moduleName) => RuntimeInstances[moduleName];
    }
}