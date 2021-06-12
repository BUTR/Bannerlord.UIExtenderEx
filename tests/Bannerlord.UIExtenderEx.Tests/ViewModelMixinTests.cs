using NUnit.Framework;

using System;
using System.Linq;

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
            Assert.True(viewModel.Properties.Contains(nameof(TestVMMixin.MixinProperty)));
        }

        [Test]
        public void MixinDerivedTest()
        {
            var uiExtender = new UIExtender(nameof(MixinPropertyIsInjectedTest));
            uiExtender.Register(typeof(PrefabsTests).Assembly);
            uiExtender.Enable();

            var viewModel = new DerivedTestVM();
            Assert.True(viewModel.Properties.Contains(nameof(DerivedTestVMMixin.DerivedMixinProperty)));
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
        }
    }
}