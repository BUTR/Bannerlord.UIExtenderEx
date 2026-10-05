
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

using System.Collections.Generic;

using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Emits collection change handlers and item template lifecycle routines, mirroring the item instantiation, repositioning, and disposal behavior of <c>GauntletView</c>.
/// </summary>
internal sealed partial class DatabindingEmitter
{
    /// <summary>Defines the widget lifecycle hook invoked immediately before an item widget is detached from the list.</summary>
    private const string BeforeDeletionAction = "OnBeforeRemovedChild(widget)";

    private static string DeletionAction(bool forDestroy) => forDestroy ? "DestroyDataSource()" : "SetDataSource(null)";

    /// <summary>
    /// Generates an expression accessing the item data source at the specified index.
    /// <para>
    /// Typed lists (<c>MBBindingList&lt;T&gt;</c>) return strongly typed elements. Untyped lists (<c>IMBBindingList</c>)
    /// evaluate via indexer and safe-cast with <c>as ViewModel</c> to avoid runtime cast exceptions when non-ViewModel elements are encountered.
    /// </para>
    /// </summary>
    private static string GetListElementAccess(BindingPathResolution resolution, string dataSourceVariableName, string childIndexVariableName) =>
        resolution.HasElementType
            ? $"{dataSourceVariableName}[{childIndexVariableName}]"
            : $"{dataSourceVariableName}[{childIndexVariableName}] as global::TaleWorlds.Library.ViewModel";

    /// <summary>
    /// Emits code instantiating and configuring a list item widget in accordance with <c>GauntletView.AddItemToList</c>.
    /// <para>
    /// Appends the item as the last child, executes layout initialization (<c>CreateWidgets</c>, <c>SetIds</c>, <c>SetAttributes</c>),
    /// adjusts its sibling index, and binds the item data source.
    /// </para>
    /// </summary>
    /// <param name="siblingIndex">Where the item is moved once built; null leaves it last, for the caller to move.</param>
    /// <param name="bind">Whether it is given its data source here.</param>
    private void AddItemCreation(MethodCode methodCode, WidgetBinding listBinding, PrefabClass itemClass, BindingPathResolution resolution, string variableName, string dataSourceVariableName, string childIndexVariableName, string? siblingIndex, bool bind = true)
    {
        var list = listBinding.Widget.VariableName;
        methodCode.AddLine($"var widgetComponent = new global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData({variableName});");
        methodCode.AddLine($"var dataSource = {GetListElementAccess(resolution, dataSourceVariableName, childIndexVariableName)};");
        methodCode.AddLine("widgetComponent.Data = dataSource;");
        methodCode.AddLine($"{variableName}.AddComponent(widgetComponent);");
        methodCode.AddLine($"{list}.AddChild({variableName});");
        methodCode.AddLine($"{variableName}.CreateWidgets();");
        methodCode.AddLine($"{variableName}.SetIds();");
        methodCode.AddLine($"{variableName}.SetAttributes();");
        if (siblingIndex != null)
            methodCode.AddLine($"{variableName}.SetSiblingIndex({siblingIndex});");
        if (bind)
            AddItemBinding(methodCode, listBinding, itemClass, variableName);
    }

    /// <summary>Emits data source assignment and outer scope handoffs for a newly created list item widget.</summary>
    private void AddItemBinding(MethodCode methodCode, WidgetBinding listBinding, PrefabClass itemClass, string variableName)
    {
        methodCode.AddLine($"{variableName}.SetDataSource(dataSource);");
        AddItemOuterHandoffs(methodCode, listBinding, itemClass, variableName);
    }

    /// <summary>
    /// Emits code instantiating a list item based on active item templates (first, default, or last), matching <c>GauntletView.AddItemToList</c>.
    /// <para>
    /// Applies the first item template when inserting into an empty list. When appending to an existing list that declares a last item template,
    /// rebuilds the preceding item using the default template before appending the new terminal item, preserving exact sibling index assignment
    /// and render seed parity.
    /// </para>
    /// </summary>
    private void AddBindingListItemCreationSection(MethodCode methodCode, WidgetBinding listBinding, ItemTemplateClasses templates, BindingPathResolution resolution, string dataSourceVariableName, string childIndexVariableName, string itemsBeforeVariableName)
    {
        var list = listBinding.Widget.VariableName;
        methodCode.AddBlock(null, () =>
        {
            methodCode.AddLine($"var itemsBefore = {itemsBeforeVariableName};");
            if (templates.First is { } first)
            {
                methodCode.AddLine("//Got first item template");
                methodCode.AddBlock("if (itemsBefore == 0)", () =>
                {
                    methodCode.AddLine($"var item = new {first.ClassName}(this.Context);");
                    AddItemCreation(methodCode, listBinding, first, resolution, "item", dataSourceVariableName, childIndexVariableName, childIndexVariableName);
                });
            }
            if (templates.Last is { } last)
            {
                methodCode.AddLine("//Got last item template");
                methodCode.AddBlock($"{(templates.First != null ? "else " : "")}if (itemsBefore == {childIndexVariableName} && itemsBefore > 0)", () =>
                {
                    methodCode.AddLine("//Rebuild the current last item with the default item template");
                    methodCode.AddBlock(null, () =>
                    {
                        methodCode.AddLine($"var newPreviousItem = new {templates.Default.ClassName}(this.Context);");
                        AddItemCreation(methodCode, listBinding, templates.Default, resolution, "newPreviousItem", dataSourceVariableName, "itemsBefore - 1", siblingIndex: null, bind: false);
                        // Remove item from the widget hierarchy prior to clearing handlers and updating subsequent sibling indices.
                        methodCode.AddLine($"var previousItem = {list}.GetChild(itemsBefore - 1);");
                        methodCode.AddLine($"{list}.RemoveChild(previousItem);");
                        AddItemAction(methodCode, templates, "previousItem", DeletionAction(forDestroy: false));
                        methodCode.AddLine("newPreviousItem.SetSiblingIndex(itemsBefore - 1);");
                        AddItemBinding(methodCode, listBinding, templates.Default, "newPreviousItem");
                    });
                    methodCode.AddLine("//Add new last item");
                    methodCode.AddBlock(null, () =>
                    {
                        methodCode.AddLine($"var item = new {last.ClassName}(this.Context);");
                        AddItemCreation(methodCode, listBinding, last, resolution, "item", dataSourceVariableName, childIndexVariableName, siblingIndex: null, bind: false);
                        methodCode.AddLine($"{list}.GetChild(itemsBefore - 1).SetSiblingIndex(itemsBefore - 1, true);");
                        methodCode.AddLine($"item.SetSiblingIndex({childIndexVariableName}, true);");
                        AddItemBinding(methodCode, listBinding, last, "item");
                    });
                });
            }
            methodCode.AddBlock(templates.First != null || templates.Last != null ? "else" : null, () =>
            {
                methodCode.AddLine($"var item = new {templates.Default.ClassName}(this.Context);");
                AddItemCreation(methodCode, listBinding, templates.Default, resolution, "item", dataSourceVariableName, childIndexVariableName, childIndexVariableName);
            });
        });
    }

    /// <summary>Emits conditional execution of <paramref name="action"/> across possible item template classes.</summary>
    private static void AddItemAction(MethodCode methodCode, ItemTemplateClasses templates, string variableName, string action)
    {
        var classes = new List<PrefabClass>();
        if (templates.First is { } first)
            classes.Add(first);
        if (templates.Last is { } last)
            classes.Add(last);
        for (var i = 0; i < classes.Count; i++)
            methodCode.AddLine($"{(i > 0 ? "else " : "")}if ({variableName} is {classes[i].ClassName} item{i}) {{ {ItemActionStatements($"item{i}", classes[i], action)} }}");
        methodCode.AddLine($"{(classes.Count > 0 ? "else " : "")}{{ var defaultItem = ({templates.Default.ClassName}){variableName}; {ItemActionStatements("defaultItem", templates.Default, action)} }}");
    }

    /// <summary>
    /// Constructs statements executing <paramref name="action"/> on an item and unbinding any external outer scopes when detaching.
    /// </summary>
    private static string ItemActionStatements(string item, PrefabClass itemClass, string action) =>
        action == DeletionAction(forDestroy: false) && itemClass.Databinding is { HasOuterPaths: true }
            ? $"{item}.{action}; {item}.{ClearOuterDataSourcesName}();"
            : $"{item}.{action};";

    /// <summary>
    /// Emits statements executing <paramref name="action"/> on a list item widget and optionally detaching it from the parent list container.
    /// </summary>
    private static void AddBindingListItemSection(MethodCode methodCode, string list, ItemTemplateClasses templates, string childIndexVariableName, string action, bool removeChild)
    {
        methodCode.AddBlock(null, () =>
        {
            methodCode.AddLine($"var widget = {list}.GetChild({childIndexVariableName});");
            if (templates.First != null || templates.Last != null)
            {
                if (templates.First is { } first)
                {
                    methodCode.AddBlock($"if (widget is {first.ClassName})", () => AddAction(first));
                }
                if (templates.Last is { } last)
                {
                    methodCode.AddBlock($"{(templates.First != null ? "else " : "")}if (widget is {last.ClassName})", () => AddAction(last));
                }
                methodCode.AddBlock("else", () => AddAction(templates.Default));
            }
            else
            {
                AddAction(templates.Default);
            }
            if (removeChild)
            {
                methodCode.AddLine($"{list}.RemoveChild(widget);");
            }
        });

        void AddAction(PrefabClass itemClass)
        {
            methodCode.AddLine($"var targetWidget = ({itemClass.ClassName})widget;");
            methodCode.AddLine(ItemActionStatements("targetWidget", itemClass, action));
        }
    }

    private void CreateListChangedMethods(ClassCode classCode)
    {
        foreach (var scope in _scopes)
        {
            if (!scope.IsList)
            {
                continue;
            }
            var field = scope.FieldName;
            var lists = new List<(string List, ItemTemplateClasses Templates, WidgetBinding Binding)>();
            // Track widgets lacking item templates to replicate Gauntlet XML loader failure behavior.
            var withoutTemplate = 0;
            foreach (var binding in scope.Widgets)
            {
                if (binding.ItemTemplates is { } templates)
                {
                    lists.Add((binding.Widget.VariableName, templates, binding));
                }
                // Referenced prefab classes maintain their own list item collections.
                else if (!binding.ItemsBuiltByUsedPrefab)
                {
                    withoutTemplate++;
                }
            }
            var methodCode = new MethodCode
            {
                Name = $"OnList{field}Changed",
                MethodSignature = "(global::System.Object sender, global::TaleWorlds.Library.ListChangedEventArgs e)",
            };
            methodCode.AddBlock("switch (e.ListChangedType)", () =>
            {
                AddCase("Reset", () =>
                {
                    foreach (var (list, templates, _) in lists)
                    {
                        methodCode.AddBlock($"for (var i = {list}.ChildCount - 1; i >= 0; i--)", () =>
                        {
                            AddBindingListItemSection(methodCode, list, templates, "i", BeforeDeletionAction, removeChild: false);
                            AddBindingListItemSection(methodCode, list, templates, "i", DeletionAction(forDestroy: false), removeChild: true);
                        });
                    }
                });
                AddCase("Sorted", () =>
                {
                    methodCode.AddBlock($"for (int i = 0; i < {field}.Count; i++)", () =>
                    {
                        methodCode.AddLine($"var bindingObject = {field}[i];");
                        foreach (var (list, _, _) in lists)
                        {
                            methodCode.AddBlock(null, () =>
                            {
                                methodCode.AddLine($"var target = {list}.FindChild(widget => widget.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>().Data == bindingObject);");
                                methodCode.AddLine("target.SetSiblingIndex(i);");
                            });
                        }
                    });
                    AddWithoutTemplate("Sorted");
                });
                AddCase("ItemAdded", () =>
                {
                    foreach (var (_, templates, listBinding) in lists)
                    {
                        AddBindingListItemCreationSection(methodCode, listBinding, templates, scope.Resolution, field, "e.NewIndex", $"{field}.Count - 1");
                    }
                    AddWithoutTemplate("ItemAdded");
                });
                AddCase("ItemBeforeDeleted", () =>
                {
                    foreach (var (list, templates, _) in lists)
                    {
                        methodCode.AddBlock(null, () => AddBindingListItemSection(methodCode, list, templates, "e.NewIndex", BeforeDeletionAction, removeChild: false));
                    }
                    AddWithoutTemplate("ItemBeforeDeleted");
                });
                AddCase("ItemDeleted", () =>
                {
                    foreach (var (list, templates, _) in lists)
                    {
                        methodCode.AddBlock(null, () => AddBindingListItemSection(methodCode, list, templates, "e.NewIndex", DeletionAction(forDestroy: false), removeChild: true));
                    }
                    AddWithoutTemplate("ItemDeleted");
                });
                // Ignore ItemChanged notifications to mirror GauntletView.OnViewModelBindingListChanged semantics.
                AddCase("ItemChanged", () => methodCode.AddLine(""));
            });
            // Re-evaluate indexed child paths (e.g. '{Perks\0\CandidatePerks}') to track the element at that index following list mutations.
            foreach (var child in scope.Children)
            {
                methodCode.AddBlock(null, () =>
                {
                    methodCode.AddLine($"var indexed = {GetChildDataSourceAccess(scope.Path, child.Path, asObject: child.ObjectFieldName is not null)};");
                    methodCode.AddLine($"if (!global::System.Object.ReferenceEquals(indexed, {child.ObjectAccess})) RefreshDataSource{child.FieldName}(indexed);");
                });
            }
            classCode.AddMethod(methodCode);

            // Replicate Gauntlet XML loader exceptions when list mutations occur on widgets lacking item templates.
            void AddWithoutTemplate(string listChangedType)
            {
                if (withoutTemplate == 0)
                {
                    return;
                }
                methodCode.AddLine("//A widget bound to the list without an item template fails, as the loader's view of it does");
                switch (listChangedType)
                {
                    case "Sorted":
                        methodCode.AddLine($"if ({field}.Count > 0) throw new global::System.NullReferenceException();");
                        break;
                    case "ItemAdded":
                        methodCode.AddLine("throw new global::System.NullReferenceException();");
                        break;
                    default:
                        methodCode.AddLine($"if (e.NewIndex != 0) global::TaleWorlds.Library.Debug.FailedAssert({GeneratedLiteral.Regular("Invalid index for list")});");
                        methodCode.AddLine("var noItem = new global::System.Collections.Generic.List<global::System.Object>()[0];");
                        break;
                }
            }

            void AddCase(string listChangedType, System.Action body)
            {
                methodCode.AddBlock($"case global::TaleWorlds.Library.ListChangedType.{listChangedType}:", body);
                methodCode.AddLine("break;");
            }
        }
    }
}