using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Provides Roslyn test verification infrastructure for prefab and XML content analyzers, validating diagnostic IDs
/// and spans in C# and XML files annotated with <c>{|UIX0012:Name|}</c> markup syntax.
/// </summary>
internal static class PrefabVerifier
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.Xml;
        using Bannerlord.UIExtenderEx.Attributes;
        using Bannerlord.UIExtenderEx.Prefabs2;
        using Bannerlord.UIExtenderEx.ViewModels;
        using TaleWorlds.Library;

        """;

    private static readonly Regex Markup = new(@"\{\|(?<id>[A-Z]+\d+):(?<text>.*?)\|\}", RegexOptions.Singleline);

    public static Task VerifyAsync(string csharpMarkup, params (string Path, string XmlMarkup)[] files) =>
        VerifyAsync(csharpMarkup, [], files);

    /// <summary>Verifies diagnostics with mock GUI bundle packages, simulating MSBuild targets that tag additional files with package metadata.</summary>
    public static Task VerifyAsync(string csharpMarkup, ITestPackage[] games, params (string Path, string XmlMarkup)[] files) =>
        VerifyAsync(csharpMarkup, games, new Dictionary<string, string>(), files);

    /// <summary>Verifies diagnostics with mock GUI packages and MSBuild property configuration exposed through compiler options.</summary>
    public static async Task VerifyAsync(string csharpMarkup, ITestPackage[] games, IReadOnlyDictionary<string, string> properties, params (string Path, string XmlMarkup)[] files)
    {
        var expected = new HashSet<(string Id, string Path, TextSpan Span)>();
        var (source, csharpExpected) = Parse(Usings + csharpMarkup);
        foreach (var (id, span) in csharpExpected)
            expected.Add((id, "Mod.cs", span));

        var additional = new List<AdditionalText>();
        foreach (var (path, xmlMarkup) in files)
        {
            var (xml, xmlExpected) = Parse(xmlMarkup);
            additional.Add(new InMemoryText(path, xml));
            foreach (var (id, span) in xmlExpected)
                expected.Add((id, path, span));
        }
        var (gameFiles, options) = Game(games, properties);
        additional.AddRange(gameFiles);

        var compilation = Verifier.WithGenerators(CSharpCompilation.Create("Mod",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), "Mod.cs") },
            Verifier.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.Public)));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, "The test's code does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));

        var actual = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new PrefabAnalyzer(), new PrefabContentAnalyzer()), new AnalyzerOptions(additional.ToImmutableArray(), options))
            .GetAnalyzerDiagnosticsAsync();

        var found = actual.Select(d => (d.Id, Path: PathOf(d), d.Location.SourceSpan, Diagnostic: d)).ToList();
        var missing = expected.Where(e => !found.Any(f => f.Id == e.Id && f.Path == e.Path && f.SourceSpan == e.Span)).ToList();
        var unexpected = found.Where(f => !expected.Contains((f.Id, f.Path, f.SourceSpan))).ToList();
        if (missing.Count == 0 && unexpected.Count == 0)
            return;

        var message = new StringBuilder();
        foreach (var (id, path, span) in missing)
            message.AppendLine($"Expected {id} in {path} at {span}, not reported.");
        foreach (var (id, path, span, diagnostic) in unexpected)
            message.AppendLine($"Unexpected {id} in {path} at {span}: {diagnostic.GetMessage()}");
        Assert.Fail(message.ToString());
    }

    /// <summary>Executes analyzers on C# source and returns formatted diagnostic messages for exact text assertions.</summary>
    public static Task<IReadOnlyList<string>> MessagesAsync(string csharp, params ITestPackage[] games) =>
        MessagesAsync(csharp, new Dictionary<string, string>(), games);

    /// <summary>Executes analyzers on C# source with MSBuild property configuration and returns formatted diagnostic messages.</summary>
    public static async Task<IReadOnlyList<string>> MessagesAsync(string csharp, IReadOnlyDictionary<string, string> properties, params ITestPackage[] games)
    {
        var (gameFiles, options) = Game(games, properties);
        var compilation = Verifier.WithGenerators(CSharpCompilation.Create("Mod",
            new[] { CSharpSyntaxTree.ParseText(Usings + csharp, new CSharpParseOptions(LanguageVersion.Latest)) },
            Verifier.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.Public)));
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new PrefabAnalyzer(), new PrefabContentAnalyzer()), new AnalyzerOptions(gameFiles.ToImmutableArray<AdditionalText>(), options)).GetAnalyzerDiagnosticsAsync();
        return diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}").ToList();
    }

    /// <summary>Constructs mock additional files and analyzer config options representing GUI bundles and project build properties.</summary>
    internal static (List<AdditionalText> Files, AnalyzerConfigOptionsProvider Options) Game(ITestPackage[] games, IReadOnlyDictionary<string, string>? properties = null)
    {
        var files = new List<AdditionalText>();
        var packages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, text, package) in games.SelectMany(g => g.Files()))
        {
            files.Add(new InMemoryText(path, text));
            packages[path] = package;
        }
        return (files, new MetadataOptionsProvider(packages, properties ?? new Dictionary<string, string>()));
    }

    /// <summary>Supplies analyzer configuration options mimicking MSBuild compiler-visible items and build properties.</summary>
    private sealed class MetadataOptionsProvider(Dictionary<string, string> packages, IReadOnlyDictionary<string, string> properties) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new BuildProperties(properties);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            packages.TryGetValue(textFile.Path, out var package) ? new Options(package) : Options.Empty;
    }

    private sealed class Options(string? package) : AnalyzerConfigOptions
    {
        public static readonly Options Empty = new(null);

        public override bool TryGetValue(string key, out string value)
        {
            value = package ?? "";
            return package is not null && key == "build_metadata.AdditionalFiles.UIExtenderExGamePackage";
        }
    }

    private sealed class BuildProperties(IReadOnlyDictionary<string, string> properties) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            const string prefix = "build_property.";
            value = "";
            return key.StartsWith(prefix, StringComparison.Ordinal) && properties.TryGetValue(key.Substring(prefix.Length), out value!);
        }
    }

    private static string PathOf(Diagnostic diagnostic) => diagnostic.Location.Kind switch
    {
        LocationKind.SourceFile => Path.GetFileName(diagnostic.Location.SourceTree!.FilePath),
        _ => diagnostic.Location.GetLineSpan().Path,
    };

    private static (string Source, List<(string Id, TextSpan Span)> Expected) Parse(string markup)
    {
        var expected = new List<(string, TextSpan)>();
        var source = new StringBuilder();
        var last = 0;
        foreach (Match match in Markup.Matches(markup))
        {
            source.Append(markup, last, match.Index - last);
            var text = match.Groups["text"].Value;
            expected.Add((match.Groups["id"].Value, new TextSpan(source.Length, text.Length)));
            source.Append(text);
            last = match.Index + match.Length;
        }
        source.Append(markup, last, markup.Length - last);
        return (source.ToString(), expected);
    }

    private sealed class InMemoryText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryText(string path, string text)
        {
            Path = path;
            _text = SourceText.From(text);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}