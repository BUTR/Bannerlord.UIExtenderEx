using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

using System.Collections.Generic;
using System.IO;
using System.Linq;

using VerifyNUnit;

using VerifyTests;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// Every file the generator wrote for a movie, held to the copy under <c>Snapshots/</c>.
/// <para>
/// The other tests ask whether one line is there; this catches every line that changed, moved or appeared, which is what
/// a refactor that should not change the output has to show. A change that is meant shows up as a <c>.received.cs</c>
/// next to the <c>.verified.cs</c>: read the diff, and replace the verified file with the received one to accept it.
/// </para>
/// </summary>
internal static class GeneratedSnapshot
{
    public static SettingsTask Verify(IEnumerable<GeneratedSource> sources) =>
        Verifier.Verify(sources.Select(x => new Target("cs", x.Content, Path.GetFileNameWithoutExtension(x.FileName))));
}
