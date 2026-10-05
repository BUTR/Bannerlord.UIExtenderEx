using Bannerlord.UIExtenderEx.Tests;

using NUnit.Framework;

/// <summary>
/// Initializes UIExtenderEx prior to executing test fixtures via <see cref="TestRuntime.Start"/>.
/// <para>
/// Omits a namespace declaration to ensure the <see cref="SetUpFixtureAttribute"/> applies globally across the entire test assembly.
/// </para>
/// </summary>
[SetUpFixture]
public class CompiledPrefabsBootstrap
{
    [OneTimeSetUp]
    public void Start() => TestRuntime.Start();
}
