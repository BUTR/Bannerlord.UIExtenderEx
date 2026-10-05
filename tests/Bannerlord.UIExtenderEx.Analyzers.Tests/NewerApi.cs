using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NUnit.Framework;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Synthesizes in-memory reference metadata for UIExtenderEx 3.0 attributes analyzed by rules when compiling against earlier
/// UIExtenderEx assemblies that lack them.
/// </summary>
/// <remarks>
/// Builds a standalone in-memory assembly representing consumer-facing attributes (such as <c>[BUTRUnsafeAccessor]</c> and
/// <c>[BUTRViewModelOverride]</c>). The analyzer package supplies <c>[PrefabLink]</c> separately via
/// <see cref="PrefabLinkAttributeGenerator"/>.
/// </remarks>
internal static class NewerApi
{
    private const string Source = """
        using System;

        namespace Bannerlord.UIExtenderEx.Attributes
        {
            public enum BUTRAccessorKind
            {
                Method,
                Field,
                StaticMethod,
                StaticField,
            }

            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            public sealed class BUTRUnsafeAccessorAttribute : Attribute
            {
                public BUTRAccessorKind Kind { get; }
                public Type? Type { get; }
                public string? Name { get; set; }

                public BUTRUnsafeAccessorAttribute(BUTRAccessorKind kind) => Kind = kind;

                public BUTRUnsafeAccessorAttribute(BUTRAccessorKind kind, Type type)
                {
                    Kind = kind;
                    Type = type;
                }
            }

            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            public sealed class BUTRViewModelOverrideAttribute : Attribute
            {
                public string MethodName { get; }

                public BUTRViewModelOverrideAttribute(string methodName) => MethodName = methodName;
            }
        }
        """;

    /// <summary>
    /// Creates an in-memory metadata reference containing synthetic UIExtenderEx 3.0 attributes, or returns
    /// <see langword="null"/> if the specified compilation <paramref name="references"/> already define them.
    /// </summary>
    public static MetadataReference? For(ImmutableArray<MetadataReference> references)
    {
        var probe = CSharpCompilation.Create("Probe", [], references);
        if (probe.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.BUTRViewModelOverrideAttribute") is not null)
            return null;

        var compilation = CSharpCompilation.Create("Bannerlord.UIExtenderEx.NewerApi",
            [CSharpSyntaxTree.ParseText(Source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.That(result.Success, Is.True, "The newer API does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}