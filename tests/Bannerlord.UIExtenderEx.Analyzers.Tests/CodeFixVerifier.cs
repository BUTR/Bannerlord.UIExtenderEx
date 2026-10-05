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
/// Runs diagnostic analyzers against module C# and Gauntlet XML documents within an in-memory workspace, executes the code
/// fix matching a specified title against the first reported diagnostic, and verifies post-transformation document state and
/// compilation validity.
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
    /// Verifies a code fix targeting module C# source.
    /// </summary>
    /// <param name="id">The diagnostic rule ID to trigger.</param>
    /// <param name="before">The initial C# code before applying the fix.</param>
    /// <param name="after">The expected C# code after applying the fix.</param>
    /// <param name="title">The title of the code action to execute.</param>
    /// <param name="gameSource">Optional external game assembly source.</param>
    /// <param name="usings">Whether standard using directives should be automatically prepended.</param>
    public static Task VerifyAsync(string id, string before, string after, string title, string? gameSource = null, bool usings = true) =>
        VerifyAsync(id, before, after, [], title, gameSource, usings);

    /// <summary>
    /// Verifies a code fix targeting module C# source or associated additional Gauntlet XML files against expected post-fix states.
    /// </summary>
    /// <param name="id">The diagnostic rule ID to trigger.</param>
    /// <param name="before">The initial C# code before applying the fix.</param>
    /// <param name="after">The expected C# code after applying the fix.</param>
    /// <param name="files">Additional document files and their respective before/after contents.</param>
    /// <param name="title">The title of the code action to execute.</param>
    /// <param name="gameSource">Optional external game assembly source.</param>
    /// <param name="usings">Whether standard using directives should be automatically prepended.</param>
    /// <param name="gameVersion">The target game version configuration string.</param>
    /// <param name="game">Optional mock or real game package provider.</param>
    public static async Task VerifyAsync(string id, string before, string after, (string Path, string Before, string After)[] files, string title,
        string? gameSource = null, bool usings = true, string? gameVersion = null, Game? game = null)
    {
        var header = usings ? Usings : "";
        var (solution, documentId, fileIds) = CreateSolution(header + before, files.Select(f => (f.Path, f.Before)), gameSource, gameVersion);
        var actions = await FixesAsync(solution, id, gameVersion, game);
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

    /// <summary>
    /// Retrieves the titles of all code actions registered for the first diagnostic report of rule <paramref name="id"/>.
    /// </summary>
    public static Task<IReadOnlyList<string>> TitlesAsync(string id, string before, params (string Path, string Text)[] files) =>
        TitlesAsync(id, before, null, files);

    /// <summary>
    /// Retrieves the titles of all code actions registered for the first diagnostic report of rule <paramref name="id"/>,
    /// evaluating against the specified game packages and build properties.
    /// </summary>
    public static async Task<IReadOnlyList<string>> TitlesAsync(string id, string before, Game? game, params (string Path, string Text)[] files)
    {
        var (solution, _, _) = CreateSolution(Usings + before, files, null, null);
        return (await FixesAsync(solution, id, game: game)).Select(a => a.Title).ToList();
    }

    /// <summary>
    /// Represents mock game GUI packages and analyzer build properties passed into the workspace options.
    /// </summary>
    public sealed class Game(ITestPackage[] packages, IReadOnlyDictionary<string, string> properties)
    {
        public ITestPackage[] Packages { get; } = packages;

        public IReadOnlyDictionary<string, string> Properties { get; } = properties;
    }

    private static async Task<List<CodeAction>> FixesAsync(Solution solution, string id, string? gameVersion = null, Game? game = null)
    {
        var project = solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, "The test's code does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));

        var options = project.AnalyzerOptions;
        if (game is not null)
        {
            var (files, provider) = PrefabVerifier.Game(game.Packages, game.Properties);
            options = new AnalyzerOptions(options.AdditionalFiles.AddRange(files), provider);
        }
        var diagnostics = await compilation.WithAnalyzers(Analyzers, options).GetAnalyzerDiagnosticsAsync();
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

    /// <summary>
    /// Analyzer reference wrapper providing source generator instances to the test workspace compilation pipeline.
    /// </summary>
    private sealed class Generators : AnalyzerReference
    {
        public override string? FullPath => null;

        public override object Id => typeof(PrefabLinkAttributeGenerator);

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => ImmutableArray<DiagnosticAnalyzer>.Empty;

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => ImmutableArray<DiagnosticAnalyzer>.Empty;

        // Maintains a single generator instance across the reference lifetime to preserve generator state within the workspace
        private static readonly ImmutableArray<ISourceGenerator> Instances = ImmutableArray.Create(new PrefabLinkAttributeGenerator().AsSourceGenerator());

        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => Instances;

        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) => GetGeneratorsForAllLanguages();
    }
}