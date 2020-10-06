using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;

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

        public override void OnRefresh()
        {
            base.OnRefresh();
        }

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
}