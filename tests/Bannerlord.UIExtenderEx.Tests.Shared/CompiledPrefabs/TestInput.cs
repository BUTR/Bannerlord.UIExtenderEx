using NSubstitute;

using TaleWorlds.InputSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Gives the game's static <see cref="Input"/> a substitute input manager, once per process.
/// <para>
/// In v1.3.14 and before, the <c>EventManager</c> constructor asks the input manager whether a controller is connected,
/// so a <c>UIContext</c> cannot be initialized before this has run.
/// </para>
/// </summary>
public static class TestInput
{
    static TestInput() => Input.Initialize(Substitute.For<IInputManager>(), Substitute.For<IInputContext>());

    /// <summary>Runs the static constructor, if it has not run yet.</summary>
    public static void EnsureInitialized() { }
}