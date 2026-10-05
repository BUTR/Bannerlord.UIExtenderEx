using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

public class CodegenTestVM : ViewModel
{
    [DataSourceProperty]
    public string Title => "title";

    [DataSourceProperty]
    public bool IsEnabled => true;

    public void ExecuteClose() { }
}

public class CodegenChildVM : ViewModel
{
    [DataSourceProperty]
    public string ChildText => "child";
}

/// <summary>Represents a non-list child ViewModel holder used to verify item template validation errors.</summary>
public class CodegenChildHolderVM : ViewModel
{
    [DataSourceProperty]
    public CodegenChildVM Child { get; } = new();
}

public class CodegenItemVM : ViewModel
{
    [DataSourceProperty]
    public string ItemText => "item";
}

public class CodegenOtherVM : ViewModel
{
    [DataSourceProperty]
    public string Title => "other";
}

[ViewModelMixin]
public class CodegenTestVMMixin : BaseViewModelMixin<CodegenTestVM>
{
    public CodegenTestVMMixin(CodegenTestVM vm) : base(vm) { }

    [DataSourceProperty]
    public string MixinText => "mixin";

    [DataSourceProperty]
    public string EditableText { get; set; } = string.Empty;

    [DataSourceProperty]
    public CodegenChildVM Child { get; } = new();

    [DataSourceProperty]
    public MBBindingList<CodegenItemVM> Items { get; } = [];

    /// <summary>Represents an unannotated property omitted from data binding discovery.</summary>
    public string NotBound => string.Empty;

    [DataSourceMethod]
    public void ExecuteMixinCommand() { }

    /// <summary>Represents an unannotated method omitted from command binding discovery.</summary>
    public void NotACommand() { }
}

/// <summary>Simulates an internal ViewModel to verify that generated code accesses internal types correctly.</summary>
internal class CodegenInternalChildVM : ViewModel
{
    [DataSourceProperty]
    public string InternalText => "internal";
}

[ViewModelMixin]
internal class CodegenInternalMixin : BaseViewModelMixin<CodegenTestVM>
{
    public CodegenInternalMixin(CodegenTestVM vm) : base(vm) { }

    [DataSourceProperty]
    public string InternalMixinText => "internal mixin";

    [DataSourceProperty]
    public CodegenInternalChildVM InternalChild { get; } = new();

    [DataSourceMethod]
    public void ExecuteInternalCommand() { }
}

/// <summary>Overrides property definitions from <see cref="CodegenTestVMMixin"/> to test registration precedence rules.</summary>
[ViewModelMixin]
public class CodegenTestVMSecondMixin : BaseViewModelMixin<CodegenTestVM>
{
    public CodegenTestVMSecondMixin(CodegenTestVM vm) : base(vm) { }

    [DataSourceProperty]
    public string MixinText => "second";
}

/// <summary>Simulates a custom widget class registered through <see cref="WidgetFactoryManager"/> rather than standard assembly scanning.</summary>
public class CodegenRegisteredWidget : Widget
{
    public CodegenRegisteredWidget(UIContext context) : base(context) { }
}
