using Bannerlord.UIExtenderEx.Tests;

using NUnit.Framework;

using VerifyNUnit;

/// <summary>
/// Starts UIExtenderEx before any test runs (<see cref="TestRuntime"/>), and keeps every snapshot under <c>Snapshots/</c>.
/// <para>
/// No namespace on purpose: an NUnit <see cref="SetUpFixtureAttribute"/> outside one covers the whole assembly.
/// </para>
/// </summary>
[SetUpFixture]
public class CodeGeneratorBootstrap
{
    [OneTimeSetUp]
    public void Start()
    {
        Verifier.UseProjectRelativeDirectory("Snapshots");
        TestRuntime.Start();
    }
}
