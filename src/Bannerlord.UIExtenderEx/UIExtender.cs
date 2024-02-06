using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;
using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Bannerlord.UIExtenderEx;

/// <summary>
/// Client class instance of which should be created for each module using this library
/// </summary>
public class UIExtender
{
    public static UIExtender Create(string moduleName) => new(moduleName, false);
    public static UIExtender? GetUIExtenderFor(string moduleName) => Instances.TryGetValue(moduleName, out var uiExtender) ? uiExtender : null;
    internal static UIExtenderRuntime? GetRuntimeFor(string moduleName) => Instances[moduleName]._runtime;

    internal static IReadOnlyList<UIExtenderRuntime> GetAllRuntimes() => Instances.Select(x => x.Value._runtime).OfType<UIExtenderRuntime>().ToList();


    private static readonly Harmony Harmony = new("bannerlord.uiextender.ex");

    static UIExtender()
    {
#if ENABLE_PARTIAL_AUTOGEN
        GauntletMoviePatch.Patch(Harmony);
#else
        UIConfigPatch.Patch(Harmony);
#endif
        ViewModelPatch.Patch(Harmony);
        WidgetPrefabPatch.Patch(Harmony);
        BrushFactoryManager.Patch(Harmony);
        WidgetFactoryManager.Patch(Harmony);
    }

    /// <summary>
    /// Cache or runtime objects that will be accessed from patched code
    /// </summary>
    ///
    private static readonly Dictionary<string, UIExtender> Instances = new();

    /// <summary>
    /// Name of the module this instance is assigned to
    /// </summary>
    private readonly string _moduleName;

    /// <summary>
    /// Runtime instance of this extender
    /// </summary>
    private UIExtenderRuntime? _runtime;

    private UIExtender(string moduleName, bool _)
    {
        _moduleName = moduleName;
    }

    /// <summary>
    /// Default constructor. `moduleName` should match module folder because it will be used to look-up resources
    /// </summary>
    /// <param name="moduleName">Module name, should match module folder</param>
    [Obsolete("Use UIExtender.Create(moduleName) if backwards compatibility is not a concern.", false)]
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
    /// Should be called during `OnSubModuleLoad`
    /// </summary>
    /// <param name="assembly"></param>
    public void Register(Assembly assembly)
    {
        Trace.TraceInformation("{0} - Register: {1}", _moduleName, assembly);

        var types = assembly
            .GetTypes()
            .Where(t => t.CustomAttributes.Any(a => a.AttributeType.IsSubclassOf(typeof(BaseUIExtenderAttribute))));

        Register(types);
    }

    /// <summary>
    /// Register extension types
    /// Should be called during `OnSubModuleLoad`
    /// </summary>
    /// <param name="types"></param>
    public void Register(IEnumerable<Type> types)
    {
        Trace.TraceInformation("{0} - Register Types", _moduleName);

        if (Instances.ContainsKey(_moduleName))
        {
            MessageUtils.DisplayUserError($"Failed to load extension module {_moduleName} - already loaded!");
            return;
        }

        Instances[_moduleName] = this;
        _runtime = new UIExtenderRuntime(_moduleName);

        _runtime.Register(types);
    }

    public void Deregister()
    {
        Trace.TraceInformation("{0} - Deregister", _moduleName);

        if (!Instances.ContainsKey(_moduleName))
        {
            MessageUtils.DisplayUserError($"Failed to deregister {_moduleName} - not loaded!");
            return;
        }

        if (Instances[_moduleName] == this)
        {
            _runtime!.Deregister();
            Instances.Remove(_moduleName);
        }
    }

    public void Enable()
    {
        Trace.TraceInformation("{0} - Enabled", _moduleName);

        if (_runtime is null)
        {
            MessageUtils.Fail("Register() method was not called before Enable()!");
            return;
        }
        _runtime.Enable();
    }

    public void Disable()
    {
        Trace.TraceInformation("{0} - Disabled", _moduleName);

        if (_runtime is null)
        {
            MessageUtils.Fail("Register() method was not called before Disable()!");
            return;
        }
        _runtime.Disable();
    }

    public void Enable(Type type)
    {
        Trace.TraceInformation("{0} - Enable {1}", _moduleName, type);

        if (_runtime is null)
        {
            MessageUtils.Fail("Register() method was not called before Enable(type)!");
            return;
        }

        _runtime.Enable(type);
    }
    public void Disable(Type type)
    {
        Trace.TraceInformation("{0} - Enable {1}", _moduleName, type);

        if (_runtime is null)
        {
            MessageUtils.Fail("Register() method was not called before Disable(type)!");
            return;
        }

        _runtime.Disable(type);
    }
}