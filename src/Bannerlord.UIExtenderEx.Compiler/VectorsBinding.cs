#if NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Binds references to <c>System.Numerics.Vectors</c> to whichever copy the process hosts, at any compatible version.
/// <para>
/// When running under .NET Framework, System.Memory relies on <c>Vector&lt;T&gt;</c>. Vectors cannot be ILRepacked because
/// the legacy JIT recognizes SIMD hardware intrinsics only within an assembly strictly named <c>System.Numerics.Vectors</c>.
/// Because game distributions ship a copy in their bin directory whose exact version might not match the version requested
/// by merged libraries, this resolve hook provides a runtime binding redirect when standard assembly binding fails.
/// </para>
/// <para>
/// Because the runtime's <c>RequestingAssembly</c> is <see langword="null"/> for these binds, the handler resolves requests by simple name.
/// </para>
/// </summary>
internal static class VectorsBinding
{
    private const string AssemblyName = "System.Numerics.Vectors";

    [ThreadStatic]
    private static bool _resolving;

    /// <summary>Installs the <see cref="AppDomain.AssemblyResolve"/> handler when the module is initialized, prior to JIT compilation of vector instructions.</summary>
    [ModuleInitializer]
    internal static void Install() => AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        if (_resolving || !string.Equals(new AssemblyName(args.Name).Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
            return null;

        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => string.Equals(x.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.GetName().Version)
            .FirstOrDefault();
        if (loaded is not null)
            return loaded;

        // Resolves by simple name to load whatever version exists in the application directory. Guarded against re-entrant resolution loops.
        _resolving = true;
        try
        {
            return Assembly.Load(AssemblyName);
        }
        catch (Exception e) when (e is IOException or BadImageFormatException)
        {
            return null;
        }
        finally
        {
            _resolving = false;
        }
    }
}
#endif