using NUnit.Framework;

using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.Verifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

public class MixinAnalyzerTests
{
    /// <summary>A ViewModel in the mod itself, where every member is visible anyway.</summary>
    private const string HostVM = """
        public class HostVM : ViewModel
        {
            public string Name { get; set; } = "";
            public void ExecuteDone() { }
            public override void RefreshValues() { }
        }

        """;

    [Test]
    public async Task ATypicalMixin_ReportsNothing()
    {
        await VerifyAsync(HostVM + """
            [ViewModelMixin(nameof(HostVM.RefreshValues))]
            public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
            {
                public HostVMMixin(HostVM vm) : base(vm) { }

                [DataSourceProperty] public string MyMod_Label { get; set; } = "";
                [DataSourceProperty] public bool MyMod_IsVisible { get; private set; }
                [DataSourceMethod] public void ExecuteMyModAction() { }

                public override void OnRefresh() { }
            }
            """);
    }

    [Test]
    public async Task ACompilationWithoutUIExtenderExTypesInUse_ReportsNothing()
    {
        await VerifyAsync("""
            public class NotAMixin : ViewModel
            {
                [DataSourceProperty] private int Hidden { get; set; }
            }
            """);
    }

    public class ReplacesHostMember
    {
        [Test]
        public async Task APropertyNamedLikeAHostProperty_IsReported()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [DataSourceProperty] public string {|UIX0001:Name|} { get; set; } = "";
                }
                """);
        }

        /// <summary>MCM's mixin, against the game's own OptionsVM.</summary>
        [Test]
        public async Task CommandsNamedLikeTheGamesOptionsVMMethods_AreReported()
        {
            await VerifyAsync("""
                using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

                [ViewModelMixin]
                public sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
                {
                    public OptionsVMMixin(OptionsVM vm) : base(vm) { }

                    [DataSourceMethod] public void {|UIX0001:ExecuteDone|}() { }
                    [DataSourceMethod] public void {|UIX0001:ExecuteCancel|}() { }
                    [DataSourceMethod] public void {|UIX0001:ExecuteCloseOptions|}() { }
                }
                """);
        }

        [Test]
        public async Task ACommandNamedLikeAPrivateMethodOfAViewModelInAnotherAssembly_IsReported()
        {
            await VerifyAsync("""
                [ViewModelMixin]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }

                    [DataSourceMethod] public void {|UIX0001:CloseScreen|}() { }
                }
                """, gameSource: """
                public class GameVM : ViewModel
                {
                    private void CloseScreen() { }
                }
                """);
        }

        [Test]
        public async Task APropertyNamedLikeAPrivatePropertyOfTheViewModel_IsReported()
        {
            await VerifyAsync("""
                [ViewModelMixin]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }

                    [DataSourceProperty] public int {|UIX0001:Secret|} { get; set; }
                }
                """, gameSource: """
                public class GameVM : ViewModel
                {
                    private int Secret { get; set; }
                }
                """);
        }

        /// <summary>
        /// GetProperties(NonPublic) returns no private property of a base type, so the table holds none; but the command
        /// walk in ExecuteCommand does find a private method of a base type.
        /// </summary>
        [Test]
        public async Task APrivateMemberOfABaseType_CollidesForCommandsOnly()
        {
            await VerifyAsync("""
                [ViewModelMixin]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }

                    [DataSourceProperty] public int Secret { get; set; }
                    [DataSourceMethod] public void {|UIX0001:Hidden|}() { }
                }
                """, gameSource: """
                public class GameBaseVM : ViewModel
                {
                    private int Secret { get; set; }
                    private void Hidden() { }
                }
                public class GameVM : GameBaseVM { }
                """);
        }

        [Test]
        public async Task AnUnmarkedMemberNamedLikeAHostMember_IsNotReported()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    public string Name { get; set; } = "";
                    public void ExecuteDone() { }
                }
                """);
        }

        [Test]
        public async Task APropertyNamedLikeAHostMethod_IsNotReported()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [DataSourceProperty] public bool ExecuteDone { get; set; }
                }
                """);
        }

        [Test]
        public async Task WithHandleDerived_AMemberOfADerivedViewModel_IsReported()
        {
            await VerifyAsync("""
                [ViewModelMixin(true)]
                public sealed class BaseVMMixin : BaseViewModelMixin<GameBaseVM>
                {
                    public BaseVMMixin(GameBaseVM vm) : base(vm) { }

                    [DataSourceProperty] public string {|UIX0001:Title|} { get; set; } = "";
                }
                """, gameSource: """
                public abstract class GameBaseVM : ViewModel { }
                public class GameDerivedVM : GameBaseVM
                {
                    public string Title { get; set; } = "";
                }
                """);
        }

        [Test]
        public async Task WithoutHandleDerived_AMemberOfADerivedViewModel_IsNotReported()
        {
            await VerifyAsync("""
                [ViewModelMixin]
                public sealed class BaseVMMixin : BaseViewModelMixin<GameBaseVM>
                {
                    public BaseVMMixin(GameBaseVM vm) : base(vm) { }

                    [DataSourceProperty] public string Title { get; set; } = "";
                }
                """, gameSource: """
                public class GameBaseVM : ViewModel { }
                public class GameDerivedVM : GameBaseVM
                {
                    public string Title { get; set; } = "";
                }
                """);
        }

        [Test]
        public async Task AMarkedMemberOfASharedMixinBase_IsReportedOnTheBase()
        {
            await VerifyAsync(HostVM + """
                public abstract class SharedMixin<TViewModel> : BaseViewModelMixin<TViewModel> where TViewModel : ViewModel
                {
                    protected SharedMixin(TViewModel vm) : base(vm) { }

                    [DataSourceProperty] public string {|UIX0001:Name|} { get; set; } = "";
                }

                [ViewModelMixin]
                public sealed class HostVMMixin : SharedMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """);
        }

        /// <summary>A PropertyInfo carries its own attributes only, so the runtime does not collect an unmarked override.</summary>
        [Test]
        public async Task AnUnmarkedOverrideOfAMarkedMember_IsNotCollected()
        {
            await VerifyAsync(HostVM + """
                public abstract class SharedMixin<TViewModel> : BaseViewModelMixin<TViewModel> where TViewModel : ViewModel
                {
                    protected SharedMixin(TViewModel vm) : base(vm) { }

                    [DataSourceProperty] public virtual string Name { get; set; } = "";
                }

                [ViewModelMixin]
                public sealed class HostVMMixin : SharedMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    public override string Name { get; set; } = "";
                }
                """);
        }

        [Test]
        public async Task TheMessage_NamesTheHostMemberAndItsAccessibility()
        {
            var messages = await MessagesAsync("""
                [ViewModelMixin]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }

                    [DataSourceMethod] public void CloseScreen() { }
                }
                """, gameSource: """
                public class GameVM : ViewModel
                {
                    private void CloseScreen() { }
                }
                """);
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0001: 'CloseScreen' on mixin 'GameVMMixin' has the name of private method 'GameVM.CloseScreen' and replaces it in that ViewModel's binding table",
            }));
        }
    }

    public class DuplicateMixinMember
    {
        [Test]
        public async Task TwoMixinsAddingOneNameToOneViewModel_AreReportedOnTheSecond()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public sealed class FirstMixin : BaseViewModelMixin<HostVM>
                {
                    public FirstMixin(HostVM vm) : base(vm) { }

                    [DataSourceProperty] public int Shared { get; set; }
                    [DataSourceMethod] public void ExecuteShared() { }
                }

                [ViewModelMixin]
                public sealed class {|UIX0002:SecondMixin|} : BaseViewModelMixin<HostVM>
                {
                    public SecondMixin(HostVM vm) : base(vm) { }

                    [DataSourceProperty] public int Shared { get; set; }
                    [DataSourceMethod] public void ExecuteShared() { }
                }
                """);
        }

        [Test]
        public async Task TheMessage_NamesBothMixinsAndTheViewModel()
        {
            var messages = await MessagesAsync(HostVM + """
                [ViewModelMixin]
                public sealed class FirstMixin : BaseViewModelMixin<HostVM>
                {
                    public FirstMixin(HostVM vm) : base(vm) { }
                    [DataSourceProperty] public int Shared { get; set; }
                    [DataSourceMethod] public void ExecuteShared() { }
                }

                [ViewModelMixin]
                public sealed class SecondMixin : BaseViewModelMixin<HostVM>
                {
                    public SecondMixin(HostVM vm) : base(vm) { }
                    [DataSourceProperty] public int Shared { get; set; }
                    [DataSourceMethod] public void ExecuteShared() { }
                }
                """);
            Assert.That(messages, Is.EquivalentTo(new[]
            {
                "UIX0002: 'ExecuteShared' is added to 'HostVM' by both 'FirstMixin' and 'SecondMixin'; whichever is registered last replaces the other",
                "UIX0002: 'Shared' is added to 'HostVM' by both 'FirstMixin' and 'SecondMixin'; whichever is registered last replaces the other",
            }));
        }

        [Test]
        public async Task AHandleDerivedMixinOnABaseType_MeetsAMixinOnADerivedType()
        {
            var messages = await MessagesAsync("""
                public class GameBaseVM : ViewModel { }
                public class GameDerivedVM : GameBaseVM { }

                [ViewModelMixin(true)]
                public sealed class BaseMixin : BaseViewModelMixin<GameBaseVM>
                {
                    public BaseMixin(GameBaseVM vm) : base(vm) { }
                    [DataSourceProperty] public int Shared { get; set; }
                }

                [ViewModelMixin]
                public sealed class DerivedMixin : BaseViewModelMixin<GameDerivedVM>
                {
                    public DerivedMixin(GameDerivedVM vm) : base(vm) { }
                    [DataSourceProperty] public int Shared { get; set; }
                }
                """);
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0002: 'Shared' is added to 'GameDerivedVM' by both 'BaseMixin' and 'DerivedMixin'; whichever is registered last replaces the other",
            }));
        }

        [Test]
        public async Task WithoutHandleDerived_MixinsOnABaseAndADerivedType_DoNotMeet()
        {
            await VerifyAsync("""
                public class GameBaseVM : ViewModel { }
                public class GameDerivedVM : GameBaseVM { }

                [ViewModelMixin]
                public sealed class BaseMixin : BaseViewModelMixin<GameBaseVM>
                {
                    public BaseMixin(GameBaseVM vm) : base(vm) { }
                    [DataSourceProperty] public int Shared { get; set; }
                }

                [ViewModelMixin]
                public sealed class DerivedMixin : BaseViewModelMixin<GameDerivedVM>
                {
                    public DerivedMixin(GameDerivedVM vm) : base(vm) { }
                    [DataSourceProperty] public int Shared { get; set; }
                }
                """);
        }

        [Test]
        public async Task APropertyAndACommandOfOneName_DoNotCollide()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public sealed class FirstMixin : BaseViewModelMixin<HostVM>
                {
                    public FirstMixin(HostVM vm) : base(vm) { }
                    [DataSourceProperty] public int Shared { get; set; }
                }

                [ViewModelMixin]
                public sealed class SecondMixin : BaseViewModelMixin<HostVM>
                {
                    public SecondMixin(HostVM vm) : base(vm) { }
                    [DataSourceMethod] public void Shared() { }
                }
                """);
        }
    }

    public class RefreshMethodNotFound
    {
        [Test]
        public async Task ANameTheViewModelDoesNotHave_IsReportedOnTheAttribute()
        {
            await VerifyAsync(HostVM + """
                [{|UIX0003:ViewModelMixin("RefreshValuez")|}]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """);
        }

        [Test]
        public async Task AnOverloadedNameWithoutAParameterlessOverload_IsReported()
        {
            var messages = await MessagesAsync("""
                public class GameVM : ViewModel
                {
                    public void Update(int a) { }
                    public void Update(string b) { }
                }

                [ViewModelMixin("Update")]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }
                }
                """);
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0003: 'GameVM' has no method 'Update' UIExtenderEx can hook: it is overloaded, and no overload takes no parameters; " + (Verifier.UIExtenderExVersion.Major < 3
                    ? "UIExtender.Register throws here, and the mod's types after this one are not registered"
                    : "the mixin's OnRefresh is never called"),
            }));
        }

        [Test]
        public async Task AnOverloadedNameWithAParameterlessOverload_IsFound()
        {
            await VerifyAsync("""
                public class GameVM : ViewModel
                {
                    public void Update() { }
                    public void Update(int a) { }
                }

                [ViewModelMixin("Update")]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }
                }
                """);
        }

        [Test]
        public async Task AnOverrideAndTheMethodItOverrides_AreOneMethod()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin(nameof(HostVM.RefreshValues))]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """);
        }

        /// <summary>AccessTools2.Method walks up the base types, where a private method is visible on its own type.</summary>
        [Test]
        public async Task APrivateMethodOfABaseTypeInAnotherAssembly_IsFound()
        {
            await VerifyAsync("""
                [ViewModelMixin("UpdateDiplomacyProperties")]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }
                }
                """, gameSource: """
                public class GameBaseVM : ViewModel
                {
                    private void UpdateDiplomacyProperties() { }
                }
                public class GameVM : GameBaseVM { }
                """);
        }
    }

    public class HostNeverInstantiated
    {
        /// <summary>SecretAlliances' mixin: the ViewModel base type, with a type name where the refresh method goes.</summary>
        [Test]
        public async Task AMixinOfViewModelItself_IsReported()
        {
            await VerifyAsync("""
                [{|UIX0003:ViewModelMixin("ClanVM")|}]
                public sealed class {|UIX0004:ClanVMMixin|} : BaseViewModelMixin<ViewModel>
                {
                    public ClanVMMixin(ViewModel vm) : base(vm) { }
                }
                """);
        }

        [Test]
        public async Task AnAbstractViewModelWithHandleDerived_IsNotReported()
        {
            await VerifyAsync("""
                public abstract class GameBaseVM : ViewModel { }

                [ViewModelMixin(true)]
                public sealed class GameBaseVMMixin : BaseViewModelMixin<GameBaseVM>
                {
                    public GameBaseVMMixin(GameBaseVM vm) : base(vm) { }
                }
                """);
        }
    }

    public class HasNoViewModel
    {
        [Test]
        public async Task AMarkedClassThatIsNotAMixin_IsReported()
        {
            await VerifyAsync("""
                [ViewModelMixin]
                public sealed class {|UIX0005:NotAMixin|} { }
                """);
        }

        /// <summary>The ViewModel comes from the closed BaseViewModelMixin, whatever a shared base takes first.</summary>
        [Test]
        public async Task ASharedBaseTakingSomethingElseFirst_IsNotReported()
        {
            await VerifyAsync(HostVM + """
                public abstract class SharedMixin<TData, TViewModel> : BaseViewModelMixin<TViewModel> where TViewModel : ViewModel
                {
                    protected SharedMixin(TViewModel vm) : base(vm) { }
                }

                [ViewModelMixin]
                public sealed class HostVMMixin : SharedMixin<string, HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """);
        }

        /// <summary>A mixin implementing IViewModelMixin itself keeps the old rule: the first type argument, whatever it is.</summary>
        [Test]
        public async Task AnIViewModelMixinOfItsOwnTakingSomethingElseFirst_IsReported()
        {
            var messages = await MessagesAsync(HostVM + """
                public abstract class OwnMixinBase<TData, TViewModel> : IViewModelMixin where TViewModel : ViewModel
                {
                    public void OnRefresh() { }
                    public void OnFinalize() { }
                }

                [ViewModelMixin]
                public sealed class HostVMMixin : OwnMixinBase<string, HostVM>
                {
                    public HostVMMixin(HostVM vm) { }
                }
                """);
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0005: 'HostVMMixin' is marked [ViewModelMixin], but the ViewModel UIExtenderEx takes from its base types is 'string', which is not a ViewModel; derive from BaseViewModelMixin<TViewModel>",
            }));
        }

        [Test]
        public async Task ASharedBaseTakingTheViewModelFirst_IsNotReported()
        {
            await VerifyAsync(HostVM + """
                public abstract class SharedMixin<TViewModel, TData> : BaseViewModelMixin<TViewModel> where TViewModel : ViewModel
                {
                    protected SharedMixin(TViewModel vm) : base(vm) { }
                }

                [ViewModelMixin]
                public sealed class HostVMMixin : SharedMixin<HostVM, string>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """);
        }
    }

    public class CannotBeCreated
    {
        [Test]
        public async Task AnAbstractMixin_IsAnError()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public abstract class {|UIX0006:HostVMMixin|} : BaseViewModelMixin<HostVM>
                {
                    protected HostVMMixin(HostVM vm) : base(vm) { }
                }
                """);
        }

        [Test]
        public async Task AMixinWithoutAConstructorTakingTheViewModel_IsAnError()
        {
            var messages = await MessagesAsync(HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm, int extra) : base(vm) { }
                    internal HostVMMixin(HostVM vm) : base(vm) { }
                }
                """);
            Assert.That(messages, Is.EqualTo(new[]
            {
                "UIX0006: Mixin 'HostVMMixin' cannot be created for 'HostVM': it has no public constructor taking a 'HostVM' as its only parameter; the ViewModel's constructor will throw",
            }));
        }

        [Test]
        public async Task AConstructorTakingABaseTypeOfTheViewModel_IsEnough()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(ViewModel vm) : base((HostVM) vm) { }
                }
                """);
        }
    }

    public class MemberNotPublic
    {
        [Test]
        public async Task NonPublicMarkedMembers_AreReported()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [DataSourceProperty] private int {|UIX0007:Count|} { get; set; }
                    [DataSourceProperty] internal string {|UIX0007:Label|} { get; set; } = "";
                    [DataSourceMethod] protected void {|UIX0007:ExecuteThing|}() { }
                }
                """);
        }

        [Test]
        public async Task APublicPropertyWithANonPublicSetter_IsPublic()
        {
            await VerifyAsync(HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [DataSourceProperty] public int Count { get; private set; }
                }
                """);
        }
    }
}
