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
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Compiles a mod's source in memory and checks the analyzer's reports against the markup in it. A span written
/// <c>{|UIX0001:Name|}</c> expects that rule on exactly that text; any report the markup does not name fails the test,
/// and so does a compiler error, so every case is code that builds.
/// <para>
/// The mod compiles with public metadata only, as csc does: the analyzer has to find private members of referenced
/// ViewModels on its own. A test that needs a ViewModel in a referenced assembly rather than in the mod passes its source
/// as <c>gameSource</c>; it is compiled into an assembly of its own first.
/// </para>
/// </summary>
internal static class Verifier
{
    private const string Usings = """
        using Bannerlord.UIExtenderEx.Attributes;
        using Bannerlord.UIExtenderEx.ViewModels;
        using TaleWorlds.Library;

        """;

    private static readonly Regex Markup = new(@"\{\|(?<id>[A-Z]+\d+):(?<text>.*?)\|\}", RegexOptions.Singleline);

    private static readonly Lazy<ImmutableArray<MetadataReference>> BaseReferences = new(() =>
    {
        var root = Path.Combine(TestContext.CurrentContext.TestDirectory, "References");
        var references = Directory.EnumerateFiles(Path.Combine(root, "Framework"), "*.dll")
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "Game"), "*.dll"))
            .Append(Path.Combine(TestContext.CurrentContext.TestDirectory, "Bannerlord.UIExtenderEx.dll"))
            .Select(path => (MetadataReference) MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
        return NewerApi.For(references) is { } newerApi ? references.Add(newerApi) : references;
    });

    /// <summary>
    /// What the code under test compiles against: .NET Framework 4.7.2, the game, UIExtenderEx, and the attributes of a
    /// newer UIExtenderEx that this one does not have (<see cref="NewerApi"/>).
    /// </summary>
    public static ImmutableArray<MetadataReference> References => BaseReferences.Value;

    /// <summary>The version of the UIExtenderEx the tests compile against, which some reports depend on.</summary>
    public static Version UIExtenderExVersion { get; } =
        System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(TestContext.CurrentContext.TestDirectory, "Bannerlord.UIExtenderEx.dll")).Version!;

    /// <summary>
    /// The compilation after the analyzer package's generators have run, as the compiler runs them before the analyzers.
    /// </summary>
    public static CSharpCompilation WithGenerators(CSharpCompilation compilation)
    {
        CSharpGeneratorDriver.Create(new PrefabLinkAttributeGenerator())
            .WithUpdatedParseOptions((CSharpParseOptions) compilation.SyntaxTrees.First().Options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        return (CSharpCompilation) updated;
    }

    public static async Task VerifyAsync(string markup, string? gameSource = null)
    {
        var (source, expected) = Parse(Usings + markup);

        var references = BaseReferences.Value;
        if (gameSource is not null)
            references = references.Add(CompileGameAssembly(gameSource));

        var compilation = WithGenerators(CSharpCompilation.Create("Mod",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), "Mod.cs") },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.Public)));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, "The test's code does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));

        var actual = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MixinAnalyzer(), new UnsafeAccessorAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        var actualSet = actual.Select(d => (d.Id, d.Location.SourceSpan)).ToList();
        var missing = expected.Where(e => !actualSet.Contains(e)).ToList();
        var unexpected = actual.Where(d => !expected.Contains((d.Id, d.Location.SourceSpan))).ToList();
        if (missing.Count == 0 && unexpected.Count == 0)
            return;

        var text = SourceText.From(source);
        var message = new StringBuilder();
        foreach (var (id, span) in missing)
            message.AppendLine($"Expected {id} on '{text.ToString(span)}' at {text.Lines.GetLinePosition(span.Start)}, not reported.");
        foreach (var diagnostic in unexpected)
            message.AppendLine($"Unexpected {diagnostic.Id} on '{text.ToString(diagnostic.Location.SourceSpan)}' at {diagnostic.Location.GetLineSpan().StartLinePosition}: {diagnostic.GetMessage()}");
        Assert.Fail(message.ToString());
    }

    /// <summary>The messages of every report, for the tests that check what a report says.</summary>
    public static async Task<IReadOnlyList<string>> MessagesAsync(string source, string? gameSource = null)
    {
        var references = BaseReferences.Value;
        if (gameSource is not null)
            references = references.Add(CompileGameAssembly(gameSource));
        var compilation = WithGenerators(CSharpCompilation.Create("Mod",
            new[] { CSharpSyntaxTree.ParseText(Usings + source, new CSharpParseOptions(LanguageVersion.Latest)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.Public)));
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MixinAnalyzer(), new UnsafeAccessorAnalyzer())).GetAnalyzerDiagnosticsAsync();
        return diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}").ToList();
    }

    /// <summary>A referenced assembly compiled from <paramref name="source"/>, for a ViewModel whose private members the mod cannot see.</summary>
    public static MetadataReference CompileGameAssembly(string source)
    {
        var compilation = CSharpCompilation.Create("Game",
            new[] { CSharpSyntaxTree.ParseText("using TaleWorlds.Library;\n" + source, new CSharpParseOptions(LanguageVersion.Latest)) },
            BaseReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.That(result.Success, Is.True, "The game assembly does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static (string Source, HashSet<(string Id, TextSpan Span)> Expected) Parse(string markup)
    {
        var expected = new HashSet<(string, TextSpan)>();
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
}
