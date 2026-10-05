using NUnit.Framework;

using System.Threading.Tasks;

using static Bannerlord.UIExtenderEx.Analyzers.Tests.CodeFixVerifier;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// Tests Roslyn code fix providers by applying registered fixes to diagnostic reports and asserting the resulting
/// document text against expected outputs.
/// </summary>
public class CodeFixTests
{
    private const string HostVM = """
        public class HostVM : ViewModel
        {
            private string _name = "";
            public string Title { get; set; } = "";
            public void ExecuteClose() { }
            public override void RefreshValues() { }
        }

        """;

    public class Mixins
    {
        [Test]
        public async Task UIX0003_AMisspelledRefreshMethod_BecomesNameof()
        {
            await VerifyAsync("UIX0003", HostVM + """
                [ViewModelMixin("RefreshValuez")]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """, HostVM + """
                [ViewModelMixin(nameof(HostVM.RefreshValues))]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """, "Use 'RefreshValues'");
        }

        /// <summary>
        /// Verifies that private methods in referenced external assemblies remain string literals rather than <c>nameof</c> expressions.
        /// </summary>
        [Test]
        public async Task UIX0003_AMethodTheMixinCannotSee_StaysAString()
        {
            const string game = """
                public class GameVM : ViewModel
                {
                    private void RefreshList() { }
                }
                """;
            await VerifyAsync("UIX0003", """
                [ViewModelMixin("RefreshLst")]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }
                }
                """, """
                [ViewModelMixin("RefreshList")]
                public sealed class GameVMMixin : BaseViewModelMixin<GameVM>
                {
                    public GameVMMixin(GameVM vm) : base(vm) { }
                }
                """, "Use 'RefreshList'", game);
        }

        private const string AbstractVM = """
            public abstract class BaseVM : ViewModel { }

            """;

        [Test]
        public async Task UIX0004_HandleDerived_IsAdded()
        {
            await VerifyAsync("UIX0004", AbstractVM + """
                [ViewModelMixin(nameof(ViewModel.RefreshValues))]
                public sealed class BaseVMMixin : BaseViewModelMixin<BaseVM>
                {
                    public BaseVMMixin(BaseVM vm) : base(vm) { }
                }
                """, AbstractVM + """
                [ViewModelMixin(nameof(ViewModel.RefreshValues), handleDerived: true)]
                public sealed class BaseVMMixin : BaseViewModelMixin<BaseVM>
                {
                    public BaseVMMixin(BaseVM vm) : base(vm) { }
                }
                """, "Set handleDerived: true");
        }

        [Test]
        public async Task UIX0004_HandleDerivedFalse_IsTurnedOver()
        {
            await VerifyAsync("UIX0004", AbstractVM + """
                [ViewModelMixin(false)]
                public sealed class BaseVMMixin : BaseViewModelMixin<BaseVM>
                {
                    public BaseVMMixin(BaseVM vm) : base(vm) { }
                }
                """, AbstractVM + """
                [ViewModelMixin(true)]
                public sealed class BaseVMMixin : BaseViewModelMixin<BaseVM>
                {
                    public BaseVMMixin(BaseVM vm) : base(vm) { }
                }
                """, "Set handleDerived: true");
        }

        [Test]
        public async Task UIX0006_TheConstructorTakingTheViewModel_IsAdded()
        {
            await VerifyAsync("UIX0006", HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    private readonly int _count;

                    public HostVMMixin(HostVM vm, int count) : base(vm) => _count = count;

                    [DataSourceProperty] public int Count => _count;
                }
                """, HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    private readonly int _count;

                    public HostVMMixin(HostVM vm) : base(vm) { }

                    public HostVMMixin(HostVM vm, int count) : base(vm) => _count = count;

                    [DataSourceProperty] public int Count => _count;
                }
                """, "Add a constructor taking 'HostVM'");
        }

        [Test]
        public async Task UIX0006_AnInternalConstructor_IsMadePublic()
        {
            await VerifyAsync("UIX0006", HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    internal HostVMMixin(HostVM vm) : base(vm) { }
                }
                """, HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """, "Make the constructor public");
        }

        [Test]
        public async Task UIX0006_AnAbstractMixin_IsMadeConcrete()
        {
            await VerifyAsync("UIX0006", HostVM + """
                [ViewModelMixin]
                public abstract class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """, HostVM + """
                [ViewModelMixin]
                public class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """, "Make 'HostVMMixin' not abstract");
        }

        [Test]
        public async Task UIX0006_AGenericMixin_HasNoFix()
        {
            var titles = await TitlesAsync("UIX0006", HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin<T> : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                }
                """);
            Assert.That(titles, Is.Empty);
        }

        [Test]
        public async Task UIX0007_APrivateMember_IsMadePublic()
        {
            await VerifyAsync("UIX0007", HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [DataSourceProperty] private int MyMod_Count { get; set; }
                }
                """, HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [DataSourceProperty] public int MyMod_Count { get; set; }
                }
                """, "Make 'MyMod_Count' public");
        }

        [Test]
        public async Task UIX0007_AMemberWithNoAccessibility_IsMadePublic()
        {
            await VerifyAsync("UIX0007", HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [DataSourceMethod]
                    void ExecuteMyMod() { }

                    [DataSourceProperty] protected internal string MyMod_Label => "";
                }
                """, HostVM + """
                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }

                    [DataSourceMethod]
                    public void ExecuteMyMod() { }

                    [DataSourceProperty] protected internal string MyMod_Label => "";
                }
                """, "Make 'ExecuteMyMod' public");
        }
    }

    public class Accessors
    {
        [Test]
        public async Task UIX0010_NoInlining_IsAdded()
        {
            await VerifyAsync("UIX0010", HostVM + """
                public static class HostVMAccessors
                {
                    // The name the game gives it
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_name")]
                    public static ref string Name(HostVM instance) => throw new NotImplementedException();
                }
                """, HostVM + """
                public static class HostVMAccessors
                {
                    // The name the game gives it
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_name")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref string Name(HostVM instance) => throw new NotImplementedException();
                }
                """, "Mark the stub NoInlining");
        }

        [Test]
        public async Task UIX0010_TheUsing_IsAddedWhenTheFileHasNone()
        {
            await VerifyAsync("UIX0010", """
                using System;
                using Bannerlord.UIExtenderEx.Attributes;
                using TaleWorlds.Library;

                public class HostVM : ViewModel
                {
                    private string _name = "";
                }

                public static class HostVMAccessors
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_name")]
                    public static ref string Name(HostVM instance) => throw new NotImplementedException();
                }
                """, """
                using System;
                using System.Runtime.CompilerServices;
                using Bannerlord.UIExtenderEx.Attributes;
                using TaleWorlds.Library;

                public class HostVM : ViewModel
                {
                    private string _name = "";
                }

                public static class HostVMAccessors
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_name")]
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref string Name(HostVM instance) => throw new NotImplementedException();
                }
                """, "Mark the stub NoInlining", usings: false);
        }

        [Test]
        public async Task UIX0010_NoInlining_JoinsTheFlagsAlreadyThere()
        {
            await VerifyAsync("UIX0010", HostVM + """
                public static class HostVMAccessors
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_name")]
                    [MethodImpl(MethodImplOptions.NoOptimization)]
                    public static ref string Name(HostVM instance) => throw new NotImplementedException();
                }
                """, HostVM + """
                public static class HostVMAccessors
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_name")]
                    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
                    public static ref string Name(HostVM instance) => throw new NotImplementedException();
                }
                """, "Mark the stub NoInlining");
        }

        [Test]
        public async Task UIX0010_AggressiveInlining_IsReplaced()
        {
            await VerifyAsync("UIX0010", HostVM + """
                public static class HostVMAccessors
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_name"), MethodImpl(MethodImplOptions.AggressiveInlining)]
                    public static ref string Name(HostVM instance) => throw new NotImplementedException();
                }
                """, HostVM + """
                public static class HostVMAccessors
                {
                    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_name"), MethodImpl(MethodImplOptions.NoInlining)]
                    public static ref string Name(HostVM instance) => throw new NotImplementedException();
                }
                """, "Mark the stub NoInlining");
        }
    }

    public class ContentMembers
    {
        private static string Patch(string member) => $$"""
            [PrefabExtension("HostMovie", "descendant::Widget[@Id='Panel']")]
            public sealed class Page : PrefabExtensionInsertPatch
            {
                public override InsertType Type => InsertType.Child;
                {{member}}
            }
            """;

        [Test]
        public async Task UIX0017_AStringUnderTheXmlNodeAttribute_TakesTheTextAttribute()
        {
            await VerifyAsync("UIX0017",
                Patch("[PrefabExtensionXmlNode] public string GetXml() => \"<Root />\";"),
                Patch("[PrefabExtensionText] public string GetXml() => \"<Root />\";"),
                "Use [PrefabExtensionText]");
        }

        [Test]
        public async Task UIX0017_AListUnderTheXmlNodeAttribute_TakesTheXmlNodesAttribute()
        {
            await VerifyAsync("UIX0017",
                Patch("[PrefabExtensionXmlNode(true)] public List<XmlNode> GetNodes() => new();"),
                Patch("[PrefabExtensionXmlNodes] public List<XmlNode> GetNodes() => new();"),
                "Use [PrefabExtensionXmlNodes]");
        }

        [Test]
        public async Task UIX0017_APrivateMember_IsMadePublic()
        {
            await VerifyAsync("UIX0017",
                Patch("[PrefabExtensionXmlNode] private XmlNode GetNode() => null!;"),
                Patch("[PrefabExtensionXmlNode] public XmlNode GetNode() => null!;"),
                "Make 'GetNode' public");
        }

        [Test]
        public async Task UIX0017_AStaticMember_BecomesAnInstanceMember()
        {
            await VerifyAsync("UIX0017",
                Patch("[PrefabExtensionXmlNode] public static XmlNode GetNode() => null!;"),
                Patch("[PrefabExtensionXmlNode] public XmlNode GetNode() => null!;"),
                "Make 'GetNode' an instance member");
        }
    }

    public class Prefabs
    {
        private const string Mod = """
            public class HostVM : ViewModel
            {
                public string Title { get; set; } = "";
            }

            public class ChildVM : ViewModel
            {
                public string Label { get; set; } = "";
                public MBBindingList<ChildVM> Items { get; } = new();
                public void ExecuteChild() { }
            }

            [ViewModelMixin]
            public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
            {
                public HostVMMixin(HostVM vm) : base(vm) { }
                [DataSourceProperty] public ChildVM Mod { get; } = new();
                [DataSourceProperty] public int ModWidth { get; set; }
            }

            """;

        private static string InsertPatch(string xml) => $$"""
            [PrefabExtension("HostMovie", "descendant::Widget[@Id='Panel']")]
            public sealed class InsertPage : PrefabExtensionInsertPatch
            {
                public override InsertType Type => InsertType.Child;
                private readonly XmlDocument _document = new();
                public InsertPage() { _document.LoadXml({{xml}}); }
                [PrefabExtensionXmlNode] public XmlNode GetPrefabExtension() => _document;
            }
            """;

        private static string Page(string widgets) => $$"""
            <Prefab>
              <Window>
                <ListPanel>
                  <Children>
                    {{widgets}}
                  </Children>
                </ListPanel>
              </Window>
            </Prefab>
            """;

        private static readonly string InsertModPage = InsertPatch("\"<ModPage DataSource=\\\"{Mod}\\\" />\"");

        [Test]
        public async Task UIX0012_AMisspelledAttributeInAFile_IsReplaced()
        {
            await VerifyAsync("UIX0012", Mod, Mod,
                [("GUI/Prefabs/Page.xml", Page("<Widget HorizontalAlightment=\"Left\" />"), Page("<Widget HorizontalAlignment=\"Left\" />"))],
                "Change to 'HorizontalAlignment'");
        }

        [Test]
        public async Task UIX0012_AMisspelledAttributeInALiteral_IsReplaced()
        {
            await VerifyAsync("UIX0012",
                Mod + InsertPatch("\"<TextWidget DataSource=\\\"{Mod}\\\" Txet=\\\"@Label\\\" />\""),
                Mod + InsertPatch("\"<TextWidget DataSource=\\\"{Mod}\\\" Text=\\\"@Label\\\" />\""),
                "Change to 'Text'");
        }

        /// <summary>
        /// Verifies that ambiguous occurrences in raw string literals do not offer automated replacements when exact source mapping is unavailable.
        /// </summary>
        [Test]
        public async Task UIX0012_ANameTwiceInALiteralTheAnalyzerCannotMap_HasNoFix()
        {
            var once = await TitlesAsync("UIX0012", Mod + InsertPatch("\"\"\"<TextWidget DataSource=\"{Mod}\" Txet=\"@Label\" />\"\"\""));
            Assert.That(once, Does.Contain("Change to 'Text'"));
            var twice = await TitlesAsync("UIX0012", Mod + InsertPatch("\"\"\"<TextWidget DataSource=\"{Mod}\" Txet=\"@Label\" Id=\"Txet\" />\"\"\""));
            Assert.That(twice, Is.Empty);
        }

        [Test]
        public async Task UIX0013_AnEnumMemberMisspelled_IsReplaced()
        {
            await VerifyAsync("UIX0013", Mod, Mod,
                [("GUI/Prefabs/Page.xml", Page("<Widget HorizontalAlignment=\"Centre\" />"), Page("<Widget HorizontalAlignment=\"Center\" />"))],
                "Change to 'Center'");
        }

        [Test]
        public async Task UIX0013_ACapitalisedBool_IsLowerCased()
        {
            await VerifyAsync("UIX0013", Mod, Mod,
                [("GUI/Prefabs/Page.xml", Page("<Widget IsVisible=\"True\" />"), Page("<Widget IsVisible=\"true\" />"))],
                "Change to 'true'");
        }

        [Test]
        public async Task UIX0014_AMisspelledParameter_IsReplaced()
        {
            const string selector = """
                <Prefab>
                  <Parameters>
                    <Parameter Name="SelectorDataSource" DefaultValue="" />
                  </Parameters>
                  <Window>
                    <ListPanel DataSource="*SelectorDataSource" />
                  </Window>
                </Prefab>
                """;
            await VerifyAsync("UIX0014", Mod, Mod,
                [
                    ("GUI/Prefabs/Page.xml", Page("<ModSelector Parameter.SelectorDatasource=\"{Items}\" />"), Page("<ModSelector Parameter.SelectorDataSource=\"{Items}\" />")),
                    ("GUI/Prefabs/ModSelector.xml", selector, selector),
                ],
                "Change to 'SelectorDataSource'");
        }

        [Test]
        public async Task UIX0015_AMisspelledBinding_IsReplaced()
        {
            await VerifyAsync("UIX0015", Mod + InsertModPage, Mod + InsertModPage,
                [("GUI/Prefabs/ModPage.xml", Page("<TextWidget Text=\"@Lable\" />"), Page("<TextWidget Text=\"@Label\" />"))],
                "Change to 'Label'");
        }

        [Test]
        public async Task UIX0015_AMisspelledCommand_IsReplaced()
        {
            await VerifyAsync("UIX0015", Mod + InsertModPage, Mod + InsertModPage,
                [("GUI/Prefabs/ModPage.xml", Page("<ButtonWidget Command.Click=\"ExecuteChld\" />"), Page("<ButtonWidget Command.Click=\"ExecuteChild\" />"))],
                "Change to 'ExecuteChild'");
        }

        [Test]
        public async Task UIX0015_AMisspelledDataSourceStep_IsReplaced()
        {
            await VerifyAsync("UIX0015", Mod + InsertModPage, Mod + InsertModPage,
                [("GUI/Prefabs/ModPage.xml", Page("<ListPanel DataSource=\"{Itmes}\" />"), Page("<ListPanel DataSource=\"{Items}\" />"))],
                "Change to 'Items'");
        }

        /// <summary>
        /// Verifies that when a binding path matches the target attribute name, the code fix updates the attribute value rather than the attribute name.
        /// </summary>
        [Test]
        public async Task UIX0015_ABindingNamedLikeItsAttribute_ReplacesTheValue()
        {
            const string mod = """
                public class HostVM : ViewModel { }

                public class ChildVM : ViewModel
                {
                    public string Texts { get; set; } = "";
                }

                [ViewModelMixin]
                public sealed class HostVMMixin : BaseViewModelMixin<HostVM>
                {
                    public HostVMMixin(HostVM vm) : base(vm) { }
                    [DataSourceProperty] public ChildVM Mod { get; } = new();
                }

                """;
            await VerifyAsync("UIX0015", mod + InsertModPage, mod + InsertModPage,
                [("GUI/Prefabs/ModPage.xml", Page("<TextWidget Text=\"@Text\" />"), Page("<TextWidget Text=\"@Texts\" />"))],
                "Change to 'Texts'");
        }

        [Test]
        public async Task UIX0016_AMisspelledBindingInASetAttributePatch_IsReplaced()
        {
            static string Patch(string value) => $$"""
                [PrefabExtension("HostMovie", "descendant::Widget[@Id='Panel']")]
                public sealed class Width : PrefabExtensionSetAttributePatch
                {
                    public override List<Attribute> Attributes => [new Attribute("SuggestedWidth", "{{value}}")];
                }
                """;
            await VerifyAsync("UIX0016", Mod + Patch("@ModWdth"), Mod + Patch("@ModWidth"), "Change to 'ModWidth'");
        }

        [Test]
        public async Task UIX0019_TheLinkLosesItsMixin()
        {
            var patch = InsertPatch("\"<TextWidget Text=\\\"@Title\\\" />\"");
            await VerifyAsync("UIX0019",
                "[assembly: PrefabLink(typeof(InsertPage), typeof(HostVM), typeof(HostVMMixin))]\n" + Mod + patch,
                "[assembly: PrefabLink(typeof(InsertPage), typeof(HostVM))]\n" + Mod + patch,
                "Link to the ViewModel alone");
        }
    }
}