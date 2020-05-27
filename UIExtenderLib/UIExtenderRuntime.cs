using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Library;
using UIExtenderLib.CodePatcher;
using UIExtenderLib.CodePatcher.BuiltInPatches;
using UIExtenderLib.Interface;
using UIExtenderLib.Prefab;
using UIExtenderLib.ViewModel;
using Debug = System.Diagnostics.Debug;

namespace UIExtenderLib
{
    /// <summary>
    /// Actual runtime of UIExtender, assigned to each module's instance of `UIExtender`
    /// </summary>
    internal class UIExtenderRuntime
    {
        /// <summary>
        /// Name of the module this runtime is assigned to
        /// </summary>
        internal readonly string ModuleName;
        
        /// <summary>
        /// Instance of PrefabComponent, which deals with XML files
        /// </summary>
        internal readonly PrefabComponent PrefabComponent;
        
        /// <summary>
        /// Instance of ViewModelComponent, which deals with child classes of `ViewModel`
        /// </summary>
        internal readonly ViewModelComponent ViewModelComponent;

        /// <summary>
        /// Instance of CodePatcherComponent, which deals with Harmony
        /// </summary>
        internal readonly CodePatcherComponent CodePatcherComponent;

        /// <summary>
        /// Whether `Verify()` was actually called
        /// </summary>
        internal bool VerifyCalled = false;
        
        /// <summary>
        /// User messages to be displayed during `Verify()` call
        /// </summary>
        private List<InformationMessage> _userMessages = new List<InformationMessage>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="moduleName">Name of the module this runtime is assigned to</param>
        internal UIExtenderRuntime(string moduleName)
        {
            ModuleName = moduleName;
            
            PrefabComponent = new PrefabComponent(moduleName);
            ViewModelComponent = new ViewModelComponent(moduleName);
            CodePatcherComponent = new CodePatcherComponent(this);
        }

        /// <summary>
        /// Display user warning when `Verify()` will be called
        /// </summary>
        /// <param name="text"></param>
        internal void AddUserWarning(string text)
        {
            _userMessages.Add(new InformationMessage(text, Colors.Yellow));
        }

        /// <summary>
        /// Display user error when `Verify()` will be called
        /// </summary>
        /// <param name="text"></param>
        internal void AddUserError(string text)
        {
            _userMessages.Add(new InformationMessage(text, Colors.Red));
        }

        /// <summary>
        /// Register types attributed with `UIExtenderLibExtension`:
        /// 1. will add extensions to their respective components
        /// 2. will add standard patches and patch game
        /// 3. will force game to reload affected XMLs
        /// </summary>
        /// <param name="types"></param>
        internal void Register(IEnumerable<Type> types)
        {
            foreach (var extensionType in types)
            {
                var baseAttribute = Attribute.GetCustomAttribute(extensionType, typeof(UIExtenderLibExtensionAttribute));
                switch (baseAttribute)
                {
                    case PrefabExtensionAttribute xmlExtension:
                    {
                        var constructor = extensionType.GetConstructor(Type.EmptyTypes);
                        if (constructor == null)
                        {
                            Debug.Fail("Failed to find appropriate constructor for patch!");
                            continue;
                        }
                    
                        // gauntlet xml extension
                        switch (constructor.Invoke(Array.Empty<object>()))
                        {
                            case PrefabExtensionInsertPatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;
                        
                            case PrefabExtensionReplacePatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;
                        
                            case PrefabExtensionInsertAsSiblingPatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;
                        
                            case CustomPatch<XmlDocument> patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, patch.Apply);
                                break;
                        
                            case CustomPatch<XmlNode> patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch.Apply);
                                break;
                        
                            default:
                                Debug.Fail($"Patch class is unsupported - {extensionType}!");
                                break;
                        }

                        break;
                    }
                    
                    case ViewModelMixinAttribute _:
                        // view model mixin
                        ViewModelComponent.RegisterViewModelMixin(extensionType);
                        break;

                    case CodePatcherAttribute _:
                        CodePatcherComponent.RegisterCustom(extensionType);
                        break;

                    default:
                        Debug.Fail($"Failed to find appropriate clause for base type {extensionType} with attribute {baseAttribute}!");
                        break;
                }
            }

            Enable();
        }

        internal void Enable()
        {
            // add core patches (in order for UIExtender to actually work)
            if (!CorePatches.AddTo(CodePatcherComponent))
            {
                _userMessages.Add(new InformationMessage($"Failed to patch {ModuleName} (outdated).", Colors.Red));
                return;
            }

            // finalize code patcher and let harmony apply patches
            CodePatcherComponent.Enable();

            // save .dll for troubleshooting
            ViewModelComponent.SaveDebugImages();

            // force reload movies that should be patched by extensions
            PrefabComponent.Enable();
        }
        internal void Disable()
        {
            // add core patches (in order for UIExtender to actually work)
            //if (!CorePatches.AddTo(CodePatcherComponent))
            //{
            //    _userMessages.Add(new InformationMessage($"Failed to patch {ModuleName} (outdated).", Colors.Red));
            //    return;
            //}

            // finalize code patcher and let harmony apply patches
            CodePatcherComponent.Disable();

            // save .dll for troubleshooting
            //ViewModelComponent.SaveDebugImages();

            // force reload movies that should be patched by extensions
            PrefabComponent.Disable();
        }

        /// <summary>
        /// Verify registration and emit user warnings/errors if needed
        /// </summary>
        internal void Verify()
        {
            VerifyCalled = true;

            foreach (var message in _userMessages)
            {
                InformationManager.DisplayMessage(message);
            }
        }
    }
}