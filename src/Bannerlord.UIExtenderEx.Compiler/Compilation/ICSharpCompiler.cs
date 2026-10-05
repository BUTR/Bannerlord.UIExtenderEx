using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

/// <summary>Represents a generated C# source file with its file name and textual content.</summary>
public sealed class GeneratedSource
{
    public string FileName { get; }
    public string Content { get; }

    public GeneratedSource(string fileName, string content)
    {
        FileName = fileName;
        Content = content;
    }
}

/// <summary>Represents the outcome of an in-memory Roslyn compilation, containing the resulting assembly bytes or error diagnostics.</summary>
public sealed class CompilationResult
{
    public bool Success => Assembly is not null;
    public byte[]? Assembly { get; }
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets the assembly reference paths resolved and referenced in the compiled assembly metadata.
    /// </summary>
    public IReadOnlyList<string> UsedReferencePaths { get; }

    private CompilationResult(byte[]? assembly, IReadOnlyList<string> errors, IReadOnlyList<string> usedReferencePaths)
    {
        Assembly = assembly;
        Errors = errors;
        UsedReferencePaths = usedReferencePaths;
    }

    public static CompilationResult Succeeded(byte[] assembly, IReadOnlyList<string>? usedReferencePaths = null) => new(assembly, [], usedReferencePaths ?? []);
    public static CompilationResult Failed(IReadOnlyList<string> errors) => new(null, errors, []);
}

/// <summary>
/// Compiles generated prefab C# source files into an in-memory assembly. Implementations must be thread-safe for background worker invocation.
/// </summary>
public interface ICSharpCompiler
{
    /// <summary>Gets the human-readable identifier of the compiler implementation.</summary>
    string Name { get; }

    /// <summary>Compiles the specified C# sources against the provided assembly reference paths.</summary>
    /// <param name="assemblyName">The output assembly name.</param>
    /// <param name="sources">The collection of generated C# source files.</param>
    /// <param name="referencePaths">The collection of assembly reference file paths.</param>
    /// <returns>A <see cref="CompilationResult"/> indicating success or failure.</returns>
    CompilationResult Compile(string assemblyName, IReadOnlyList<GeneratedSource> sources, IReadOnlyList<string> referencePaths);

    /// <summary>
    /// Warms up compiler components (JIT, metadata tables) in advance of runtime compilation requests. Safe to invoke from worker threads.
    /// </summary>
    /// <param name="referencePaths">The initial assembly reference paths to warm up.</param>
    void WarmUp(IReadOnlyList<string> referencePaths);
}