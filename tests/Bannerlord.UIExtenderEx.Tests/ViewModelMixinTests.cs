using NUnit.Framework;

using System;

namespace Bannerlord.UIExtenderEx.Tests
{
    public class ViewModelMixinTests : BaseTests
    {
        [Test]
        public void MixinPropertyIsInjectedTest()
        {
            var uiExtender = new UIExtender(nameof(MixinPropertyIsInjectedTest));
            uiExtender.Register(typeof(PrefabsTests).Assembly);
            uiExtender.Enable();

            var viewModel = new TestVM();
            Assert.True(viewModel.GetPropertyType(nameof(TestVMMixin.MixinProperty)) is not null);
        }

        [Test]
        public void MixinDerivedTest()
        {
            var uiExtender = new UIExtender(nameof(MixinPropertyIsInjectedTest));
            uiExtender.Register(typeof(PrefabsTests).Assembly);
            uiExtender.Enable();

            var viewModel = new DerivedTestVM();
            Assert.True(viewModel.GetPropertyType(nameof(DerivedTestVMMixin.DerivedMixinProperty)) is not null);
        }

        [Test]
        public void MixinMethodIsCalledTest()
        {
            var uiExtender = new UIExtender(nameof(MixinMethodIsCalledTest));
            uiExtender.Register(typeof(PrefabsTests).Assembly);
            uiExtender.Enable();

            var viewModel = new TestVM();
            viewModel.ExecuteCommand(nameof(TestVMMixin.MixinMethod), Array.Empty<object>());
            Assert.True(TestVMMixin.MixinMethodCalled);
            Assert.True(DerivedTestVMMixin.DerivedMixinMethodCalled);
        }

        [Test]
        public void MixinMethodIsCalledDerivedTest()
        {
            var uiExtender = new UIExtender(nameof(MixinMethodIsCalledTest));
            uiExtender.Register(typeof(PrefabsTests).Assembly);
            uiExtender.Enable();

            var viewModel = new TestVM();
            viewModel.ExecuteCommand(nameof(DerivedTestVMMixin.DerivedMixinMethod), Array.Empty<object>());
            Assert.False(TestVMMixin.MixinMethodCalled);
            Assert.True(DerivedTestVMMixin.DerivedMixinMethodCalled);
        }
    }
}