using System;
using System.Xml;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

using UIExtenderLib;
using UIExtenderLib.Prefab;
using UIExtenderLibModule.Prefab;
using UIExtenderLibModule.ViewModel;

using Debug = System.Diagnostics.Debug;

namespace UIExtenderLibModule
{
    public class UIExtenderLibModule: MBSubModuleBase
    {
        // set up when module is loaded. used to retrieve component instances
        internal static UIExtenderLibModule SharedInstance;
        
        // widget component instance
        internal readonly PrefabComponent WidgetComponent = new PrefabComponent();
        
        // view model component instance
        internal readonly ViewModelComponent ViewModelComponent = new ViewModelComponent();

        // flag to check for single process call
        private static bool _processedExtensions = false;

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            /*
             * This method is run every time MainMenu appears, which could happen multiple times
             * during single application run.
             * 
             * Code to apply extensions should be run after all of the mods has been loaded, therefore it could not
             * be placed in `OnSubModuleLoad`.
             *
             * In order to only run it once this flag is implemented.
             */
            if (_processedExtensions)
            {
                return;
            }
            _processedExtensions = true;
            
            // apply registered extensions
            foreach (var extensionType in UIExtender.Extensions)
            {
                var baseAttribute = Attribute.GetCustomAttribute(extensionType, typeof(UIExtenderLibExtension));
                if (baseAttribute is PrefabExtension xmlExtension)
                {
                    var constructor = extensionType.GetConstructor(new Type[] { });
                    if (constructor == null)
                    {
                        Debug.Fail("Failed to find appropriate constructor for patch!");
                    }
                    
                    // gauntlet xml extension
                    switch (constructor.Invoke(new object[]{}))
                    {
                        case PrefabExtensionInsertPatch patch:
                            WidgetComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                            break;
                        
                        case PrefabExtensionReplacePatch patch:
                            WidgetComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                            break;
                        
                        case PrefabExtensionInsertAsSiblingPatch patch:
                            WidgetComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                            break;
                        
                        case CustomPatch<XmlDocument> patch:
                            WidgetComponent.RegisterPatch(xmlExtension.Movie, patch.Apply);
                            break;
                        
                        case CustomPatch<XmlNode> patch:
                            WidgetComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch.Apply);
                            break;
                        
                        default:
                            Debug.Fail($"Patch class is unsupported - {extensionType}!");
                            break;
                    }
                } else if (baseAttribute is ViewModelMixin) {
                    // view model mixin
                    ViewModelComponent.RegisterViewModelMixin(extensionType);
                }
                else
                {
                    Debug.Fail($"Failed to find appropriate clause for base type {extensionType} with attribute {baseAttribute}!");
                }
            }
            
            // apply patches
            var harmony = new Harmony("net.shdwp.UIExtenderLibModule");
            harmony.PatchAll();

            // save .runtime_dll for troubleshooting
            ViewModelComponent.SaveRuntimeImages();
            
            // force reload movies that should be patched by extensions
            WidgetComponent.ForceReloadMovies();
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            SharedInstance = this;
            
            // find prefab extension files
            WidgetComponent.FindPrefabExtensions();
        }
    }

}