using Bannerlord.UIExtenderEx.CompiledPrefabs;

using NUnit.Framework;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

#if NET
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

using TaleWorlds.GauntletUI.PrefabSystem;
#endif

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies graceful degradation when the standalone compiler assembly is missing or quarantined.
/// <para>
/// Ensures the compiled runtime assembly loads, enumerates types, and initializes SubModules across both .NET Framework and .NET 6 runtimes,
/// while configuring the manager to enter a disabled state that routes all movie loads to XML.
/// </para>
/// </summary>
public class CompilerMissingTests
{
    private const string CompilerAssembly = "Bannerlord.UIExtenderEx.Compiler";
    private const string RuntimeAssembly = "Bannerlord.UIExtenderEx.CompiledPrefabs";
    private const string SubModuleType = "Bannerlord.UIExtenderEx.CompiledPrefabs.SubModule";

    /// <summary>
    /// Loads the compiled runtime assembly in an isolated context lacking the compiler, enumerates its types, instantiates its SubModule,
    /// and reports the resulting registration state.
    /// </summary>
    public sealed class Probe : MarshalByRefObject
    {
        public string Run()
        {
            try
            {
                Assembly.Load(CompilerAssembly);
                return "test premise: the compiler assembly could be loaded";
            }
            catch (FileNotFoundException) { }

            var runtime = Assembly.Load(RuntimeAssembly);
            try
            {
                runtime.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return "GetTypes failed: " + string.Join("; ", e.LoaderExceptions.Select(x => x?.Message));
            }

            Activator.CreateInstance(runtime.GetType(SubModuleType, true)!);

            var core = AppDomain.CurrentDomain.GetAssemblies().First(x => x.GetName().Name == "Bannerlord.UIExtenderEx");
            var runtimes = (Array) core.GetType("Bannerlord.UIExtenderEx.Runtimes.PrefabRuntimes", true)!
                .GetProperty("All", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            return $"registered {runtimes.Length}";
        }
    }

#if NET
    private sealed class NoCompilerContext : AssemblyLoadContext
    {
        public NoCompilerContext() : base("UIExtenderEx-no-compiler", isCollectible: true) { }

        // Delegate all other assembly loads to the default context.
        protected override Assembly? Load(AssemblyName assemblyName) =>
            assemblyName.Name == CompilerAssembly ? throw new FileNotFoundException("quarantined", CompilerAssembly + ".dll") : null;
    }

    [Test]
    public void WithoutTheCompilerAssembly_TheManagerComesUpDisabled()
    {
        var context = new NoCompilerContext();
        try
        {
            var prefabs = context.LoadFromAssemblyPath(typeof(CompiledPrefabManager).Assembly.Location);
            var manager = prefabs.GetType(typeof(CompiledPrefabManager).FullName!, true)!;
            var environment = prefabs.GetType(typeof(ICompiledPrefabEnvironment).FullName!, true)!;

            // Simulate environment construction failure when the compiler assembly cannot be loaded.
            var factory = Expression.Lambda(typeof(Func<>).MakeGenericType(environment),
                Expression.Throw(Expression.Constant(new FileNotFoundException("quarantined", CompilerAssembly + ".dll")), environment)).Compile();

            object instance = null!;
            Assert.That(() => instance = manager.GetMethod(nameof(CompiledPrefabManager.Create))!.Invoke(null, [factory])!, Throws.Nothing);
            Assert.That(manager.GetProperty(nameof(CompiledPrefabManager.IsDisabled))!.GetValue(instance), Is.True);

            // Verify entry points fall back to XML without invoking the compiler.
            Assert.That(manager.GetMethod(nameof(CompiledPrefabManager.TryUseCompiledPrefab))!.Invoke(instance, [null, "SomeMovie", null]), Is.False);
            Assert.That(() => manager.GetMethod(nameof(CompiledPrefabManager.OnPrefabsCollected))!.Invoke(instance, [new GeneratedPrefabContext()]), Throws.Nothing);
            Assert.That(() => manager.GetMethod(nameof(CompiledPrefabManager.WarmUpCompilers))!.Invoke(instance, []), Throws.Nothing);
            Assert.That(() => manager.GetMethod(nameof(CompiledPrefabManager.PreloadDeferredBuilds))!.Invoke(instance, []), Throws.Nothing);
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>
    /// Verifies that the compiled runtime assembly enumerates types and JIT-prepares SubModule initialization methods
    /// in the absence of the compiler assembly under .NET.
    /// </summary>
    [Test]
    public void WithoutTheCompilerAssembly_TheCompiledRuntimeLoadsEnumeratesAndStarts()
    {
        var context = new NoCompilerContext();
        try
        {
            Assert.That(() => context.LoadFromAssemblyName(new AssemblyName(CompilerAssembly)), Throws.InstanceOf<FileNotFoundException>(), "test premise");

            var runtime = context.LoadFromAssemblyPath(typeof(CompiledPrefabRuntime).Assembly.Location);
            Assert.That(() => runtime.GetTypes(), Throws.Nothing);

            var subModule = runtime.GetType(SubModuleType, true)!;
            var install = runtime.GetType(typeof(CompiledPrefabRuntime).FullName!, true)!.GetMethod(nameof(CompiledPrefabRuntime.Install))!;
            Assert.That(() => RuntimeHelpers.PrepareMethod(subModule.GetConstructor(Type.EmptyTypes)!.MethodHandle), Throws.Nothing);
            Assert.That(() => RuntimeHelpers.PrepareMethod(install.MethodHandle), Throws.Nothing);
            Assert.That(context.Assemblies.Select(x => x.GetName().Name), Does.Not.Contain(CompilerAssembly));
        }
        finally
        {
            context.Unload();
        }
    }
#else
    /// <summary>
    /// Verifies that the compiled runtime loads and initializes in an isolated AppDomain configured without the compiler assembly under .NET Framework.
    /// </summary>
    [Test]
    public void WithoutTheCompilerAssembly_TheCompiledRuntimeLoadsEnumeratesAndStarts()
    {
        var output = Path.GetDirectoryName(typeof(CompilerMissingTests).Assembly.Location)!;
        var folder = Path.Combine(Path.GetTempPath(), "UIExtenderEx-no-compiler-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.GetFiles(output).Where(x => !Path.GetFileName(x).StartsWith(CompilerAssembly + ".", StringComparison.Ordinal)))
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));

        var setup = new AppDomainSetup
        {
            ApplicationBase = folder,
            ConfigurationFile = Path.Combine(folder, Path.GetFileName(typeof(CompilerMissingTests).Assembly.Location) + ".config"),
        };
        var domain = AppDomain.CreateDomain("UIExtenderEx-no-compiler", null, setup);
        try
        {
            var probe = (Probe) domain.CreateInstanceAndUnwrap(typeof(Probe).Assembly.FullName!, typeof(Probe).FullName!);
            Assert.That(probe.Run(), Is.EqualTo("registered 1"), "the SubModule logs a failed install and carries on, so only a registered runtime shows it installed");
        }
        finally
        {
            AppDomain.Unload(domain);
            try { Directory.Delete(folder, true); }
            catch (Exception) { /* Ignore file locks held by the unloaded AppDomain. */ }
        }
    }
#endif
}
