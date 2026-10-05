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
/// Compiles module source in memory and validates diagnostic analyzer reports against embedded test markup spans.
/// </summary>
/// <remarks>
/// Markup spans formatted as <c>{|UIX0001:Name|}</c> assert that diagnostic <c>UIX0001</c> is reported on that exact text range.
/// Unmatched diagnostics or unexpected compiler errors fail the test, ensuring all test fixtures produce compiling code.
/// <para>
/// Compilations import public metadata only (matching standard <c>csc</c> behavior), requiring analyzers to inspect private
/// members of referenced ViewModels via complete symbol compilation views. Tests requiring ViewModels in referenced assemblies
/// pass <c>gameSource</c> to compile an independent referenced assembly first.
/// </para>
/// <para>
/// Does not depend on <c>Microsoft.CodeAnalysis.Testing</c>, avoiding runtime NuGet assembly downloads in favor of static
/// reference assemblies copied at build time.
/// </para>
/// </remarks>
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
    /// Gets the base metadata references required by test compilations (.NET Framework 4.7.2, Bannerlord game libraries,
    /// UIExtenderEx, and synthetic attributes from <see cref="NewerApi"/>).
    /// </summary>
    public static ImmutableArray<MetadataReference> References => BaseReferences.Value;

    /// <summary>Gets the assembly version of the referenced UIExtenderEx instance used by test compilations.</summary>
    public static Version UIExtenderExVersion { get; } =
        System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(TestContext.CurrentContext.TestDirectory, "Bannerlord.UIExtenderEx.dll")).Version!;

    /// <summary>
    /// Executes source generators (e.g. <see cref="PrefabLinkAttributeGenerator"/>) against the compilation, matching compiler execution order.
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

    /// <summary>
    /// Compiles test source and retrieves formatted diagnostic report strings (<c>Id: Message</c>).
    /// </summary>
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

    /// <summary>
    /// Compiles <paramref name="source"/> into a standalone external game assembly reference, modeling private members hidden from consumer compilations.
    /// </summary>
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