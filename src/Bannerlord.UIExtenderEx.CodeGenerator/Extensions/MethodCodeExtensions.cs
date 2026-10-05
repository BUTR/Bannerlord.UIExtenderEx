using System;

using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;

/// <summary>
/// Provides code emission helper extensions for <see cref="MethodCode"/>.
/// </summary>
internal static class MethodCodeExtensions
{
    /// <summary>
    /// Emits a braced block with an optional control header statement (e.g. <c>if</c>, <c>for</c>, <c>try</c>).
    /// </summary>
    /// <param name="methodCode">The method code generator to append to.</param>
    /// <param name="header">The optional header statement, or <see langword="null"/> for a standalone scope block.</param>
    /// <param name="body">An action that emits statements within the block.</param>
    public static void AddBlock(this MethodCode methodCode, string? header, Action body)
    {
        if (header is not null)
        {
            methodCode.AddLine(header);
        }
        methodCode.AddLine("{");
        body();
        methodCode.AddLine("}");
    }
}