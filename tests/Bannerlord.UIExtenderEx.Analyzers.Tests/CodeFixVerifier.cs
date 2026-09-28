using Bannerlord.UIExtenderEx.Analyzers.CodeFixes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Runs every analyzer over a mod's C# and XML files in a workspace, applies the fix with the given title to the first
/// report of a rule, and compares every document with what it should read afterwards. The code has to compile before the
/// fix and after it.
/// </summary>
internal static class CodeFixVerifier
{
    public const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.Runtime.CompilerServices;
        using System.Xml;
        using Bannerlord.UIExtenderEx.Attributes;
        using Bannerlord.UIExtenderEx.Prefabs2;
        using Bannerlord.UIExtenderEx.ViewModels;
        using TaleWorlds.Library;

        """;

    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
        new MixinAnalyzer(), new UnsafeAccessorAnalyzer(), new PrefabAnalyzer(), new PrefabContentAnalyzer());

    private static readonly ImmutableArray<CodeFixProvider> Providers = ImmutableArray.Create<CodeFixProvider>(
        new MixinCodeFixProvider(), new UnsafeAccessorCodeFixProvider(), new PrefabContentCodeFixProvider(),
        new PrefabLinkCodeFixProvider(), new PrefabXmlCodeFixProvider());

    /// <summary>
    /// A fix to the mod's C#; <paramref name="before"/> and <paramref name="after"/> both follow <see cref="Usings"/>, or
    /// with <paramref name="usings"/> false, are whole files.
    /// </summary>
    public static Task VerifyAsync(string id, string before, string after, string title, string? gameSource = null, bool usings = true) =>
        VerifyAsync(id, before, after, [], title, gameSource, usings);

    /// <summary>
    /// A fix to the mod's C# or one of its XML files, each given as it reads before and after. With
    /// <paramref name="gameVersion"/>, the project builds against that game version, as the analyzer targets pass it on.
    /// </summary>
    public static async Task VerifyAsync(string id, string before, string after, (string Path, string Before, string After)[] files, string title,
        string? gameSource = null, bool usings = true, string? gameVersion = null)
    {
        var header = usings ? Usings : "";
        var (solution, documentId, fileIds) = CreateSolution(header + before, files.Select(f => (f.Path, f.Before)), gameSource, gameVersion);
        var actions = await FixesAsync(solution, id, gameVersion);
        var action = actions.FirstOrDefault(a => a.Title == title);
        Assert.That(action, Is.Not.Null, $"No fix titled '{title}' for {id}; offered: {string.Join(", ", actions.Select(a => $"'{a.Title}'"))}");

        var operations = await action!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        var csharp = (await changed.GetDocument(documentId)!.GetTextAsync()).ToString();
        Assert.That(Normalize(csharp), Is.EqualTo(Normalize(header + after)), "The C# after the fix:" + Environment.NewLine + csharp);
        for (var i = 0; i < files.Length; i++)
        {
            var xml = (await changed.GetAdditionalDocument(fileIds[i])!.GetTextAsync()).ToString();
            Assert.That(Normalize(xml), Is.EqualTo(Normalize(files[i].After)), $"{files[i].Path} after the fix:" + Environment.NewLine + xml);
        }

        var compilation = (await changed.GetProject(documentId.ProjectId)!.GetCompilationAsync())!;
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, "The code does not compile after the fix:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    /// <summary>The titles of the fixes offered for the first report of a rule; empty when there is none.</summary>
    public static async Task<IReadOnlyList<string>> TitlesAsync(string id, string before, params (string Path, string Text)[] files)
    {
        var (solution, _, _) = CreateSolution(Usings + before, files, null, null);
        return (await FixesAsync(solution, id)).Select(a => a.Title).ToList();
    }

    private static async Task<List<CodeAction>> FixesAsync(Solution solution, string id, string? gameVersion = null)
    {
        var project = solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, "The test's code does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));

        var diagnostics = await compilation.WithAnalyzers(Analyzers, project.AnalyzerOptions).GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics
            .Where(d => d.Id == id)
            .OrderBy(d => d.Location.GetLineSpan().Path, StringComparer.Ordinal)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .FirstOrDefault();
        Assert.That(diagnostic, Is.Not.Null, $"{id} is not reported; reported: {string.Join(", ", diagnostics.Select(d => d.Id))}");
        if (gameVersion is not null)
            Assert.That(diagnostic!.GetMessage(), Does.StartWith($"[v{gameVersion}] "), "The report names the game version");

        TextDocument document = diagnostic!.Location.IsInSource
            ? solution.GetDocument(diagnostic.Location.SourceTree)!
            : project.AdditionalDocuments.Single(d => d.FilePath == diagnostic.Location.GetLineSpan().Path);

        var actions = new List<CodeAction>();
        var context = document is Document source
            ? new CodeFixContext(source, diagnostic, (action, _) => actions.Add(action), CancellationToken.None)
            : new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        foreach (var provider in Providers.Where(p => p.FixableDiagnosticIds.Contains(id)))
            await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    private static (Solution Solution, DocumentId Document, List<DocumentId> Files) CreateSolution(string source, IEnumerable<(string Path, string Text)> files,
        string? gameSource, string? gameVersion)
    {
        var references = Verifier.References;
        if (gameSource is not null)
            references = references.Add(Verifier.CompileGameAssembly(gameSource));

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "Mod", "Mod", LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.Public),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: references,
            analyzerReferences: [new Generators()]));

        var documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(documentId, "Mod.cs", SourceText.From(source), filePath: "Mod.cs");
        var fileIds = new List<DocumentId>();
        foreach (var (path, text) in files)
        {
            var fileId = DocumentId.CreateNewId(projectId);
            solution = solution.AddAdditionalDocument(fileId, Path.GetFileName(path), SourceText.From(text), filePath: path);
            fileIds.Add(fileId);
        }
        // The property as the generated editorconfig carries it: a global section of build properties
        if (gameVersion is not null)
        {
            solution = solution.AddAnalyzerConfigDocument(DocumentId.CreateNewId(projectId), ".globalconfig",
                SourceText.From($"is_global = true\nbuild_property.GameVersion = {gameVersion}\n"), filePath: Path.Combine(Path.GetTempPath(), ".globalconfig"));
        }
        return (solution, documentId, fileIds);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    /// <summary>The analyzer package's generators, which the workspace runs before handing out a compilation.</summary>
    private sealed class Generators : AnalyzerReference
    {
        public override string? FullPath => null;

        public override object Id => typeof(PrefabLinkAttributeGenerator);

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => ImmutableArray<DiagnosticAnalyzer>.Empty;

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => ImmutableArray<DiagnosticAnalyzer>.Empty;

        // One instance for the life of the reference: the workspace keeps each generator's state by the instance
        private static readonly ImmutableArray<ISourceGenerator> Instances = ImmutableArray.Create(new PrefabLinkAttributeGenerator().AsSourceGenerator());

        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => Instances;

        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) => GetGeneratorsForAllLanguages();
    }
}
