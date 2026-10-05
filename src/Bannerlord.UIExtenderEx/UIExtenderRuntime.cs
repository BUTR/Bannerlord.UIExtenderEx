using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Components;
using Bannerlord.UIExtenderEx.Utils;

using System;
using System.Collections.Generic;
using System.Xml;

namespace Bannerlord.UIExtenderEx;

/// <summary>
/// Encapsulates runtime extension state and components for an individual module.
/// </summary>
internal class UIExtenderRuntime
{
    /// <summary>
    /// Gets the name of the module managed by this runtime.
    /// </summary>
    public readonly string ModuleName;

    /// <summary>
    /// Gets the prefab component handling Gauntlet XML patches.
    /// </summary>
    public readonly PrefabComponent PrefabComponent;

    /// <summary>
    /// Gets the view model component managing view model mixins.
    /// </summary>
    public readonly ViewModelComponent ViewModelComponent;

    /// <summary>
    /// Initializes a new instance of the <see cref="UIExtenderRuntime"/> class for the specified module.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    public UIExtenderRuntime(string moduleName)
    {
        ModuleName = moduleName;

        PrefabComponent = new(moduleName);
        ViewModelComponent = new(moduleName);
    }

    /// <summary>
    /// Registers extension types decorated with <see cref="BaseUIExtenderAttribute"/> to their respective components.
    /// </summary>
    /// <param name="types">The extension types to register.</param>
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
                            MessageUtils.Fail("Failed to find appropriate constructor for patch!");
                            continue;
                        }

                        // Gauntlet XML extension
                        switch (constructor.Invoke([]))
                        {
                            case Prefabs.PrefabExtensionSetAttributePatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;
#pragma warning disable CS0618
                            case Prefabs.PrefabExtensionInsertPatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;
#pragma warning restore CS0618
                            case Prefabs.PrefabExtensionReplacePatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;
                            case Prefabs.PrefabExtensionInsertAsSiblingPatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;
                            case Prefabs.CustomPatch<XmlDocument> patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, patch.GetType(), patch.Apply);
                                break;
                            case Prefabs.CustomPatch<XmlNode> patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch.GetType(), patch.Apply);
                                break;

                            case Prefabs2.PrefabExtensionSetAttributePatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;
                            case Prefabs2.PrefabExtensionInsertPatch patch:
                                PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
                                break;

                            default:
                                MessageUtils.Fail($"Patch class is unsupported - {extensionType}!");
                                break;
                        }

                        break;
                    }

                    case ViewModelMixinAttribute viewModelExtension:
                        // View model mixin
                        ViewModelComponent.RegisterViewModelMixin(extensionType, viewModelExtension.RefreshMethodName, viewModelExtension.HandleDerived);
                        break;

                    default:
                        MessageUtils.Fail($"Failed to find appropriate clause for base type {extensionType} with attribute {baseAttribute}!");
                        break;
                }
            }
        }
    }

    public void Deregister()
    {
        PrefabComponent.Deregister();
        ViewModelComponent.Deregister();
    }

    public void Enable()
    {
        // Enable view model mixins and apply dynamic binding tables.
        ViewModelComponent.Enable();

        // Reload movies patched by prefab extensions.
        PrefabComponent.Enable();
    }
    public void Disable()
    {
        // Disable view model mixins.
        ViewModelComponent.Disable();

        // Reload movies patched by prefab extensions.
        PrefabComponent.Disable();
    }

    public void Enable(Type type)
    {
        ViewModelComponent.Enable(type);
        PrefabComponent.Enable(type);
    }
    public void Disable(Type type)
    {
        ViewModelComponent.Disable(type);
        PrefabComponent.Disable(type);
    }
}