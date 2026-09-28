using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// The analyzer targets, run by MSBuild on a project that references a GUI package the way NuGet imports one. The
/// prefab tests hand the analyzer each game file's package directly, so only this shows whether the targets do: a
/// metadata reference the targets evaluate to nothing once let the game's XML be checked as the mod's own.
/// </summary>
public class TargetsTests
{
    private const string Package = "Fake.GUI";

    [Test]
    public void TheGamesFiles_ReachTheCompilerTaggedWithTheirPackage_AndTheModsDoNot_AndTheGameVersionReachesIt()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "uix-targets-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            // The package as format 2 lays it out: build/<id>.props exposing everything under gui/, the prefab trees
            // below it, as one item type
            Write(root, "pkg/build/Fake.GUI.props", $"""
                <Project>
                  <ItemGroup>
                    <BannerlordGameGuiData Include="$(MSBuildThisFileDirectory)../gui/**/*.json" Visible="false" Package="{Package}" />
                  </ItemGroup>
                </Project>
                """);
            Write(root, "pkg/gui/manifest.json", "{}");
            Write(root, "pkg/gui/Native/GUI/Prefabs/GameMovie.json", "{}");

            // The mod: its own prefab under GUI, and the package's props imported before the analyzer targets, as NuGet orders them
            Write(root, "mod/GUI/Prefabs/ModMovie.xml", "<Prefab />");
            var targets = Path.Combine(TestContext.CurrentContext.TestDirectory, "Packaging", "Bannerlord.UIExtenderEx.Analyzers.targets");
            Write(root, "mod/Mod.csproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="..\pkg\build\Fake.GUI.props" />
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                  </PropertyGroup>
                  <Import Project="{targets}" />
                  <!-- After the targets, as Bannerlord.BUTRModule.Sdk sets it in its Sdk.targets -->
                  <PropertyGroup>
                    <GameVersion>1.4.8</GameVersion>
                  </PropertyGroup>
                </Project>
                """);

            Run(Path.Combine(root, "mod"), "msbuild Mod.csproj -restore -t:GenerateMSBuildEditorConfigFile -nologo -v:q");

            var editorconfig = File.ReadAllLines(Path.Combine(root, "mod", "obj", "Debug", "netstandard2.0", "Mod.GeneratedMSBuildEditorConfig.editorconfig"));
            Assert.That(editorconfig.TakeWhile(l => !l.StartsWith("[", StringComparison.Ordinal)).Select(l => l.Trim()),
                Has.Member("build_property.GameVersion = 1.4.8"), "The game version, set after the targets");
            var sections = Sections(editorconfig);
            Assert.That(PackageOf(sections, "GameMovie.json"), Is.EqualTo(Package), "The game's prefab tree");
            Assert.That(PackageOf(sections, "manifest.json"), Is.EqualTo(Package), "The game's data");
            Assert.That(sections.Keys.Any(k => k.EndsWith("ModMovie.xml", StringComparison.Ordinal)), Is.True, "The mod's prefab is an additional file");
            Assert.That(PackageOf(sections, "ModMovie.xml"), Is.Empty, "The mod's prefab is not the game's");
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // A build server may still hold a file; the folder is under the temp path
            }
        }
    }

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void Run(string directory, string arguments)
    {
        var start = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // The test host's own MSBuild settings would point the child build at the host's SDK internals
        foreach (var name in new[] { "MSBuildExtensionsPath", "MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBuildLoadMicrosoftTargetsReadOnly" })
            start.Environment.Remove(name);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Assert.That(process.ExitCode, Is.Zero, $"dotnet {arguments} failed:{Environment.NewLine}{output.Result}{error.Result}");
    }

    /// <summary>The editorconfig's sections, by the file path in their header.</summary>
    private static Dictionary<string, List<string>> Sections(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                result[line.Substring(1, line.Length - 2)] = current = [];
            else
                current?.Add(line);
        }
        return result;
    }

    private static string PackageOf(Dictionary<string, List<string>> sections, string fileName)
    {
        const string key = "build_metadata.AdditionalFiles.UIExtenderExGamePackage = ";
        var section = sections.FirstOrDefault(x => x.Key.EndsWith("/" + fileName, StringComparison.Ordinal)).Value ?? [];
        return section.FirstOrDefault(l => l.StartsWith(key, StringComparison.Ordinal))?.Substring(key.Length).Trim() ?? "";
    }
}
