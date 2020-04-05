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

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
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