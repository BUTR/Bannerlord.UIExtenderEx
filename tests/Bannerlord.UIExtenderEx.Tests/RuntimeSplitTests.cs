using NUnit.Framework;

using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

using TaleWorlds.MountAndBlade;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies architectural decoupling across prefab runtimes, ensuring the core assembly contains zero references
/// to runtime assemblies and that <c>SubModule.xml</c> initializes them in designated dependency order.
/// </summary>
public class RuntimeSplitTests
{
    /// <summary>Enumerates assemblies residing on the runtime side of the modular split.</summary>
    private static readonly string[] RuntimeSideAssemblies =
    [
        "Bannerlord.UIExtenderEx.CodeGenerator",
        "Bannerlord.UIExtenderEx.Compiler",
        "Bannerlord.UIExtenderEx.CompiledPrefabs",
        "Bannerlord.UIExtenderEx.GamePrefabs",
        "Bannerlord.UIExtenderEx.XmlPrefabs",
    ];

    /// <summary>
    /// Verifies that the core UIExtenderEx assembly references no runtime-side assemblies, allowing core functionality
    /// to load and execute independently without runtime dependencies.
    /// </summary>
    [Test]
    public void TheCoreReferencesNoRuntimeSideAssembly()
    {
        var referenced = typeof(UIExtender).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();

        Assert.That(referenced.Intersect(RuntimeSideAssemblies), Is.Empty);
    }

    /// <summary>
    /// Verifies that <c>SubModule.xml</c> sequences startup correctly, loading the core module prior to prefab runtimes,
    /// and ordering the game prefab runtime ahead of the compiled runtime to avoid redundant compilations.
    /// </summary>
    [Test]
    public void TheShippedSubModuleXml_StartsTheCoreThenTheRuntimes_EachFromItsOwnAssembly()
    {
        var document = new XmlDocument();
        document.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "UIExtenderEx", "SubModule.xml"));
        var moduleId = document.SelectSingleNode("/Module/Id/@value")!.Value!;
        Assert.That(moduleId, Is.EqualTo("$moduleid$"), "test premise: the manifest as it is in the source tree");

        var subModules = document.SelectNodes("/Module/SubModules/SubModule")!.Cast<XmlNode>()
            .Select(x => (Dll: x.SelectSingleNode("DLLName/@value")!.Value!.Replace("$moduleid$", "Bannerlord.UIExtenderEx"),
                          Class: x.SelectSingleNode("SubModuleClassType/@value")!.Value!.Replace("$moduleid$", "Bannerlord.UIExtenderEx")))
            .ToList();

        Assert.That(subModules.Select(x => x.Dll), Is.EqualTo(new[]
        {
            "Bannerlord.UIExtenderEx.dll",
            "Bannerlord.UIExtenderEx.XmlPrefabs.dll",
            "Bannerlord.UIExtenderEx.GamePrefabs.dll",
            "Bannerlord.UIExtenderEx.CompiledPrefabs.dll",
        }));

        foreach (var (dll, className) in subModules)
        {
            var assembly = Assembly.Load(new AssemblyName(Path.GetFileNameWithoutExtension(dll)));
            var type = assembly.GetType(className);
            Assert.That(type, Is.Not.Null, $"{className} is not in {dll}");
            Assert.That(typeof(MBSubModuleBase).IsAssignableFrom(type), Is.True, $"{className} is no SubModule");
        }
    }
}