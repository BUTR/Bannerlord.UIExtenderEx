using Bannerlord.UIExtenderEx.Analyzers.Tasks;

using NUnit.Framework;

using System;
using System.IO;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Tests automatic game version inference when no explicit version is configured in project properties,
/// evaluating package reference identities and file system paths containing <c>TaleWorlds.Library.dll</c>.
/// </summary>
public class GameVersionInferenceTests
{
    [TestCase("Bannerlord.ReferenceAssemblies.Core", "1.3.4.102430", "v1.3.4")]
    [TestCase("Bannerlord.ReferenceAssemblies.Core", "1.5.3.122374-beta", "v1.5.3")]
    [TestCase("Bannerlord.ReferenceAssemblies.Native", "1.2.12.66233", "v1.2.12")]
    [TestCase("Bannerlord.ReferenceAssemblies.Core.EarlyAccess", "1.9.0.3526", "e1.9.0")]
    [TestCase("Lib.Harmony", "2.3.3", null)]
    public void APackageVersion_IsTheGamesWithoutItsChangeSet(string id, string version, string? expected)
    {
        Assert.That(GameVersionInference.FromPackage(id, version), Is.EqualTo(expected));
    }

    /// <summary>Verifies version extraction from <c>Version.xml</c> across Steam, GOG, Epic, and Xbox PC directory layouts.</summary>
    [TestCase("Win64_Shipping_Client", "v1.4.8", "v1.4.8")]
    [TestCase("Gaming.Desktop.x64_Shipping_Client", "v1.4.8", "v1.4.8")]
    [TestCase("Win64_Shipping_Client", "v1.2.12.66233", "v1.2.12")]
    public void AGameFolder_GivesItsVersion(string binaries, string written, string expected)
    {
        var game = Game(binaries, written);
        try
        {
            Assert.That(GameVersionInference.FromLibrary(Path.Combine(game, "bin", binaries, GameVersionInference.LibraryFileName)), Is.EqualTo(expected));
        }
        finally
        {
            Directory.Delete(game, true);
        }
    }

    [Test]
    public void ALibraryOutsideAGameFolder_GivesNothing()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "uix-version-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            Assert.That(GameVersionInference.FromLibrary(Path.Combine(folder, "lib", "net472", GameVersionInference.LibraryFileName)), Is.Null);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Verifies priority resolution order: core library package first, secondary packages next, and local game folders last.</summary>
    [Test]
    public void TheReferences_AreAskedInOrder()
    {
        var game = Game("Win64_Shipping_Client", "v1.4.8");
        try
        {
            var library = Path.Combine(game, "bin", "Win64_Shipping_Client", GameVersionInference.LibraryFileName);
            Assert.That(GameVersionInference.Infer(
            [
                new GameReference("Bannerlord.ReferenceAssemblies.Native/TaleWorlds.MountAndBlade.dll", "Bannerlord.ReferenceAssemblies.Native", "1.2.12.66233"),
                new GameReference("Bannerlord.ReferenceAssemblies.Core/" + GameVersionInference.LibraryFileName, "Bannerlord.ReferenceAssemblies.Core", "1.3.4.102430"),
            ]), Is.EqualTo("v1.3.4"));
            Assert.That(GameVersionInference.Infer(
            [
                new GameReference(library, null, null),
                new GameReference("Bannerlord.ReferenceAssemblies.Native/TaleWorlds.MountAndBlade.dll", "Bannerlord.ReferenceAssemblies.Native", "1.2.12.66233"),
            ]), Is.EqualTo("v1.2.12"), "A package before the game folder");
            Assert.That(GameVersionInference.Infer([new GameReference(library, null, null)]), Is.EqualTo("v1.4.8"));
            Assert.That(GameVersionInference.Infer([new GameReference("Lib.Harmony/0Harmony.dll", "Lib.Harmony", "2.3.3")]), Is.Null);
        }
        finally
        {
            Directory.Delete(game, true);
        }
    }

    [TestCase("1.4.8", "v1.4.8")]
    [TestCase("V1.4.8", "v1.4.8")]
    [TestCase("e1.8.1", "e1.8.1")]
    [TestCase("v1.2.12.66233", "v1.2.12")]
    [TestCase("v1.4", null)]
    [TestCase("", null)]
    public void AVersion_IsNormalized(string version, string? expected)
    {
        Assert.That(GameVersionInference.Normalize(version), Is.EqualTo(expected));
    }

    /// <summary>Creates a temporary mock game directory structure containing <c>Version.xml</c> in the specified binary subdirectory.</summary>
    private static string Game(string binaries, string version)
    {
        var game = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "uix-version-" + Guid.NewGuid().ToString("N"))).FullName;
        var folder = Directory.CreateDirectory(Path.Combine(game, "bin", binaries)).FullName;
        File.WriteAllText(Path.Combine(folder, "Version.xml"), $"<Version>\n\t<Singleplayer Value=\"{version}\"/>\n</Version>");
        return game;
    }
}