using Bannerlord.UIExtenderEx.ViewModels;

using System;

namespace Bannerlord.UIExtenderEx.Attributes
{
    /// <summary>
    /// Attribute to mark view model mixins.
    /// Mixin classes should extend from <see cref="BaseViewModelMixin{TViewModel}"/> and should be marked with this attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ViewModelMixinAttribute : BaseUIExtenderAttribute
    {
        public string? RefreshMethodName { get; }
        public bool HandleDerived { get; }

        public ViewModelMixinAttribute() { }
        public ViewModelMixinAttribute(string refreshMethodName)
        {
            RefreshMethodName = refreshMethodName;
        }
        public ViewModelMixinAttribute(bool handleDerived)
        {
            HandleDerived = handleDerived;
        }
        public ViewModelMixinAttribute(string? refreshMethodName = null, bool handleDerived = false)
        {
            RefreshMethodName = refreshMethodName;
            HandleDerived = handleDerived;
        }
    }
}