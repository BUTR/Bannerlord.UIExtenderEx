using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Components;
using Bannerlord.UIExtenderEx.Patches;

using System;
using System.Collections.Generic;
using System.Xml;

namespace Bannerlord.UIExtenderEx
{
    /// <summary>
    /// Actual runtime of UIExtender, assigned to each module's instance of `UIExtender`
    /// </summary>
    internal class UIExtenderRuntime
    {
        /// <summary>
        /// Name of the module this runtime is assigned to
        /// </summary>
        public readonly string ModuleName;

        /// <summary>
        /// Instance of PrefabComponent, which deals with XML files
        /// </summary>
        public readonly PrefabComponent PrefabComponent;

        /// <summary>
        /// Instance of ViewModelComponent, which deals with child classes of `ViewModel`
        /// </summary>
        public readonly ViewModelComponent ViewModelComponent;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="moduleName">Name of the module this runtime is assigned to</param>
        public UIExtenderRuntime(string moduleName)
        {
            ModuleName = moduleName;

            PrefabComponent = new PrefabComponent(moduleName);
            ViewModelComponent = new ViewModelComponent(moduleName);
        }

        /// <summary>
        /// Register types attributed with `UIExtenderLibExtension`:
        /// 1. will add extensions to their respective components
        /// 2. will add standard patches and patch game
        /// 3. will force game to reload affected XMLs
        /// </summary>
        /// <param name="types"></param>
        public void Register(IEnumerable<Type> types)
        {
            foreach (var extensionType in types)
            {
                foreach (var baseAttribute in Attribute.GetCustomAttributes(extensionType, typeof(BaseUIExtenderAttribute)))
                {
                    switch (baseAttribute)
                    {
                        case PrefabExtensionAttribute xmlExtension:
                        {
                            var constructor = extensionType.GetConstructor(Type.EmptyTypes);
                            if (constructor is null)
                            {
                                Utils.Fail("Failed to find appropriate constructor for patch!");
                                continue;
                            }

                            // gauntlet xml extension
                            switch (constructor.Invoke(Array.Empty<object>()))
                            {
                                case Prefabs.PrefabExtensionSetAttributePatch patch:
                                    PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                    break;
                                case Prefabs.PrefabExtensionInsertPatch patch:
                                    PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                    break;
                                case Prefabs.PrefabExtensionReplacePatch patch:
                                    PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                    break;
                                case Prefabs.PrefabExtensionInsertAsSiblingPatch patch:
                                    PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                    break;
                                case Prefabs.CustomPatch<XmlDocument> patch:
                                    PrefabComponent.RegisterPatch(xmlExtension.Movie, patch.Apply);
                                    break;
                                case Prefabs.CustomPatch<XmlNode> patch:
                                    PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch.Apply);
                                    break;

                                case Prefabs2.PrefabExtensionSetAttributePatch patch:
                                    PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                    break;
                                case Prefabs2.PrefabExtensionInsertPatch patch:
                                    PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                    break;

                                default:
                                    Utils.Fail($"Patch class is unsupported - {extensionType}!");
                                    break;
                            }

                            GauntletMoviePatch.Register(this, xmlExtension.AutoGenWidgetName);

                            break;
                        }

                        case ViewModelMixinAttribute viewModelExtension:
                            // view model mixin
                            ViewModelComponent.RegisterViewModelMixin(extensionType, viewModelExtension.RefreshMethodName);
                            break;

                        default:
                            Utils.Fail($"Failed to find appropriate clause for base type {extensionType} with attribute {baseAttribute}!");
                            break;
                    }
                }
            }
        }

        public void Enable()
        {
            // finalize code patcher and let harmony apply patches
            ViewModelComponent.Enable();

            // force reload movies that should be patched by extensions
            PrefabComponent.Enable();
        }
        public void Disable()
        {
            // finalize code patcher and let harmony apply patches
            ViewModelComponent.Disable();

            // force reload movies that should be patched by extensions
            PrefabComponent.Disable();
        }
    }
}