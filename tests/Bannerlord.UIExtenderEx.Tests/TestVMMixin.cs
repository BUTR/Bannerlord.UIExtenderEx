using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;

using System.Diagnostics.CodeAnalysis;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests
{
    [ViewModelMixin]
    internal class TestVMMixin : BaseViewModelMixin<TestVM>
    {
        public static bool MixinMethodCalled { get; private set; }

        [DataSourceProperty]
        public object MixinProperty => null!;

        public TestVMMixin(TestVM vm) : base(vm) { }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("Redundancy", "RCS1132:Remove redundant overriding member.", Justification = "Explicit declaration")]
        public override void OnRefresh()
        {
            base.OnRefresh();
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("Redundancy", "RCS1132:Remove redundant overriding member.", Justification = "Explicit declaration")]
        public override void OnFinalize()
        {
            base.OnFinalize();
        }

        [DataSourceMethod]
        public void MixinMethod()
        {
            MixinMethodCalled = true;
        }
    }

    [ViewModelMixin(true)]
    internal class DerivedTestVMMixin : BaseViewModelMixin<TestVM>
    {
        public static bool DerivedMixinMethodCalled { get; private set; }

        [DataSourceProperty]
        public object DerivedMixinProperty => null!;

        public DerivedTestVMMixin(TestVM vm) : base(vm) { }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("Redundancy", "RCS1132:Remove redundant overriding member.", Justification = "Explicit declaration")]
        public override void OnRefresh()
        {
            base.OnRefresh();
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("Redundancy", "RCS1132:Remove redundant overriding member.", Justification = "Explicit declaration")]
        public override void OnFinalize()
        {
            base.OnFinalize();
        }

        [DataSourceMethod]
        public void DerivedMixinMethod()
        {
            DerivedMixinMethodCalled = true;
        }
    }
}