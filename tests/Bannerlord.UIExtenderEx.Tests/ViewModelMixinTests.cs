using Bannerlord.UIExtenderEx.Tests.Prefabs.IntegrationTests;

using NUnit.Framework;

using System;

namespace Bannerlord.UIExtenderEx.Tests;

public class ViewModelMixinTests : BaseTests
{
    private UIExtender _uiExtender = default!;

    [SetUp]
    public void Setup()
    {
        _uiExtender = UIExtender.Create(nameof(ViewModelMixinTests));
        _uiExtender.Register(typeof(PrefabsTests).Assembly);
        _uiExtender.Enable();
    }

    [TearDown]
    public void Finalization()
    {
        _uiExtender.Deregister();
    }

    [Test]
    public void MixinPropertyIsInjectedTest()
    {
        var viewModel = new TestVM();
        Assert.True(viewModel.GetPropertyType(nameof(TestVMMixin.MixinProperty)) is not null);
    }

    [Test]
    public void MixinDerivedTest()
    {
        var viewModel = new DerivedTestVM();
        Assert.True(viewModel.GetPropertyType(nameof(DerivedTestVMMixin.DerivedMixinProperty)) is not null);
    }

    [Test]
    public void MixinMethodIsCalledTest()
    {
        var viewModel = new TestVM();
        viewModel.ExecuteCommand(nameof(TestVMMixin.MixinMethod), []);
        Assert.True(TestVMMixin.MixinMethodCalled);
        Assert.True(DerivedTestVMMixin.DerivedMixinMethodCalled);
    }

    [Test]
    public void MixinMethodIsCalledDerivedTest()
    {
        var viewModel = new TestVM();
        viewModel.ExecuteCommand(nameof(DerivedTestVMMixin.DerivedMixinMethod), []);
        Assert.False(TestVMMixin.MixinMethodCalled);
        Assert.True(DerivedTestVMMixin.DerivedMixinMethodCalled);
    }

    [Test]
    public void MixinDerivedTestDisabled()
    {
        _uiExtender.Disable(typeof(DerivedTestVMMixin));

        var viewModel = new DerivedTestVM();
        Assert.True(viewModel.GetPropertyType(nameof(DerivedTestVMMixin.DerivedMixinProperty)) is null);
    }
}