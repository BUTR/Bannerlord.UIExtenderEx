using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// A piece of prefab XML and the way back from a position in it to a position in what the author wrote: a prefab file
/// handed to the compiler as an additional file, or a string literal in a patch class.
/// </summary>
internal sealed class PrefabXml
{
    private readonly Func<int, int, Location> _locate;

    /// <summary>The document, or null when it is not well-formed; <see cref="Error"/> then says why and where.</summary>
    public XDocument? Document { get; }

    public (string Message, Location Location)? Error { get; }

    /// <summary>What reports about the whole piece point at: the file's start, or the literal.</summary>
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

    /// <summary>Where an element's name or an attribute's name starts.</summary>
    public Location Locate(XObject node)
    {
        if (node is IXmlLineInfo { } info && info.HasLineInfo())
            return _locate(info.LineNumber, info.LinePosition);
        return Location;
    }

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
    /// XML written as a C# string literal. Positions are mapped through the literal's escapes, so a report lands on the
    /// characters that were written; a literal whose content cannot be mapped (a raw or interpolated string) reports on the
    /// literal as a whole.
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

    /// <summary>For each character of the literal's value, where it starts in the source; null when that cannot be told.</summary>
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
                // \uXXXX and \xH..HHHH are longer than two characters; the rest are two
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
