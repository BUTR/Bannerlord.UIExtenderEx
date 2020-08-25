using NUnit.Framework;

using System;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests
{
    public class ViewModelMixinTests
    {
        [Test]
        public void MixinPropertyIsInjectedTest()
        {
            var uiExtender = new UIExtender("TestModule");
            uiExtender.Register();
            uiExtender.Enable();

            var viewModel = new TestVM();
            Assert.True(viewModel.Properties.Contains(nameof(TestVMMixin.MixinProperty)));
        }

        [Test]
        public void MixinMethodIsCalledTest()
        {
            var uiExtender = new UIExtender("TestModule");
            uiExtender.Register();
            uiExtender.Enable();

            var viewModel = new TestVM();
            viewModel.ExecuteCommand(nameof(TestVMMixin.MixinMethod), Array.Empty<object>());
            Assert.True(TestVMMixin.MixinMethodCalled);
        }
    }
}