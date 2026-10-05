using Bannerlord.UIExtenderEx.Tests;

using NUnit.Framework;

/// <summary>
/// Initializes UIExtenderEx prior to executing any corpus test suites (<see cref="TestRuntime"/>), ensuring
/// consistent runtime state across both main and chunk worker processes.
/// <para>
/// Omits a namespace intentionally so that the <see cref="SetUpFixtureAttribute"/> applies globally across the entire assembly.
/// </para>
/// </summary>
[SetUpFixture]
public class CorpusBootstrap
{
    [OneTimeSetUp]
    public void Start() => TestRuntime.Start();
}