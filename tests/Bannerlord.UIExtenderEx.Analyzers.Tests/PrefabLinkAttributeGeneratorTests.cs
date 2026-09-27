using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NUnit.Framework;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// <see cref="PrefabLinkAttributeGenerator"/>: <c>[assembly: PrefabLink]</c> for a mod whose UIExtenderEx has none, and
/// nothing for one whose UIExtenderEx or own code declares it.
/// </summary>
public class PrefabLinkAttributeGeneratorTests
{
    private const string Linked = """
        using Bannerlord.UIExtenderEx.Attributes;
        using TaleWorlds.Library;

        [assembly: PrefabLink(typeof(HostVM), typeof(HostVM))]
        [assembly: PrefabLink("OwnPrefab", typeof(HostVM))]

        public class HostVM : ViewModel { }
        """;

    private static CSharpCompilation Compile(string source, ImmutableArray<MetadataReference> references, LanguageVersion language = LanguageVersion.Latest) =>
        CSharpCompilation.Create("Mod",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(language))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static int GeneratedTrees(CSharpCompilation compilation) =>
        Verifier.WithGenerators(compilation).SyntaxTrees.Count() - compilation.SyntaxTrees.Count();

    private static void AssertCompiles(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
    }

    private static bool UIExtenderExHasIt() =>
        Compile("", Verifier.References).GetTypesByMetadataName(PrefabLinkAttributeGenerator.MetadataName).Any(t => t.ContainingAssembly.Name == "Bannerlord.UIExtenderEx");

    [Test]
    public void AUIExtenderExWithoutIt_GetsItFromThePackage()
    {
        Assume.That(UIExtenderExHasIt(), Is.False, "The UIExtenderEx the tests compile against declares [PrefabLink] itself");

        var compilation = Verifier.WithGenerators(Compile(Linked, Verifier.References));
        AssertCompiles(compilation);
        Assert.That(compilation.Assembly.GetAttributes().Count(a => a.AttributeClass?.Name == "PrefabLinkAttribute"), Is.EqualTo(2), "what the analyzers read");
    }

    /// <summary>Conditional: the links stay in the build, not in the mod's assembly.</summary>
    [Test]
    public void TheLinks_AreNotCompiledIntoTheAssembly()
    {
        Assume.That(UIExtenderExHasIt(), Is.False, "The UIExtenderEx the tests compile against declares [PrefabLink] itself");

        var compilation = Verifier.WithGenerators(Compile(Linked, Verifier.References));
        using var stream = new MemoryStream();
        Assert.That(compilation.Emit(stream).Success, Is.True);

        var reference = MetadataReference.CreateFromImage(stream.ToArray());
        var reader = CSharpCompilation.Create("Reader", [], Verifier.References.Add(reference));
        var emitted = (IAssemblySymbol) reader.GetAssemblyOrModuleSymbol(reference)!;
        Assert.That(emitted.GetAttributes().Where(a => a.AttributeClass?.Name == "PrefabLinkAttribute"), Is.Empty);
    }

    /// <summary>A mod targeting .NET Framework compiles as C# 7.3 unless it says otherwise.</summary>
    [Test]
    public void TheAttribute_CompilesAsCSharp73()
    {
        Assume.That(UIExtenderExHasIt(), Is.False, "The UIExtenderEx the tests compile against declares [PrefabLink] itself");

        var compilation = Compile(Linked, Verifier.References, LanguageVersion.CSharp7_3);
        Assert.That(GeneratedTrees(compilation), Is.EqualTo(1));
        AssertCompiles(Verifier.WithGenerators(compilation));
    }

    [Test]
    public void AUIExtenderExThatHasIt_GetsNothing()
    {
        // An assembly by UIExtenderEx's name that declares the attribute, as 3.0 does
        var uiExtenderEx = CSharpCompilation.Create("Bannerlord.UIExtenderEx",
            [CSharpSyntaxTree.ParseText("""
                namespace Bannerlord.UIExtenderEx.Attributes
                {
                    public sealed class ViewModelMixinAttribute : System.Attribute { }
                    public sealed class PrefabLinkAttribute : System.Attribute { }
                }
                """)],
            Verifier.References.Where(r => r.Display is not { } display || !display.EndsWith("Bannerlord.UIExtenderEx.dll", StringComparison.OrdinalIgnoreCase)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        Assert.That(uiExtenderEx.Emit(stream).Success, Is.True);
        var references = Verifier.References
            .Where(r => r.Display is not { } display || !display.EndsWith("Bannerlord.UIExtenderEx.dll", StringComparison.OrdinalIgnoreCase))
            .Append(MetadataReference.CreateFromImage(stream.ToArray()))
            .ToImmutableArray();

        Assert.That(GeneratedTrees(Compile("", references)), Is.Zero);
    }

    [Test]
    public void AModDeclaringItItself_GetsNothing()
    {
        var compilation = Compile("""
            namespace Bannerlord.UIExtenderEx.Attributes
            {
                internal sealed class PrefabLinkAttribute : System.Attribute { }
            }
            """, Verifier.References);
        Assert.That(GeneratedTrees(compilation), Is.Zero);
    }

    [Test]
    public void AProjectWithoutUIExtenderEx_GetsNothing()
    {
        var references = Verifier.References
            .Where(r => r.Display is not { } display || !display.EndsWith("Bannerlord.UIExtenderEx.dll", StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();
        Assert.That(GeneratedTrees(Compile("", references)), Is.Zero);
    }
}
