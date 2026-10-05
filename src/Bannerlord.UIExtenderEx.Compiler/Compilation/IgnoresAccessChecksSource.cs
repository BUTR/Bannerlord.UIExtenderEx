using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

/// <summary>
/// Generates C# declarations for <c>[assembly: IgnoresAccessChecksTo("...")]</c> attributes and their supporting attribute definition,
/// allowing compiled prefab code to access internal mixin and view model types across module assembly boundaries.
/// </summary>
public static class IgnoresAccessChecksSource
{
    public const string FileName = "IgnoresAccessChecks.gen.cs";

    private static readonly string[] FrameworkPrefixes = ["mscorlib", "netstandard", "System", "Microsoft", "WindowsBase"];

    /// <summary>
    /// Generates source code declaring <c>IgnoresAccessChecksToAttribute</c> for each non-framework referenced assembly.
    /// </summary>
    /// <param name="referencePaths">The file paths of the referenced assemblies.</param>
    /// <returns>A <see cref="GeneratedSource"/> containing the attribute declarations.</returns>
    public static GeneratedSource Create(IEnumerable<string> referencePaths)
    {
        var names = referencePaths
            .Select(Path.GetFileNameWithoutExtension)
            .Where(x => !string.IsNullOrEmpty(x) && !IsFramework(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        // Assembly attributes must precede every other declaration in the file
        foreach (var name in names)
            sb.AppendLine("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"" + name + "\")]");
        sb.AppendLine();
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("\t[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]");
        sb.AppendLine("\tinternal sealed class IgnoresAccessChecksToAttribute : Attribute");
        sb.AppendLine("\t{");
        sb.AppendLine("\t\tpublic IgnoresAccessChecksToAttribute(string assemblyName)");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\tAssemblyName = assemblyName;");
        sb.AppendLine("\t\t}");
        sb.AppendLine();
        sb.AppendLine("\t\tpublic string AssemblyName { get; private set; }");
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        return new(FileName, sb.ToString());
    }

    /// <summary>
    /// Determines whether the specified assembly name belongs to standard .NET framework libraries.
    /// </summary>
    public static bool IsFramework(string assemblyName) =>
        FrameworkPrefixes.Any(x => assemblyName.Equals(x, StringComparison.OrdinalIgnoreCase) || assemblyName.StartsWith(x + ".", StringComparison.OrdinalIgnoreCase));
}