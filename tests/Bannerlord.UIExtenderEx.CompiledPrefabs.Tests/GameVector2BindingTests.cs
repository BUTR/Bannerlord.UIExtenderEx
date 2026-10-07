#if !NET
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies that generated code names the <c>System.Numerics.Vector2</c> that <c>Vector2PropertyChanged</c> carries in the game.
/// <para>
/// On .NET Framework the game's <c>System.Numerics.Vectors</c> 4.1.3.0 is a netstandard build that defines Vector2 itself,
/// while the framework's <c>System.Numerics</c> defines another, and mods ship copies of <c>System.Numerics.Vectors</c> that
/// forward to that one (ButterLib's 4.1.4.0 resolves every request for a newer version). The game process holds both types.
/// Compiling against the framework's made every movie with a Vector2 subscription fail with
/// <see cref="MissingMethodException"/> for <c>add_Vector2PropertyChanged</c>.
/// </para>
/// <para>
/// The test output cannot show it: its own <c>System.Numerics.Vectors</c> forwards, so there is one Vector2 there. The probe
/// runs in an AppDomain set up the way the game binds - the game's copy and no redirect.
/// </para>
/// </summary>
public class GameVector2BindingTests
{
    private const string Vectors = "System.Numerics.Vectors";

    public sealed class Probe : MarshalByRefObject
    {
        /// <summary>Names nothing of the game's, so that compiling it loads none of the game's assemblies.</summary>
        public string? Prepare(string forwardingCopy, bool loadForwardingCopyFirst)
        {
            // What a mod's resolver does with the requests for a newer version than the game's
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) => new AssemblyName(e.Name).Name == Vectors ? Assembly.LoadFrom(forwardingCopy) : null;
            if (!loadForwardingCopyFirst)
                return null;
            if (AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name == Vectors))
                return "test premise: System.Numerics.Vectors was loaded before the forwarding copy";
            Assembly.LoadFrom(forwardingCopy);
            return null;
        }

        public string Run(string[] mods)
        {
            var carried = typeof(PropertyOwnerObject).GetEvent(nameof(PropertyOwnerObject.Vector2PropertyChanged))!.EventHandlerType!.GetGenericArguments()[2];
            if (carried.Assembly.GetName().Name != Vectors)
                return $"test premise: the game's event carries the Vector2 of {carried.Assembly.GetName().Name}";

            var seeds = new List<Assembly> { typeof(Widget).Assembly };
            seeds.AddRange(mods.Select(Assembly.LoadFrom));
            var references = PrefabReferenceSet.ToPaths(PrefabReferenceSet.Select(seeds));
            var source = new GeneratedSource("Probe.gen.cs", """
                public static class Probe
                {
                    public static void Run(TaleWorlds.GauntletUI.BaseTypes.Widget widget) => widget.Vector2PropertyChanged += Handler;
                    private static void Handler(TaleWorlds.GauntletUI.PropertyOwnerObject owner, string name, System.Numerics.Vector2 value) { }
                }
                """);
            var result = new RoslynCompiler().Compile("Probe", [source], references);
            if (!result.Success)
                return string.Join("\n", result.Errors);

            var run = Assembly.Load(result.Assembly!).GetType("Probe")!.GetMethod("Run")!;
            try
            {
                RuntimeHelpers.PrepareMethod(run.MethodHandle);
                return "bound";
            }
            catch (MissingMethodException e)
            {
                return e.Message;
            }
        }
    }

    /// <summary>Every BUTR module loader names <c>System.Numerics</c>; a few dozen mods must not outvote the game's own event.</summary>
    [Test]
    public void ModsNamingSystemNumerics_DoNotDecideTheVector2() => AssertBound(loadForwardingCopyFirst: false, numericsMods: 40);

    /// <summary>A forwarding copy loaded before the game's is the first <c>System.Numerics.Vectors</c> in the AppDomain.</summary>
    [Test]
    public void AForwardingCopyLoadedFirst_DoesNotDecideTheVector2() => AssertBound(loadForwardingCopyFirst: true, numericsMods: 0);

    private static void AssertBound(bool loadForwardingCopyFirst, int numericsMods)
    {
        if (TestGame.Bin is not { } bin || !File.Exists(Path.Combine(bin, Vectors + ".dll")))
        {
            Assert.Ignore("No game installation; set BANNERLORD_GAME_DIR to run this.");
            return;
        }

        var output = Path.GetDirectoryName(typeof(GameVector2BindingTests).Assembly.Location)!;
        var root = Path.Combine(Path.GetTempPath(), "UIExtenderEx-vector2-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "bin");
        var other = Path.Combine(root, "other");
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(other);
        foreach (var file in Directory.GetFiles(output))
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));

        // The test output's copy forwards to System.Numerics, as the ones mods ship do; the game's own takes its place
        var forwardingCopy = Path.Combine(other, Vectors + ".dll");
        File.Move(Path.Combine(folder, Vectors + ".dll"), forwardingCopy);
        File.Copy(Path.Combine(bin, Vectors + ".dll"), Path.Combine(folder, Vectors + ".dll"));

        // The game has no redirect for it; the test output's would send every request to a version that is not there
        var config = Path.Combine(folder, Path.GetFileName(typeof(GameVector2BindingTests).Assembly.Location) + ".config");
        var document = XDocument.Load(config);
        document.Descendants().Where(x => x.Name.LocalName == "dependentAssembly"
                                          && x.Elements().Any(y => y.Name.LocalName == "assemblyIdentity" && (string?) y.Attribute("name") == Vectors))
            .ToList().ForEach(x => x.Remove());
        document.Save(config);

        var mods = new List<string>();
        var modReferences = new[] { typeof(object).Assembly.Location, typeof(System.Numerics.BigInteger).Assembly.Location };
        for (var i = 0; i < numericsMods; i++)
        {
            var name = $"NumericsMod{i}";
            var mod = new RoslynCompiler().Compile(name, [new GeneratedSource(name + ".gen.cs", "public static class M { public static System.Numerics.BigInteger X; }")], modReferences);
            Assume.That(mod.Success, Is.True, string.Join("\n", mod.Errors));
            var path = Path.Combine(other, name + ".dll");
            File.WriteAllBytes(path, mod.Assembly!);
            mods.Add(path);
        }

        var domain = AppDomain.CreateDomain("UIExtenderEx-vector2", null, new AppDomainSetup { ApplicationBase = folder, ConfigurationFile = config });
        try
        {
            var probe = (Probe) domain.CreateInstanceAndUnwrap(typeof(Probe).Assembly.FullName!, typeof(Probe).FullName!);
            var outcome = probe.Prepare(forwardingCopy, loadForwardingCopyFirst) ?? probe.Run([.. mods]);
            Assert.That(outcome, Is.EqualTo("bound"), outcome);
        }
        finally
        {
            AppDomain.Unload(domain);
            try { Directory.Delete(root, true); }
            catch (Exception) { /* Ignore file locks held by the unloaded AppDomain. */ }
        }
    }
}
#endif
