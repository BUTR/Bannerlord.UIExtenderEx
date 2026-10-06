using Bannerlord.UIExtenderEx.Runtimes;

using NUnit.Framework;

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

using TaleWorlds.ModuleManager;

using Module = TaleWorlds.MountAndBlade.Module;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies <see cref="RuntimeSubModules"/>: the game skips a prefab runtime submodule whose DLL cannot be loaded, instead of
/// crashing on it, and loads the others as before.
/// <para>
/// Asks the game's own <c>Module.CheckIfSubmoduleCanBeLoadable</c>, which <c>Module.LoadSubModules</c> calls before it
/// loads a submodule's DLL. A DLL that is not a .NET assembly stands in for one Windows blocks: either way
/// <c>Assembly.LoadFrom</c> throws.
/// </para>
/// </summary>
public class RuntimeSubModulesTests
{
    private const string XmlPrefabs = "Bannerlord.UIExtenderEx.XmlPrefabs";
    private const string CompiledPrefabs = "Bannerlord.UIExtenderEx.CompiledPrefabs";

    private readonly Module _module = (Module) FormatterServices.GetUninitializedObject(typeof(Module));
    private string _binFolder = null!;

    [SetUp]
    public void SetUp()
    {
        _binFolder = Path.Combine(Path.GetTempPath(), "UIExtenderEx-unloadable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_binFolder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_binFolder, true);

    private static SubModuleInfo NewSubModule(string name)
    {
        var document = new XmlDocument();
        document.LoadXml($"""
            <SubModule>
              <Name value="{name}" />
              <DLLName value="{name}.dll" />
              <SubModuleClassType value="{name}.SubModule" />
            </SubModule>
            """);
        var subModule = new SubModuleInfo();
        subModule.LoadFrom(document.DocumentElement!, AppDomain.CurrentDomain.BaseDirectory, false);
        return subModule;
    }

    /// <summary>Puts a DLL in the bin folder that no runtime can load.</summary>
    private void WriteUnloadable(string name) => File.WriteAllText(Path.Combine(_binFolder, $"{name}.dll"), "blocked");

    private bool GameLoads(SubModuleInfo subModule) => _module.CheckIfSubmoduleCanBeLoadable(subModule);

    [TestCase(XmlPrefabs)]
    [TestCase("Bannerlord.UIExtenderEx.GamePrefabs")]
    [TestCase(CompiledPrefabs)]
    public void ARuntimeThatCannotBeLoaded_IsSkippedByTheGame(string runtime)
    {
        WriteUnloadable(runtime);
        var subModule = NewSubModule(runtime);
        Assert.That(GameLoads(subModule), Is.True, "test premise");

        RuntimeSubModules.RejectUnloadable([subModule], _binFolder);

        Assert.That(GameLoads(subModule), Is.False);
        Assert.That(RuntimeSubModules.RejectedDlls, Does.Contain($"{runtime}.dll"), "and reported in the main menu");
    }

    [Test]
    public void ARuntimeThatLoads_IsLoadedByTheGame()
    {
        var subModule = NewSubModule(XmlPrefabs);

        RuntimeSubModules.RejectUnloadable([subModule], AppDomain.CurrentDomain.BaseDirectory);

        Assert.That(GameLoads(subModule), Is.True);
    }

    [Test]
    public void AMissingRuntime_IsLeftToTheGame()
    {
        var subModule = NewSubModule(CompiledPrefabs);

        RuntimeSubModules.RejectUnloadable([subModule], _binFolder);

        Assert.That(GameLoads(subModule), Is.True, "the game shows its Cannot find message box and carries on");
    }

    [TestCase("Bannerlord.UIExtenderEx", Description = "the core, which the runtimes need")]
    [TestCase("SomeMod", Description = "another module's")]
    public void AnyOtherSubModuleThatCannotBeLoaded_IsLeftToTheGame(string name)
    {
        WriteUnloadable(name);
        var subModule = NewSubModule(name);

        RuntimeSubModules.RejectUnloadable([subModule], _binFolder);

        Assert.That(GameLoads(subModule), Is.True);
        Assert.That(RuntimeSubModules.RejectedDlls, Does.Not.Contain($"{name}.dll"));
    }
}
