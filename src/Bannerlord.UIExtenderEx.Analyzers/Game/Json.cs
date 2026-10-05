using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>
/// Lightweight standalone JSON parser tailored for GUI package manifests and metadata tables.
/// Avoids external dependencies such as <c>System.Text.Json</c> to prevent assembly loading conflicts in Roslyn analyzer hosts.
/// Deserializes JSON objects as <see cref="IReadOnlyDictionary{TKey,TValue}"/>, arrays as <see cref="List{T}"/>, and numbers as <see cref="double"/>.
/// </summary>
internal static class Json
{
    public static object? Parse(string text)
    {
        var index = 0;
        SkipWhitespace(text, ref index);
        var value = ReadValue(text, ref index);
        SkipWhitespace(text, ref index);
        if (index != text.Length)
            throw new FormatException($"Unexpected '{text[index]}' at {index}");
        return value;
    }

    public static string? String(this IReadOnlyDictionary<string, object?> obj, string key) =>
        obj.TryGetValue(key, out var value) ? value as string : null;

    public static bool Bool(this IReadOnlyDictionary<string, object?> obj, string key, bool fallback) =>
        obj.TryGetValue(key, out var value) && value is bool b ? b : fallback;

    public static IEnumerable<IReadOnlyDictionary<string, object?>> Objects(this IReadOnlyDictionary<string, object?> obj, string key)
    {
        if (!obj.TryGetValue(key, out var value) || value is not List<object?> list)
            yield break;
        foreach (var item in list)
        {
            if (item is IReadOnlyDictionary<string, object?> entry)
                yield return entry;
        }
    }

    private static object? ReadValue(string text, ref int index)
    {
        if (index >= text.Length)
            throw new FormatException("Unexpected end");
        switch (text[index])
        {
            case '{':
                return ReadObject(text, ref index);
            case '[':
                return ReadArray(text, ref index);
            case '"':
                return ReadString(text, ref index);
            case 't':
                Expect(text, ref index, "true");
                return true;
            case 'f':
                Expect(text, ref index, "false");
                return false;
            case 'n':
                Expect(text, ref index, "null");
                return null;
            default:
                return ReadNumber(text, ref index);
        }
    }

    private static Dictionary<string, object?> ReadObject(string text, ref int index)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        index++;
        SkipWhitespace(text, ref index);
        if (Peek(text, index) == '}')
        {
            index++;
            return result;
        }
        while (true)
        {
            SkipWhitespace(text, ref index);
            var key = ReadString(text, ref index);
            SkipWhitespace(text, ref index);
            Expect(text, ref index, ":");
            SkipWhitespace(text, ref index);
            result[key] = ReadValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (Peek(text, index) == ',')
            {
                index++;
                continue;
            }
            Expect(text, ref index, "}");
            return result;
        }
    }

    private static List<object?> ReadArray(string text, ref int index)
    {
        var result = new List<object?>();
        index++;
        SkipWhitespace(text, ref index);
        if (Peek(text, index) == ']')
        {
            index++;
            return result;
        }
        while (true)
        {
            SkipWhitespace(text, ref index);
            result.Add(ReadValue(text, ref index));
            SkipWhitespace(text, ref index);
            if (Peek(text, index) == ',')
            {
                index++;
                continue;
            }
            Expect(text, ref index, "]");
            return result;
        }
    }

    private static string ReadString(string text, ref int index)
    {
        Expect(text, ref index, "\"");
        var builder = new StringBuilder();
        while (index < text.Length)
        {
            var c = text[index++];
            if (c == '"')
                return builder.ToString();
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }
            if (index >= text.Length)
                break;
            var escaped = text[index++];
            switch (escaped)
            {
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    if (index + 4 > text.Length)
                        throw new FormatException("Truncated \\u escape");
                    builder.Append((char) int.Parse(text.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    index += 4;
                    break;
                default: builder.Append(escaped); break;
            }
        }
        throw new FormatException("Unterminated string");
    }

    private static double ReadNumber(string text, ref int index)
    {
        var start = index;
        while (index < text.Length && (char.IsDigit(text[index]) || text[index] is '-' or '+' or '.' or 'e' or 'E'))
            index++;
        if (start == index)
            throw new FormatException($"Unexpected '{text[index]}' at {index}");
        return double.Parse(text.Substring(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static void Expect(string text, ref int index, string token)
    {
        if (string.CompareOrdinal(text, index, token, 0, token.Length) != 0)
            throw new FormatException($"Expected '{token}' at {index}");
        index += token.Length;
    }

    private static char Peek(string text, int index) => index < text.Length ? text[index] : '\0';

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == '﻿'))
            index++;
    }
}