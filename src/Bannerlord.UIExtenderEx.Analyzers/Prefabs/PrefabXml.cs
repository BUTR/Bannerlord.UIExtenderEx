using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// Encapsulates parsed prefab XML content and maps XML line and column positions back to their original source locations,
/// whether originating from an MSBuild <c>AdditionalFiles</c> XML file or an embedded C# string literal.
/// </summary>
internal sealed class PrefabXml
{
    private readonly Func<int, int, Location> _locate;

    /// <summary>The parsed XML document, or <see langword="null"/> if the content is malformed; <see cref="Error"/> describes any syntax failure.</summary>
    public XDocument? Document { get; }

    public (string Message, Location Location)? Error { get; }

    /// <summary>The fallback source location representing the document as a whole (the file start or the enclosing string literal).</summary>
    public Location Location { get; }

    private PrefabXml(string text, Func<int, int, Location> locate, Location location)
    {
        _locate = locate;
        Location = location;
        try
        {
            Document = XDocument.Parse(text, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            Error = (exception.Message, locate(exception.LineNumber, exception.LinePosition));
        }
    }

    /// <summary>Computes the precise Roslyn <see cref="Location"/> corresponding to the beginning of an element or attribute name.</summary>
    public Location Locate(XObject node)
    {
        if (node is IXmlLineInfo { } info && info.HasLineInfo())
            return _locate(info.LineNumber, info.LinePosition);
        return Location;
    }

    /// <summary>Creates a <see cref="PrefabXml"/> instance from an XML additional file.</summary>
    public static PrefabXml FromFile(AdditionalText file, SourceText text)
    {
        var path = file.Path;
        return new PrefabXml(text.ToString(), (line, column) =>
        {
            if (line < 1 || line > text.Lines.Count)
                return Location.Create(path, default, default);
            var lineStart = text.Lines[line - 1].Start;
            var start = Math.Min(lineStart + Math.Max(column - 1, 0), text.Length);
            var end = Math.Min(start + WordLength(text, start), text.Length);
            var span = TextSpan.FromBounds(start, end);
            return Location.Create(path, span, text.Lines.GetLinePositionSpan(span));
        }, Location.Create(path, default, default));
    }

    /// <summary>
    /// Creates a <see cref="PrefabXml"/> instance from a C# string literal token, mapping internal XML offsets back
    /// through escape sequences to highlight exact tokens within source code.
    /// </summary>
    public static PrefabXml FromLiteral(SyntaxToken literal)
    {
        var decoded = literal.ValueText;
        var map = MapLiteral(literal);
        var tree = literal.SyntaxTree!;
        var source = tree.GetText();
        var whole = literal.GetLocation();
        return new PrefabXml(decoded, (line, column) =>
        {
            if (map is null)
                return whole;
            var offset = OffsetOf(decoded, line, column);
            if (offset < 0 || offset >= map.Count)
                return whole;
            var start = map[offset];
            var end = start;
            while (end < literal.Span.End - 1 && IsWordChar(source[end]))
                end++;
            return Location.Create(tree, TextSpan.FromBounds(start, Math.Max(end, start + 1)));
        }, whole);
    }

    private static int WordLength(SourceText text, int start)
    {
        var length = 0;
        while (start + length < text.Length && IsWordChar(text[start + length]))
            length++;
        return Math.Max(length, 1);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or ':';

    private static int OffsetOf(string text, int line, int column)
    {
        var currentLine = 1;
        var index = 0;
        while (currentLine < line && index < text.Length)
        {
            if (text[index] == '\n')
                currentLine++;
            index++;
        }
        return currentLine == line ? index + Math.Max(column - 1, 0) : -1;
    }

    /// <summary>Maps character indices within the literal's decoded string value back to source text offsets, or <see langword="null"/> if unmappable.</summary>
    private static List<int>? MapLiteral(SyntaxToken literal)
    {
        var text = literal.Text;
        var start = literal.SpanStart;
        var map = new List<int>();
        if (literal.IsKind(SyntaxKind.StringLiteralToken) && text.StartsWith("@\"", StringComparison.Ordinal))
        {
            for (var i = 2; i < text.Length - 1; i++)
            {
                map.Add(start + i);
                if (text[i] == '"' && i + 1 < text.Length - 1 && text[i + 1] == '"')
                    i++;
            }
            return map;
        }
        if (literal.IsKind(SyntaxKind.StringLiteralToken) && text.StartsWith("\"", StringComparison.Ordinal))
        {
            for (var i = 1; i < text.Length - 1; i++)
            {
                map.Add(start + i);
                if (text[i] != '\\' || i + 1 >= text.Length - 1)
                    continue;
                // Standard escapes span 2 characters, whereas unicode escapes (\uXXXX, \UXXXXXXXX, \xH..) require variable offsets.
                var next = text[i + 1];
                i += next switch
                {
                    'u' => 5,
                    'U' => 9,
                    'x' => 1 + CountHex(text, i + 2, 4),
                    _ => 1,
                };
            }
            return map;
        }
        return null;
    }

    private static int CountHex(string text, int start, int max)
    {
        var count = 0;
        while (count < max && start + count < text.Length && Uri.IsHexDigit(text[start + count]))
            count++;
        return count;
    }
}