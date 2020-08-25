using System;

namespace Bannerlord.UIExtenderEx.Attributes
{
    /// <summary>
    /// Attribute for mixin methods to be added to view models.
    /// Only methods specified by this attribute will actually end up in extended view model
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class DataSourceMethodAttribute : Attribute { }
}