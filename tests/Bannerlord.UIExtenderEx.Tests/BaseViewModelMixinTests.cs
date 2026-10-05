using Bannerlord.UIExtenderEx.ViewModels;

using NUnit.Framework;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests;

public class BaseViewModelMixinTests
{
    private class PrivateStateBaseVM : ViewModel
    {
        private int _inherited = 3;

        public int Inherited => _inherited;
    }

    private class PrivateStateVM : PrivateStateBaseVM
    {
        private string _field = "field";
        private string Property { get; set; } = "property";
        private object _untyped = "untyped";
        private readonly int _readonly = 1;

        public string Field => _field;
        public string PropertyValue => Property;
        public int Readonly => _readonly;
    }

    // Instantiate directly without [ViewModelMixin] attribute decoration to test BaseViewModelMixin in isolation without an active UIExtender instance.
    private class PrivateStateMixin : BaseViewModelMixin<PrivateStateVM>
    {
        public PrivateStateMixin(PrivateStateVM vm) : base(vm) { }

        public TValue? Get<TValue>(string name) => GetPrivate<TValue>(name);
        public void Set<TValue>(string name, TValue? value) => SetPrivate(name, value);
    }

    [Test]
    public void GetPrivate_ReadsNonPublicFieldsAndProperties_IncludingInheritedOnes()
    {
        var mixin = new PrivateStateMixin(new PrivateStateVM());

        Assert.That(mixin.Get<string>("_field"), Is.EqualTo("field"));
        Assert.That(mixin.Get<string>("Property"), Is.EqualTo("property"));
        Assert.That(mixin.Get<int>("_inherited"), Is.EqualTo(3));
    }

    [Test]
    public void SetPrivate_WritesNonPublicFieldsAndProperties_IncludingInheritedOnes()
    {
        var vm = new PrivateStateVM();
        var mixin = new PrivateStateMixin(vm);

        mixin.Set("_field", "changed field");
        mixin.Set("Property", "changed property");
        mixin.Set("_inherited", 7);

        Assert.That(vm.Field, Is.EqualTo("changed field"));
        Assert.That(vm.PropertyValue, Is.EqualTo("changed property"));
        Assert.That(vm.Inherited, Is.EqualTo(7));
    }

    [Test]
    public void GetPrivate_ConvertsToTheRequestedType_OrReadsAsDefault()
    {
        var mixin = new PrivateStateMixin(new PrivateStateVM());

        Assert.That(mixin.Get<object>("_field"), Is.EqualTo("field"));
        Assert.That(mixin.Get<object>("_inherited"), Is.EqualTo(3));
        Assert.That(mixin.Get<int?>("_inherited"), Is.EqualTo(3));
        Assert.That(mixin.Get<string>("_untyped"), Is.EqualTo("untyped"));
        Assert.That(mixin.Get<int>("_untyped"), Is.EqualTo(0));
        Assert.That(mixin.Get<string>("_inherited"), Is.Null);
    }

    [Test]
    public void SetPrivate_WritesReadonlyFields_AndValuesOfADerivedType()
    {
        var vm = new PrivateStateVM();
        var mixin = new PrivateStateMixin(vm);

        mixin.Set("_readonly", 5);
        mixin.Set("_untyped", "changed");

        Assert.That(vm.Readonly, Is.EqualTo(5));
        Assert.That(mixin.Get<string>("_untyped"), Is.EqualTo("changed"));
    }

    [Test]
    public void AnUnknownName_ReadsAsDefault_AndWritesNothing()
    {
        var mixin = new PrivateStateMixin(new PrivateStateVM());

        Assert.That(mixin.Get<string>("NoSuchMember"), Is.Null);
        Assert.DoesNotThrow(() => mixin.Set("NoSuchMember", "value"));
    }
}