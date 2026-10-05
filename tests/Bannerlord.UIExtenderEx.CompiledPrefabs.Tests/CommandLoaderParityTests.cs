using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies behavioral parity between compiled command execution and the XML interpreted command dispatch pipeline.
/// <para>
/// Dispatches widget commands through compiled movies and compares invocation logs and exception handling directly
/// against <see cref="ViewModel.ExecuteCommand"/> on identical ViewModel instances.
/// </para>
/// </summary>
public class CommandLoaderParityTests
{
    private const string Movie = "CommandParityMovie";

    private const string Prefab = """
<Prefab>
  <Window>
    <Widget Id="Root">
      <Children>
        <Widget Id="NoArgs" Command.Click="ExecuteNoArgs" />
        <Widget Id="NoArgsWithParameter" Command.Click="ExecuteNoArgs" CommandParameter.Click="surplus" />
        <Widget Id="Int" Command.Click="ExecuteWithInt" />
        <Widget Id="IntLiteral" Command.Click="ExecuteWithInt" CommandParameter.Click="42" />
        <Widget Id="Float" Command.Click="ExecuteWithFloat" />
        <Widget Id="FloatLiteral" Command.Click="ExecuteWithFloat" CommandParameter.Click="0.5" />
        <Widget Id="String" Command.Click="ExecuteWithString" />
        <Widget Id="EnumLiteral" Command.Click="ExecuteWithEnum" CommandParameter.Click="Second" />
        <Widget Id="BoolLiteral" Command.Click="ExecuteWithBool" CommandParameter.Click="true" />
        <Widget Id="ViewModel" Command.Click="ExecuteWithViewModel" />
        <Widget Id="Two" Command.Click="ExecuteWithTwo" />
        <Widget Id="Throws" Command.Click="ExecuteThrows" />
        <Widget Id="Walked" Command.Click="Options\ExecuteReset" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    /// <summary>Represents a widget parameter swapped for its underlying data source during command invocation.</summary>
    private sealed class WidgetArgument
    {
        public static readonly WidgetArgument Instance = new();
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        TestCaseData Case(string id, string method, string? parameter, params object?[] arguments) =>
            new TestCaseData(id, method, parameter, arguments).SetArgDisplayNames(id, string.Join(", ", arguments.Select(x => x switch
            {
                null => "null",
                WidgetArgument => "widget",
                string text => $"\"{text}\"",
                _ => $"{x} ({x.GetType().Name})",
            })));

        yield return Case("NoArgs", "ExecuteNoArgs", null);
        yield return Case("NoArgs", "ExecuteNoArgs", null, 1, "extra");
        yield return Case("NoArgsWithParameter", "ExecuteNoArgs", "surplus");
        yield return Case("Int", "ExecuteWithInt", null);
        yield return Case("Int", "ExecuteWithInt", null, 7);
        yield return Case("Int", "ExecuteWithInt", null, "42");
        yield return Case("Int", "ExecuteWithInt", null, 1.5f);
        yield return Case("Int", "ExecuteWithInt", null, (object?) null);
        yield return Case("Int", "ExecuteWithInt", null, 1, 2);
        yield return Case("IntLiteral", "ExecuteWithInt", "42");
        yield return Case("IntLiteral", "ExecuteWithInt", "42", 5);
        yield return Case("Float", "ExecuteWithFloat", null, "0.5");
        yield return Case("Float", "ExecuteWithFloat", null, 2);
        yield return Case("FloatLiteral", "ExecuteWithFloat", "0.5");
        yield return Case("String", "ExecuteWithString", null, "text");
        yield return Case("String", "ExecuteWithString", null, 5);
        yield return Case("String", "ExecuteWithString", null, WidgetArgument.Instance);
        yield return Case("EnumLiteral", "ExecuteWithEnum", "Second");
        yield return Case("BoolLiteral", "ExecuteWithBool", "true");
        yield return Case("ViewModel", "ExecuteWithViewModel", null, WidgetArgument.Instance);
        yield return Case("ViewModel", "ExecuteWithViewModel", null, "text");
        yield return Case("ViewModel", "ExecuteWithViewModel", null, new object());
        yield return Case("Two", "ExecuteWithTwo", null, 1);
        yield return Case("Two", "ExecuteWithTwo", null, 1, "second");
        yield return Case("Throws", "ExecuteThrows", null);
    }

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private CultureInfo _previousCulture = null!;

    [SetUp]
    public void SetUp()
    {
        // Configures invariant culture so float conversions behave consistently across both test pipelines.
        _previousCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        _workspace = new PrefabWorkspace((Movie, Prefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(CommandLoaderParityTests));
    }

    [TearDown]
    public void TearDown()
    {
        _ui.Dispose();
        _workspace.Dispose();
        Thread.CurrentThread.CurrentCulture = _previousCulture;
    }

    [TestCaseSource(nameof(Cases))]
    public void AResolvedCommand_RunsExactlyAsExecuteCommandRunsIt(string id, string method, string? parameter, object?[] arguments)
    {
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(TypedCommandVM), _ui);
        var compiled = new TypedCommandVM();
        movie.SetDataSource(compiled);
        var compiledFailure = Capture(() => CompiledMovie.FireEvent(movie.ById(id), "Click",
            [.. arguments.Select(x => x is WidgetArgument ? movie.ById("Root") : x)]));

        // Prepares baseline arguments by replacing widget parameters with data sources and appending literal command parameters.
        var oracle = new TypedCommandVM();
        var prepared = arguments.Select(x => x is WidgetArgument ? oracle : x).ToList();
        if (parameter is not null)
            prepared.Add(parameter);
        var oracleFailure = Capture(() => oracle.ExecuteCommand(method, [.. prepared]));

        Assert.That(compiled.Calls, Is.EqualTo(oracle.Calls));
        Assert.That(Describe(Unwrap(compiledFailure)), Is.EqualTo(Describe(oracleFailure)));
    }

    /// <summary>
    /// Verifies that commands targeting unbound nested data sources navigate property paths dynamically by name.
    /// </summary>
    [Test]
    public void ACommandOnADataSourceNoScopeHolds_IsWalkedToByName()
    {
        var movie = CompiledMovie.Build(_workspace, Movie, typeof(TypedCommandVM), _ui);
        var viewModel = new TypedCommandVM();
        movie.SetDataSource(viewModel);

        CompiledMovie.FireEvent(movie.ById("Walked"), "Click");

        Assert.That(viewModel.Options.Calls, Is.EqualTo(new[] { "Reset" }));
    }

    private static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Unwraps target invocation exceptions resulting from reflection invocation of <see cref="Widget.EventFired"/>.
    /// </summary>
    private static Exception? Unwrap(Exception? exception) =>
        exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

    private static string Describe(Exception? exception) => exception is null
        ? "nothing thrown"
        : exception.GetType().Name + (exception.InnerException is { } inner ? $" around {Describe(inner)}" : "");
}

public enum CommandChoice
{
    First,
    Second,
}

/// <summary>Provides test command methods covering diverse parameter signature conversions, recording invocation calls.</summary>
public class TypedCommandVM : ViewModel
{
    public List<string> Calls { get; } = [];

    public CommandOptionsVM Options { get; } = new();

    public void ExecuteNoArgs() => Calls.Add("NoArgs");

    public void ExecuteWithInt(int value) => Calls.Add($"Int {value}");

    public void ExecuteWithFloat(float value) => Calls.Add($"Float {value.ToString(CultureInfo.InvariantCulture)}");

    public void ExecuteWithString(string? value) => Calls.Add($"String {value ?? "<null>"}");

    public void ExecuteWithEnum(CommandChoice value) => Calls.Add($"Enum {value}");

    public void ExecuteWithBool(bool value) => Calls.Add($"Bool {value}");

    public void ExecuteWithViewModel(ViewModel? value) => Calls.Add($"ViewModel {value?.GetType().Name ?? "<null>"}");

    public void ExecuteWithTwo(int first, string? second) => Calls.Add($"Two {first} {second ?? "<null>"}");

    public void ExecuteThrows() => throw new InvalidOperationException("thrown by the command");
}

public class CommandOptionsVM : ViewModel
{
    public List<string> Calls { get; } = [];

    public void ExecuteReset() => Calls.Add("Reset");
}
