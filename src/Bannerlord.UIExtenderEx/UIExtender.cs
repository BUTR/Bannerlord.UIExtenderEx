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
using System.Threading;

namespace Bannerlord.UIExtenderEx;

/// <summary>
/// Provides module-level registration and lifecycle management for UI extensions and view model mixins.
/// </summary>
public class UIExtender
{
    public static UIExtender Create(string moduleName) => new(moduleName, false);
    public static UIExtender? GetUIExtenderFor(string moduleName) => Instances.TryGetValue(moduleName, out var uiExtender) ? uiExtender : null;
    internal static UIExtenderRuntime? GetRuntimeFor(string moduleName) => Instances[moduleName]._runtime;

    /// <summary>
    /// Gets all active runtimes in the order they were registered.
    /// Published as an immutable array snapshot to allow allocation-free, thread-safe iteration during lifecycle hooks.
    /// </summary>
    internal static UIExtenderRuntime[] GetAllRuntimes() => Volatile.Read(ref _runtimes);

    private static UIExtenderRuntime[] _runtimes = [];


    /// <summary>
    /// Shared <see cref="Harmony"/> instance used across all UIExtenderEx patches.
    /// </summary>
    internal static readonly Harmony Harmony = new("bannerlord.uiextender.ex");

    /// <summary>
    /// Assemblies containing target methods patched during initialization.
    /// Loaded eagerly to ensure reflection queries resolve target methods.
    /// </summary>
    private static readonly string[] PatchedAssemblies =
    [
        "TaleWorlds.Library",
        "TaleWorlds.GauntletUI",
        "TaleWorlds.GauntletUI.PrefabSystem",
        "TaleWorlds.GauntletUI.Data",
        "TaleWorlds.Engine.GauntletUI",
    ];

    static UIExtender()
    {
        LoadPatchedAssemblies();

        GauntletMoviePatch.Patch(Harmony);
        ParsePatch.Patch(Harmony);
        ViewModelPatch.Patch(Harmony);
        WidgetPrefabPatch.Patch(Harmony);
        BrushFactoryManager.Patch(Harmony);
        WidgetFactoryManager.Patch(Harmony);
        EventManagerPatch.Patch(Harmony);
        UIConfigPatch.Patch(Harmony);
    }

    private static void LoadPatchedAssemblies()
    {
        var loaded = new HashSet<string>(AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName().Name));
        foreach (var name in PatchedAssemblies)
        {
            // Skip assemblies that are already loaded to avoid duplicate assembly references.
            if (loaded.Contains(name))
                continue;

            try
            {
                Assembly.Load(new AssemblyName(name));
            }
            catch (Exception e)
            {
                MessageUtils.DisplayUserWarning("Failed to load {0} ({1})! Some changes mods make to the game's screens may not appear.", name, e.Message);
            }
        }
    }

    /// <summary>
    /// Active extender instances indexed by module name.
    /// </summary>
    private static readonly Dictionary<string, UIExtender> Instances = new();

    /// <summary>
    /// Name of the module associated with this extender instance.
    /// </summary>
    private readonly string _moduleName;

    /// <summary>
    /// Runtime instance associated with this extender.
    /// </summary>
    private UIExtenderRuntime? _runtime;

    private UIExtender(string moduleName, bool _)
    {
        _moduleName = moduleName;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="UIExtender"/> for the specified module.
    /// </summary>
    /// <param name="moduleName">The module name matching the module folder on disk.</param>
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
    /// Registers all extension types and unsafe accessor stubs found in the specified assembly.
    /// Should be invoked during module load.
    /// </summary>
    /// <param name="assembly">The assembly containing extension types.</param>
    public void Register(Assembly assembly)
    {
        Trace.TraceInformation("{0} - Register: {1}", _moduleName, assembly);

        var loadable = LoadableTypes(assembly).ToList();

        // Accessor stubs can be in any type, not only in the ones carrying an extension attribute
        Register(loadable.Where(t => t.CustomAttributes.Any(a => a.AttributeType.IsSubclassOf(typeof(BaseUIExtenderAttribute)))).ToList(), loadable);
    }

    /// <summary>
    /// Retrieves all loadable types from <paramref name="assembly"/>, logging warnings for any types that fail to load.
    /// </summary>
    private IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            foreach (var message in exception.LoaderExceptions.Where(x => x is not null).Select(x => x!.Message).Distinct())
                Trace.TraceWarning("UIExtenderEx: {0}: a type of {1} did not load and is not registered: {2}", _moduleName, assembly.GetName().Name, message);
            return exception.Types.Where(x => x is not null).Select(x => x!);
        }
    }

    /// <summary>
    /// Registers the specified extension types.
    /// Should be invoked during module load.
    /// </summary>
    /// <param name="types">The extension types to register.</param>
    public void Register(IEnumerable<Type> types)
    {
        var list = types.ToList();
        Register(list, list);
    }

    /// <summary>
    /// Registers extension types and prepares unsafe accessor stubs for patching.
    /// </summary>
    private void Register(List<Type> types, List<Type> stubTypes)
    {
        Trace.TraceInformation("{0} - Register Types", _moduleName);

        if (Instances.ContainsKey(_moduleName))
        {
            MessageUtils.DisplayUserError($"Failed to load extension module {_moduleName} - already loaded!");
            return;
        }

        Instances[_moduleName] = this;
        _runtime = new(_moduleName);
        Volatile.Write(ref _runtimes, [.. _runtimes, _runtime]);

        UnsafeAccessorPatch.Register(Harmony, _moduleName, stubTypes);
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
            _runtime?.Deregister();
            Instances.Remove(_moduleName);
            var runtime = _runtime;
            Volatile.Write(ref _runtimes, [.. _runtimes.Where(x => x != runtime)]);
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
        Trace.TraceInformation("{0} - Disable {1}", _moduleName, type);

        if (_runtime is null)
        {
            MessageUtils.Fail("Register() method was not called before Disable(type)!");
            return;
        }

        _runtime.Disable(type);
    }
}