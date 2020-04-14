using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using UIExtenderLib.Interface;
using Debug = System.Diagnostics.Debug;

namespace UIExtenderLib
{
    /// <summary>
    /// Client class instance of which should be created for each module using this library
    /// </summary>
    public class UIExtender
    {
        /// <summary>
        /// Cache or runtime objects that will be accessed from patched code
        /// </summary>
        /// 
        private static readonly Dictionary<string, UIExtenderRuntime> RuntimeInstances = new Dictionary<string, UIExtenderRuntime>();
        
        /// <summary>
        /// Name of the module this instance is assigned to
        /// </summary>
        private string _moduleName;
        
        /// <summary>
        /// Runtime instance of this extender
        /// </summary>
        private UIExtenderRuntime _runtime;

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
        public void Register()
        {
            Register(Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Register extension types from specified assembly
        /// Should be called during `OnSubModuleLoad`, called by `Register`
        /// </summary>
        /// <param name="assembly"></param>
        public void Register(Assembly assembly)
        {
            CheckNotCompatibleVersions();
            
            var types = assembly
                .GetTypes()
                .Where(t => t.CustomAttributes.Any(a => a.AttributeType.IsSubclassOf(typeof(UIExtenderLibExtension))));

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

        /// <summary>
        /// Verify registration and emit user messages if needed
        /// Required to be called during `OnBeforeInitialScreenSetAsRoot`
        /// </summary>
        public void Verify()
        {
            if (Utils.SoftAssert(_runtime != null, $"Verify() called before Register()!"))
            {
                _runtime.Verify();
            }
        }

        /// <summary>
        /// Get specified module runtime from the cache. Called from patches
        /// </summary>
        /// <param name="moduleName">name of the module that produced this patch</param>
        /// <returns></returns>
        internal static UIExtenderRuntime RuntimeFor(string moduleName)
        {
            return RuntimeInstances[moduleName];
        }

        /// <summary>
        /// Check for previous not compatible versions of UIExtenderLib
        /// </summary>
        private void CheckNotCompatibleVersions()
        {
            var mods = Utilities.GetModulesNames();
            if (mods.Contains("0UIExtenderLibModule") || mods.Contains("UIExtenderLibModule"))
            {
                Debug.Fail($"UIExtender version >= 2 is not supported with UIExtenderLibModule.\n" +
                           $"Please disable UIExtenderLibModule.\n\n" +
                           $"Modules doesn't need to use it anymore and should be updated to newer version.'");
            }
        }
    }
}