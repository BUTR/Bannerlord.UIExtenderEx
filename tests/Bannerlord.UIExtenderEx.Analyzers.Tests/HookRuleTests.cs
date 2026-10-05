using NUnit.Framework;

using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.Verifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Tests diagnostics UIX0008 through UIX0010, enforcing method signatures and attributes
/// for <c>[BUTRViewModelOverride]</c> and <c>[BUTRUnsafeAccessor]</c>.
/// </summary>
public class HookRuleTests
{
    private const string Usings = """
        using System;
        using System.Runtime.CompilerServices;

        """;

    private const string HostVM = """
        public class HostVM : ViewModel
        {
            public void ExecuteDone() { }
            public void SetCategory(int index, string name) { }
            public int Count() => 0;
        }

        """;

    public class OverrideDoesNotMatch
    {
        [Test]
        public async Task WellFormedOverrides_ReportNothing()
        {
            await VerifyAsync(Usings + HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [BUTRViewModelOverride(nameof(HostVM.ExecuteDone))]
                    private void ExecuteDone(Action original) => original();

                    [BUTRViewModelOverride(nameof(HostVM.SetCategory))]
                    private void SetCategory(int index, string name, Action<int, string> original) => original(index, name);
                }
                """);
        }

        [Test]
        public async Task EachWayAnOverrideCanBeWrong_IsReported()
        {
            var messages = await MessagesAsync(Usings + HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [BUTRViewModelOverride(nameof(HostVM.ExecuteDone))]
                    private void NoOriginal() { }

                    [BUTRViewModelOverride(nameof(HostVM.SetCategory))]
                    private void WrongOriginal(int index, string name, Action<int> original) { }

                    [BUTRViewModelOverride("NoSuchMethod")]
                    private void Missing(Action original) { }

                    [BUTRViewModelOverride(nameof(HostVM.Count))]
                    private void NotVoid(Action original) { }
                }
                """);
            Assert.That(messages, Is.EquivalentTo(new[]
            {
                "UIX0008: Override 'NoOriginal' is not registered: its last parameter has to be the original, a delegate",
                "UIX0008: Override 'WrongOriginal' is not registered: its original has to be a delegate taking (int, string) and returning void",
                "UIX0008: Override 'Missing' is not registered: 'HostVM' has no instance method NoSuchMethod() returning void",
                "UIX0008: Override 'NotVoid' is not registered: 'HostVM' has no instance method Count() returning void",
            }));
        }

        /// <summary>Verifies that private game methods can be targeted by overrides because runtime lookup ignores member accessibility.</summary>
        [Test]
        public async Task AnOverrideOfAPrivateMethodOfTheGame_IsFound()
        {
            await VerifyAsync("""
                using System;

                [ViewModelMixin]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }

                    [BUTRViewModelOverride("CloseScreen")]
                    private void CloseScreen(Action original) => original();
                }
                """, gameSource: """
                public class GameVM : ViewModel
                {
                    private void CloseScreen() { }
                }
                """);
        }
    }

    public class Accessors
    {
        private const string Game = """
            public class GameBaseVM : ViewModel
            {
                private string _inherited = "";
            }
            public class GameVM : GameBaseVM
            {
                private static int _counter;
                private System.Collections.Generic.List<ViewModel> _categories = new();
                private string Combine(int number) => "";
                private static int Twice(int value) => value * 2;
            }
            """;

        [Test]
        public async Task StubsForPrivateMembersOfTheGame_ReportNothing()
        {
            await VerifyAsync(Usings + """
                public static class Stubs
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_categories")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref System.Collections.Generic.List<ViewModel> Categories(GameVM instance) => throw new NotImplementedException();

                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_inherited")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref string Inherited(GameVM instance) => throw new NotImplementedException();

                    [BUTRUnsafeAccessor(BUTRAccessorKind.Method)]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static string Combine(GameVM instance, int number) => throw new NotImplementedException();

                    [BUTRUnsafeAccessor(BUTRAccessorKind.StaticField, typeof(GameVM), Name = "_counter")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref int Counter() => throw new NotImplementedException();

                    [BUTRUnsafeAccessor(BUTRAccessorKind.StaticMethod, typeof(GameVM))]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static int Twice(int value) => throw new NotImplementedException();
                }
                """, gameSource: Game);
        }

        [Test]
        public async Task EachWayAStubCanBeWrong_IsReported()
        {
            var messages = await MessagesAsync(Usings + """
                public class Stubs
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_renamed")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref int Renamed(GameVM instance) => throw new NotImplementedException();

                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_categories")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref string WrongType(GameVM instance) => throw new NotImplementedException();

                    [BUTRUnsafeAccessor(BUTRAccessorKind.StaticField, Name = "_counter")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref int NoType() => throw new NotImplementedException();

                    [BUTRUnsafeAccessor(BUTRAccessorKind.Method, Name = "Combine")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static int WrongReturn(GameVM instance, int number) => throw new NotImplementedException();

                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_categories")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public ref System.Collections.Generic.List<ViewModel> NotStatic(GameVM instance) => throw new NotImplementedException();
                }
                """, gameSource: Game);
            Assert.That(messages, Is.EquivalentTo(new[]
            {
                "UIX0009: Accessor 'Renamed' is not resolved: 'GameVM' has no instance field _renamed; calling it throws",
                "UIX0009: Accessor 'WrongType' is not resolved: the stub has to return ref System.Collections.Generic.List<TaleWorlds.Library.ViewModel>; calling it throws",
                "UIX0009: Accessor 'NoType' is not resolved: a static member's stub names the type on the attribute: [BUTRUnsafeAccessor(kind, typeof(TheType))]; calling it throws",
                "UIX0009: Accessor 'WrongReturn' is not resolved: 'GameVM.Combine' returns string, the stub int; calling it throws",
                "UIX0009: Accessor 'NotStatic' is not resolved: the stub has to be static; calling it throws",
            }));
        }

        [Test]
        public async Task AStubWithoutNoInlining_IsReported()
        {
            await VerifyAsync(Usings + """
                public static class Stubs
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_categories")]
                    public static ref System.Collections.Generic.List<ViewModel> {|UIX0010:Categories|}(GameVM instance) => throw new NotImplementedException();
                }
                """, gameSource: Game);
        }
    }
}