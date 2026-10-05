using System;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// Generated C#, split into what the compiler sees and what the generator only left as a note.
/// <para>
/// The generator records everything it could not emit as a comment, and those comments repeat the assignment it skipped.
/// A plain substring search over the whole file therefore cannot tell "this binding works" from "this binding was dropped",
/// which is exactly the distinction these tests are about.
/// </para>
/// </summary>
internal sealed class GeneratedCode
{
    private readonly string[] _lines;

    public string Text { get; }

    public GeneratedCode(string text)
    {
        Text = text;
        _lines = [.. text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0)];
    }

    /// <summary>Lines the compiler acts on.</summary>
    public IReadOnlyList<string> Statements => [.. _lines.Where(x => !x.StartsWith("//", StringComparison.Ordinal))];

    /// <summary>Lines the generator left as a note about something it skipped.</summary>
    public IReadOnlyList<string> Comments => [.. _lines.Where(x => x.StartsWith("//", StringComparison.Ordinal))];

    public bool HasStatement(string fragment) => Statements.Any(x => x.Contains(fragment));

    public bool HasComment(string fragment) => Comments.Any(x => x.Contains(fragment));

    /// <summary>How many times a statement appears; tells one generated class from several.</summary>
    public int CountStatements(string fragment) => Statements.Count(x => x.Contains(fragment));

    public override string ToString() => Text;
}
