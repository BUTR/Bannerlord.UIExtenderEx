using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// <c>Command.X</c> and the <c>CommandParameter.X</c> that goes with it.
/// <para>
/// The XML loader resolves these in <c>PrefabDatabindingExtension.AfterAttributesSet</c> by walking the commands and
/// looking each parameter up, so a parameter is only ever seen through the command it belongs to, and one that resolves
/// to nothing leaves the command taking its argument from the fired event instead. These tests hold the generator to
/// that, because a movie is expected to behave the same whether it was compiled or read from XML.
/// </para>
/// </summary>
public class CommandBindingTests
{
    private const string MovieName = "CommandMovie";

    private const string Prefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <ButtonWidget Id=""Plain"" Command.Click=""ExecuteNoArgs"" />
        <ButtonWidget Id=""Literal"" Command.Click=""ExecuteWithTag"" CommandParameter.Click=""literal"" />
        <CommandPart Parameter.Tag=""passed"" />
        <CommandPart />
      </Children>
    </Widget>
  </Window>
</Prefab>";

    private const string PartPrefab = @"
<Prefab>
  <Parameters><Parameter Name=""Tag"" DefaultValue=""fromDefault"" /></Parameters>
  <Window>
    <ButtonWidget Command.Click=""ExecuteWithTag"" CommandParameter.Click=""*Tag"" />
  </Window>
</Prefab>";

    private PrefabWorkspace _workspace = null!;

    [SetUp]
    public void SetUp() => _workspace = new PrefabWorkspace((MovieName, Prefab), ("CommandPart", PartPrefab));

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    private GeneratedCode Generate() => Generate(_workspace, MovieName);

    private static GeneratedCode Generate(PrefabWorkspace workspace, string movieName) => new(workspace
        .Generate(movieName, typeof(CommandVM))
        .Single(x => x.FileName == movieName + ".gen.cs").Content);

    [Test]
    public void ACommandWithoutArguments_IsCalledDirectly()
    {
        Assert.That(Generate().HasStatement("_datasource_Root.ExecuteNoArgs();"), Is.True);
    }

    [Test]
    public void ALiteralCommandParameter_BecomesTheArgument()
    {
        // Appended after the event's own arguments, as the string it was written as: GauntletView.OnCommand appends it
        // so, and ExecuteCommand converts it from there
        var code = Generate();

        Assert.That(code.HasStatement("arguments[args.Length] = \"literal\";"), Is.True);
        Assert.That(code.HasStatement("_datasource_Root.ExecuteWithTag((global::System.String)commandArgument0);"), Is.True);
    }

    [Test]
    public void ACommandParameterTakenFromAPassedParameter_BecomesTheArgument()
    {
        Assert.That(Generate().HasStatement("arguments[args.Length] = \"passed\";"), Is.True);
    }

    [Test]
    public void ACommandParameterWhosePrefabParameterWasNotPassed_LeavesTheArgumentToTheEvent()
    {
        // The XML loader only consults the parameters the enclosing prefab passed in; a declared DefaultValue is a
        // default for the prefab's own attributes, not an argument to hand a command. With nothing passed, the command
        // is bound without a parameter and takes its argument from the event, exactly as if CommandParameter were absent.
        var code = Generate();

        Assert.That(code.HasStatement("\"fromDefault\""), Is.False);
        Assert.That(code.HasStatement("var arguments = new global::System.Object[args.Length];"), Is.True);
    }

    [Test]
    public void ACommandParameterOnAMethodWithoutParameters_IsIgnoredAsExecuteCommandIgnoresIt()
    {
        // ExecuteCommand calls a method without parameters whatever it was handed. The game's generator read the type
        // of the parameter the method does not have, and took the whole movie down with an ArgumentOutOfRangeException.
        using var workspace = new PrefabWorkspace(("SurplusMovie", @"
<Prefab>
  <Window>
    <ButtonWidget Command.Click=""ExecuteNoArgs"" CommandParameter.Click=""surplus"" />
  </Window>
</Prefab>"));

        var code = Generate(workspace, "SurplusMovie");

        Assert.That(code.HasStatement("_datasource_Root.ExecuteNoArgs();"), Is.True);
    }

    [Test]
    public void AParameterAMovieEscapes_IsWrittenAsALiteral()
    {
        // Command names and parameters are XML; a quote or a backslash in one used to end the literal it was pasted into
        using var workspace = new PrefabWorkspace(("QuotedMovie", @"
<Prefab>
  <Window>
    <ButtonWidget Command.Click=""ExecuteWithTag"" CommandParameter.Click=""say &quot;hi&quot; C:\temp"" />
  </Window>
</Prefab>"));

        var code = Generate(workspace, "QuotedMovie");

        Assert.That(code.HasStatement(@"arguments[args.Length] = ""say \""hi\"" C:\\temp"";"), Is.True);
    }

    [Test]
    public void ACommandParameterNamingNoCommand_IsIgnored()
    {
        // Reachable from patched XML, and valid to the loader, which never looks at it. The generator used to index its
        // command table with the parameter's name and take the whole movie down with a KeyNotFoundException.
        using var workspace = new PrefabWorkspace(("OrphanMovie", @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <ButtonWidget Id=""Orphan"" CommandParameter.Click=""stray"" />
      </Children>
    </Widget>
  </Window>
</Prefab>"));

        Assert.That(() => Generate(workspace, "OrphanMovie"), Throws.Nothing);
        Assert.That(Generate(workspace, "OrphanMovie").HasStatement("stray"), Is.False);
    }

    [Test]
    public void AnUnknownCommand_IsRunByName()
    {
        using var workspace = new PrefabWorkspace(("UnknownCommandMovie", @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <ButtonWidget Command.Click=""NoSuchCommand"" />
      </Children>
    </Widget>
  </Window>
</Prefab>"));

        var code = Generate(workspace, "UnknownCommandMovie");

        // A command ExecuteCommand may still find on the instance: it looks up non-public methods and walks the base
        // types, so a name the generator cannot resolve is not a name that cannot run
        Assert.That(code.HasStatement("DynamicMember.Execute(_datasource_Root, \"NoSuchCommand\", arguments)"), Is.True);
        Assert.That(code.HasStatement("_datasource_Root.NoSuchCommand("), Is.False, "never a typed call to a member that does not exist");
    }

    // --- the whole output ----------------------------------------------------------------------------------------

    /// <summary>Every file generated for the movie, line for line; see <see cref="GeneratedSnapshot"/>.</summary>
    [Test]
    public Task Snapshot() => GeneratedSnapshot.Verify(_workspace.Generate(MovieName, typeof(CommandVM)));
}
