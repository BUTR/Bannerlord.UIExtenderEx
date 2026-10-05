using Microsoft.CodeAnalysis;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Tests MSBuild integration targets (<c>Bannerlord.UIExtenderEx.Analyzers.targets</c>) by invoking MSBuild on mock projects,
/// verifying generated <c>.editorconfig</c> metadata, package tagging, and game version property propagation.
/// </summary>
public class TargetsTests
{
    private const string Package = "Fake.GUI";

    [Test]
    public void TheGamesFiles_ReachTheCompilerTaggedWithTheirPackage_AndTheModsDoNot_AndTheGameVersionsReachIt()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "uix-targets-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            // Simulates format 3 package layout: build/<id>.props exposing all gui/ items under the BannerlordGameGuiData item type
            Write(root, "pkg/build/Fake.GUI.props", $"""
                <Project>
                  <ItemGroup>
                    <BannerlordGameGuiData Include="$(MSBuildThisFileDirectory)../gui/**/*.json" Visible="false" Package="{Package}" />
                  </ItemGroup>
                </Project>
                """);
            Write(root, "pkg/gui/manifest.json", "{}");
            Write(root, "pkg/gui/Native/GUI/Prefabs/GameMovie.json", "{}");

            // Simulates mod project layout with GUI/Prefabs and package props imported ahead of analyzer targets as in NuGet restores
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
                  <!-- After the targets, as Bannerlord.BUTRModule.Sdk sets them in its Sdk.targets, from supported-game-versions.txt -->
                  <PropertyGroup>
                    <GameVersion>1.4.8</GameVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <SGVItem Include="v1.4.8" />
                    <SGVItem Include="v1.3.4" />
                  </ItemGroup>
                </Project>
                """);

            Run(Path.Combine(root, "mod"), "msbuild Mod.csproj -restore -t:GenerateMSBuildEditorConfigFile -nologo -v:q");

            var path = Path.Combine(root, "mod", "obj", "Debug", "netstandard2.0", "Mod.GeneratedMSBuildEditorConfig.editorconfig");
            var global = GlobalOptions(path);
            Assert.That(global["build_property.GameVersion"], Is.EqualTo("1.4.8"), "The game version, set after the targets");
            Assert.That(global["build_property.UIExtenderExGameVersions"], Is.EqualTo("v1.4.8,v1.3.4"), "The supported versions, from the SDK's items");
            var sections = Sections(File.ReadAllLines(path));
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
                // Ignore transient file locking on build servers; directory is in temp storage
            }
        }
    }

    /// <summary>
    /// Verifies that semicolon-separated lists of game versions configured in project files are converted to comma-separated
    /// lists in generated <c>.editorconfig</c> files, preventing semicolons from being misinterpreted as comment delimiters.
    /// </summary>
    [Test]
    public void SupportedVersionsSetByHand_ReachTheCompilerWhole()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "uix-targets-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var targets = Path.Combine(TestContext.CurrentContext.TestDirectory, "Packaging", "Bannerlord.UIExtenderEx.Analyzers.targets");
            Write(root, "mod/Mod.csproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                    <UIExtenderExGameVersions>v1.2.12;v1.3.4;v1.4.8</UIExtenderExGameVersions>
                  </PropertyGroup>
                  <Import Project="{targets}" />
                </Project>
                """);
            Run(Path.Combine(root, "mod"), "msbuild Mod.csproj -restore -t:GenerateMSBuildEditorConfigFile -nologo -v:q");
            var global = GlobalOptions(Path.Combine(root, "mod", "obj", "Debug", "netstandard2.0", "Mod.GeneratedMSBuildEditorConfig.editorconfig"));
            Assert.That(global["build_property.UIExtenderExGameVersions"], Is.EqualTo("v1.2.12,v1.3.4,v1.4.8"));
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // Ignore transient file locking on build servers; directory is in temp storage
            }
        }
    }

    /// <summary>
    /// Verifies that projects omitting explicit game versions infer the version from referenced game directory binaries,
    /// while explicitly declared versions take precedence.
    /// </summary>
    [Test]
    public void WithoutAVersion_TheGameVersionIsInferredFromTheReferences()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "uix-targets-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            // Simulates game installation layout with TaleWorlds.Library.dll and adjacent Version.xml
            var binaries = Path.Combine(root, "game", "bin", "Win64_Shipping_Client");
            Directory.CreateDirectory(binaries);
            File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "References", "Game", "TaleWorlds.Library.dll"), Path.Combine(binaries, "TaleWorlds.Library.dll"));
            Write(root, "game/bin/Win64_Shipping_Client/Version.xml", "<Version>\n\t<Singleplayer Value=\"v1.2.12.66233\"/>\n</Version>");

            var targets = Path.Combine(TestContext.CurrentContext.TestDirectory, "Packaging", "Bannerlord.UIExtenderEx.Analyzers.targets");
            string Project(string properties) => $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                    {properties}
                  </PropertyGroup>
                  <ItemGroup>
                    <Reference Include="TaleWorlds.Library" HintPath="..\game\bin\Win64_Shipping_Client\TaleWorlds.Library.dll" />
                  </ItemGroup>
                  <Import Project="{targets}" />
                </Project>
                """;
            Write(root, "inferred/Mod.csproj", Project(""));
            Write(root, "named/Mod.csproj", Project("<GameVersion>1.4.8</GameVersion>"));

            Run(Path.Combine(root, "inferred"), "msbuild Mod.csproj -restore -t:GenerateMSBuildEditorConfigFile -nologo -v:q");
            Run(Path.Combine(root, "named"), "msbuild Mod.csproj -restore -t:GenerateMSBuildEditorConfigFile -nologo -v:q");

            var inferred = GlobalOptions(Path.Combine(root, "inferred", "obj", "Debug", "netstandard2.0", "Mod.GeneratedMSBuildEditorConfig.editorconfig"));
            Assert.That(inferred["build_property.UIExtenderExInferredGameVersion"], Is.EqualTo("v1.2.12"), "From the game folder's Version.xml");
            var named = GlobalOptions(Path.Combine(root, "named", "obj", "Debug", "netstandard2.0", "Mod.GeneratedMSBuildEditorConfig.editorconfig"));
            Assert.That(named.TryGetValue("build_property.UIExtenderExInferredGameVersion", out var notInferred) ? notInferred : "", Is.Empty, "Not inferred when named");
            Assert.That(named["build_property.GameVersion"], Is.EqualTo("1.4.8"));
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // Ignore transient file locking on build servers; directory is in temp storage
            }
        }
    }

    /// <summary>Parses generated editorconfig global properties using Roslyn's <see cref="AnalyzerConfig"/> parser.</summary>
    private static IReadOnlyDictionary<string, string> GlobalOptions(string path)
    {
        var config = AnalyzerConfig.Parse(File.ReadAllText(path), path);
        return AnalyzerConfigSet.Create(new[] { config }).GlobalConfigOptions.AnalyzerOptions;
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
        // Strip test-runner environment variables that would redirect child MSBuild execution to host SDK internals
        foreach (var name in new[] { "MSBuildExtensionsPath", "MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBuildLoadMicrosoftTargetsReadOnly" })
            start.Environment.Remove(name);
        // Disable MSBuild node reuse to prevent process locking on task assemblies across test runs
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Assert.That(process.ExitCode, Is.Zero, $"dotnet {arguments} failed:{Environment.NewLine}{output.Result}{error.Result}");
    }

    /// <summary>Parses editorconfig sections into a dictionary keyed by file pattern header.</summary>
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