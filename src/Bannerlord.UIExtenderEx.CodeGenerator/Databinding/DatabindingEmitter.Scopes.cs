using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.Library;
using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Emits data source refresh routines for all scopes, coordinating unbinding and unsubscription of existing data sources
/// followed by assignment and subscription of new data sources in hierarchical order.
/// </summary>
internal sealed partial class DatabindingEmitter
{
    private void CreateRefreshDataSourceMethods(ClassCode classCode)
    {
        foreach (var scope in _scopes)
        {
            var methodCode = new MethodCode
            {
                Name = $"RefreshDataSource{scope.FieldName}",
                MethodSignature = $"({(scope.ObjectFieldName is not null ? "global::System.Object" : scope.Resolution.EmittedTypeName)} newDataSource)",
                AccessModifier = MethodCodeAccessModifier.Private,
            };
            methodCode.AddLine("//Clear Section");
            AddCommandListenersBelow(methodCode, scope, add: false);
            FillClearSection(scope, methodCode, forDestroy: false);
            methodCode.AddLine("");
            if (scope.ObjectFieldName is { } objectField)
            {
                methodCode.AddLine($"{objectField} = newDataSource;");
                methodCode.AddLine($"{scope.FieldName} = newDataSource as {scope.Resolution.EmittedTypeName};");
            }
            else
            {
                methodCode.AddLine($"{scope.FieldName} = newDataSource; ");
            }
            methodCode.AddLine("");
            methodCode.AddLine("//Assign Section");
            FillAssignSection(scope, methodCode);
            classCode.AddMethod(methodCode);
        }
    }

    /// <summary>
    /// Emits cleanup logic to detach the active data source from a scope and all its descendant scopes.
    /// <para>
    /// <paramref name="forDestroy"/> indicates whether the cleanup occurs as part of class disposal (<c>DestroyDataSource</c>)
    /// or standard data source unbinding.
    /// </para>
    /// </summary>
    private void FillClearSection(DataSourceScope scope, MethodCode methodCode, bool forDestroy)
    {
        if (scope.ObjectFieldName is not null)
        {
            FillObjectClearSection(scope, methodCode, forDestroy);
            return;
        }
        var field = scope.FieldName;
        methodCode.AddBlock($"if ({field} != null)", () =>
        {
            foreach (var binding in scope.Widgets)
            {
                if (binding.BindsItsPrefabAtItsScope)
                {
                    AddPrefabRelease(methodCode, binding, forDestroy);
                }
                AddWidgetDataAssignment(methodCode, binding, null);
            }
            foreach (var binding in scope.PrefabHandoffs)
            {
                // Root widgets delegate data source propagation to base classes via base.SetDataSource.
                if (!binding.IsRoot)
                {
                    AddPrefabRelease(methodCode, binding, forDestroy);
                }
            }
            AddOuterHandoffs(methodCode, scope, "null");
            if (scope.IsList)
            {
                methodCode.AddLine($"{field}.ListChanged -= OnList{field}Changed;");
                foreach (var binding in scope.Widgets)
                {
                    if (binding.ItemTemplates is { } templates)
                    {
                        methodCode.AddLine($"//Binding path list: {GeneratedLiteral.Comment(scope.Path.Path.ToString())}");
                        var list = binding.Widget.VariableName;
                        methodCode.AddBlock($"for (var i = {list}.ChildCount - 1; i >= 0; i--)", () =>
                        {
                            AddBindingListItemSection(methodCode, list, templates, "i", BeforeDeletionAction, removeChild: false);
                            AddBindingListItemSection(methodCode, list, templates, "i", DeletionAction(forDestroy), removeChild: !forDestroy);
                        });
                    }
                }
            }
            else
            {
                AddViewModelListeners(methodCode, scope, add: false);
                foreach (var binding in scope.Widgets)
                {
                    AddWidgetPropertyListeners(methodCode, binding, add: false);
                }
            }
            foreach (var child in scope.Children)
            {
                FillClearSection(child, methodCode, forDestroy);
            }
            foreach (var index in MixinReceiversOf(field))
            {
                methodCode.AddLine($"{MixinOwnerField(index)} = null;");
                methodCode.AddLine($"{MixinField(index)} = null;");
            }
            methodCode.AddLine($"{field} = null;");
        });
    }

    /// <summary>Emits logic binding a newly supplied data source across a scope and all its descendant scopes.</summary>
    private void FillAssignSection(DataSourceScope scope, MethodCode methodCode)
    {
        if (scope.ObjectFieldName is not null)
        {
            FillObjectAssignSection(scope, methodCode);
            return;
        }
        var field = scope.FieldName;
        // Read child scope data sources from their parent scopes, except for root and outer scopes which receive data sources directly.
        if (!scope.IsRoot && !scope.IsOuter)
        {
            methodCode.AddLine($"{field} = {GetChildDataSourceAccess(scope.Path.ParentPath, scope.Path)};");
        }
        methodCode.AddBlock($"if ({field} != null)", () =>
        {
            if (scope.IsList)
            {
                methodCode.AddLine($"{field}.ListChanged += OnList{field}Changed;");
                foreach (var binding in scope.Widgets)
                {
                    if (binding.ItemTemplates is { } templates)
                    {
                        methodCode.AddLine($"//Binding path list: {GeneratedLiteral.Comment(scope.Path.Path.ToString())}");
                        methodCode.AddBlock($"for (var i = 0; i < {field}.Count; i++)", () =>
                            AddBindingListItemCreationSection(methodCode, binding, templates, scope.Resolution, field, "i", "i"));
                    }
                    // Referenced prefab classes construct items from their own root item templates.
                    else if (!binding.ItemsBuiltByUsedPrefab)
                    {
                        // A list without item templates throws in GauntletView.AddItemToList upon receiving items.
                        methodCode.AddLine("//No item template: the loader's AddItemToList throws on the first item");
                        methodCode.AddLine($"if ({field}.Count > 0) throw new global::System.NullReferenceException();");
                    }
                    // Attach the list instance to the widget's data component to ensure command parameters resolve correctly.
                    AddWidgetDataAssignment(methodCode, binding, field);
                    AddEventListener(methodCode, binding, add: true);
                }
            }
            else
            {
                AddViewModelListeners(methodCode, scope, add: true);
                foreach (var binding in scope.Widgets)
                {
                    foreach (var propertyBinding in binding.PropertyBindings.Values)
                    {
                        if (propertyBinding.ViewModelPropertyType == null)
                        {
                            AddUnresolvedViewModelPropertyAssignment(methodCode, propertyBinding, field, binding.Widget.VariableName);
                        }
                        else
                        {
                            AddViewModelPropertyAssignment(methodCode, propertyBinding, field, binding.Widget.VariableName);
                        }
                    }
                    AddWidgetDataAssignment(methodCode, binding, field);
                    AddEventListener(methodCode, binding, add: true);
                    AddWidgetPropertyListeners(methodCode, binding, add: true);
                }
            }
            foreach (var child in scope.Children)
            {
                FillAssignSection(child, methodCode);
            }
            foreach (var binding in scope.Widgets)
            {
                if (binding.BindsItsPrefabAtItsScope)
                {
                    methodCode.AddLine($"{binding.Widget.VariableName}.SetDataSource({field});");
                }
            }
            // Pass the surrounding scope to referenced prefabs that adopt their root data sources.
            foreach (var binding in scope.PrefabHandoffs)
            {
                // Root widgets delegate data source propagation to base classes via base.SetDataSource.
                if (!binding.IsRoot)
                {
                    methodCode.AddLine($"{binding.Widget.VariableName}.SetDataSource({field});");
                }
            }
            AddOuterHandoffs(methodCode, scope, field);
        });
        AddNullScopeSection(scope, methodCode);
    }

    /// <summary>
    /// Emits clear logic for dynamically typed object scopes, unbinding listeners, clearing widget data, and tearing down child scopes.
    /// </summary>
    private void FillObjectClearSection(DataSourceScope scope, MethodCode methodCode, bool forDestroy)
    {
        var field = scope.FieldName;
        var objectField = scope.ObjectFieldName!;
        methodCode.AddBlock($"if ({objectField} != null)", () =>
        {
            foreach (var binding in scope.Widgets)
            {
                if (binding.BindsItsPrefabAtItsScope)
                {
                    AddPrefabRelease(methodCode, binding, forDestroy);
                }
                AddWidgetDataAssignment(methodCode, binding, null);
            }
            foreach (var binding in scope.PrefabHandoffs)
            {
                if (!binding.IsRoot)
                {
                    AddPrefabRelease(methodCode, binding, forDestroy);
                }
            }
            AddOuterHandoffs(methodCode, scope, "null");
            if (scope.Widgets.Count > 0)
            {
                methodCode.AddLine($"if ({objectField} is {HeldListType}) (({HeldListType}){objectField}).ListChanged -= OnHeldList{field}Changed;");
            }
            methodCode.AddBlock($"if ({field} != null)", () =>
            {
                AddViewModelListeners(methodCode, scope, add: false);
                foreach (var binding in scope.Widgets)
                {
                    AddWidgetPropertyListeners(methodCode, binding, add: false);
                }
            });
            foreach (var child in scope.Children)
            {
                FillClearSection(child, methodCode, forDestroy);
            }
            foreach (var index in MixinReceiversOf(field))
            {
                methodCode.AddLine($"{MixinOwnerField(index)} = null;");
                methodCode.AddLine($"{MixinField(index)} = null;");
            }
            methodCode.AddLine($"{field} = null;");
            methodCode.AddLine($"{objectField} = null;");
        });
    }

    /// <summary>
    /// Emits assignment logic for dynamically typed object scopes, establishing ViewModel listeners when holding ViewModels
    /// while maintaining widget data references and forwarding scopes.
    /// </summary>
    private void FillObjectAssignSection(DataSourceScope scope, MethodCode methodCode)
    {
        var field = scope.FieldName;
        var objectField = scope.ObjectFieldName!;
        if (!scope.IsRoot && !scope.IsOuter)
        {
            methodCode.AddLine($"{objectField} = {GetChildDataSourceAccess(scope.Path.ParentPath, scope.Path, asObject: true)};");
            methodCode.AddLine($"{field} = {objectField} as {scope.Resolution.EmittedTypeName};");
        }
        methodCode.AddBlock($"if ({objectField} != null)", () =>
        {
            // Subscribe collection change handlers if a dynamically held object represents an IMBBindingList.
            if (scope.Widgets.Count > 0)
            {
                methodCode.AddBlock($"if ({objectField} is {HeldListType})", () =>
                {
                    methodCode.AddLine($"(({HeldListType}){objectField}).ListChanged += OnHeldList{field}Changed;");
                    methodCode.AddLine($"if ((({HeldListType}){objectField}).Count > 0) throw new global::System.NullReferenceException();");
                });
            }
            methodCode.AddBlock($"if ({field} != null)", () =>
            {
                AddViewModelListeners(methodCode, scope, add: true);
                foreach (var binding in scope.Widgets)
                {
                    foreach (var propertyBinding in binding.PropertyBindings.Values)
                    {
                        if (propertyBinding.ViewModelPropertyType == null)
                        {
                            AddUnresolvedViewModelPropertyAssignment(methodCode, propertyBinding, field, binding.Widget.VariableName);
                        }
                        else
                        {
                            AddViewModelPropertyAssignment(methodCode, propertyBinding, field, binding.Widget.VariableName);
                        }
                    }
                    AddWidgetPropertyListeners(methodCode, binding, add: true);
                }
            });
            foreach (var binding in scope.Widgets)
            {
                AddWidgetDataAssignment(methodCode, binding, objectField);
                AddEventListener(methodCode, binding, add: true);
            }
            foreach (var child in scope.Children)
            {
                FillAssignSection(child, methodCode);
            }
            foreach (var binding in scope.Widgets)
            {
                if (binding.BindsItsPrefabAtItsScope)
                {
                    methodCode.AddLine($"{binding.Widget.VariableName}.SetDataSource({field});");
                }
            }
            foreach (var binding in scope.PrefabHandoffs)
            {
                if (!binding.IsRoot)
                {
                    methodCode.AddLine($"{binding.Widget.VariableName}.SetDataSource({field});");
                }
            }
            AddOuterHandoffs(methodCode, scope, objectField);
        });
        AddNullScopeSection(scope, methodCode);
    }

    private const string HeldListType = "global::TaleWorlds.Library.IMBBindingList";

    /// <summary>
    /// Emits collection change handlers for dynamically held binding lists on scalar widget bindings, matching Gauntlet XML loader failure modes.
    /// </summary>
    private void CreateHeldListChangedMethods(ClassCode classCode)
    {
        foreach (var scope in _scopes)
        {
            if (scope.ObjectFieldName is null || scope.Widgets.Count == 0)
            {
                continue;
            }
            var methodCode = new MethodCode
            {
                Name = $"OnHeldList{scope.FieldName}Changed",
                MethodSignature = "(global::System.Object sender, global::TaleWorlds.Library.ListChangedEventArgs e)",
                AccessModifier = MethodCodeAccessModifier.Private,
            };
            methodCode.AddLine("//The prefab binds a ViewModel here, and the loader's view of a list without an item template fails on what touches an item");
            methodCode.AddBlock("switch (e.ListChangedType)", () =>
            {
                methodCode.AddLine("case global::TaleWorlds.Library.ListChangedType.ItemAdded:");
                methodCode.AddLine("    throw new global::System.NullReferenceException();");
                methodCode.AddLine("case global::TaleWorlds.Library.ListChangedType.Sorted:");
                methodCode.AddLine($"    if ((({HeldListType})sender).Count > 0) throw new global::System.NullReferenceException();");
                methodCode.AddLine("    break;");
                methodCode.AddLine("case global::TaleWorlds.Library.ListChangedType.ItemBeforeDeleted:");
                methodCode.AddLine("case global::TaleWorlds.Library.ListChangedType.ItemDeleted:");
                methodCode.AddLine($"    if (e.NewIndex != 0) global::TaleWorlds.Library.Debug.FailedAssert({GeneratedLiteral.Regular("Invalid index for list")});");
                methodCode.AddLine("    var noItem = new global::System.Collections.Generic.List<global::System.Object>()[0];");
                methodCode.AddLine("    break;");
            });
            classCode.AddMethod(methodCode);
        }
    }

    private static void AddPrefabRelease(MethodCode methodCode, WidgetBinding binding, bool forDestroy) =>
        methodCode.AddLine(forDestroy
            ? $"{binding.Widget.VariableName}.DestroyDataSource();"
            : $"{binding.Widget.VariableName}.SetDataSource(null);");

    /// <summary>
    /// Emits statements assigning or clearing the data source stored in the widget's <see cref="GeneratedWidgetData"/> component.
    /// </summary>
    private static void AddWidgetDataAssignment(MethodCode methodCode, WidgetBinding binding, string? value) =>
        methodCode.AddBlock(null, () =>
        {
            methodCode.AddLine(value is null ? "//Requires component data to be cleared" : "//Requires component data assignment");
            methodCode.AddLine($"var widgetComponent = {binding.Widget.VariableName}.GetComponent<global::TaleWorlds.GauntletUI.Data.GeneratedWidgetData>();");
            methodCode.AddLine($"widgetComponent.Data = {value ?? "null"};");
        });

    /// <summary>Subscribes or unsubscribes property change event handlers for a ViewModel scope, including untyped and typed variants.</summary>
    private static void AddViewModelListeners(MethodCode methodCode, DataSourceScope scope, bool add)
    {
        var field = scope.FieldName;
        var sign = add ? "+" : "-";
        methodCode.AddLine($"//Binding path: {GeneratedLiteral.Comment(scope.Path.Path.ToString())}");
        methodCode.AddLine($"{field}.PropertyChanged {sign}= ViewModelPropertyChangedListenerOf{field};");
        foreach (var (typeVariant, _) in PropertyChangedValueVariants)
        {
            methodCode.AddLine($"{field}.PropertyChangedWith{typeVariant}Value {sign}=ViewModelPropertyChangedWith{typeVariant}ValueListenerOf{field};");
        }
    }

    /// <summary>Attaches or detaches command event listeners for all widgets in the class.</summary>
    public void AddCommandListeners(MethodCode methodCode, bool add)
    {
        foreach (var binding in WidgetBindings)
        {
            AddEventListener(methodCode, binding, add);
        }
    }

    /// <summary>
    /// Attaches or detaches command event listeners across a scope and all its descendant scopes.
    /// <para>
    /// Matches <c>GauntletView.RefreshBinding</c> by detaching command handlers prior to property write operations
    /// to prevent side-effect command execution during refresh.
    /// </para>
    /// </summary>
    private static void AddCommandListenersBelow(MethodCode methodCode, DataSourceScope scope, bool add)
    {
        foreach (var binding in scope.Widgets)
        {
            AddEventListener(methodCode, binding, add);
        }
        foreach (var child in scope.Children)
        {
            AddCommandListenersBelow(methodCode, child, add);
        }
    }

    /// <summary>
    /// Emits fallback handler attachment and null propagation when a scope receives <see langword="null"/>.
    /// <para>
    /// Preserves command event listener registration and propagates null data sources downward to child scopes and prefabs,
    /// in parity with <c>GauntletView.RefreshBindingWithChildren</c>.
    /// </para>
    /// </summary>
    private void AddNullScopeSection(DataSourceScope scope, MethodCode methodCode)
    {
        if (!RefreshesWithNull(scope))
        {
            return;
        }
        var held = scope.ObjectFieldName ?? scope.FieldName;
        methodCode.AddBlock($"if ({held} == null)", () => FillNullScope(scope, methodCode));
    }

    private void FillNullScope(DataSourceScope scope, MethodCode methodCode)
    {
        foreach (var binding in scope.Widgets)
        {
            AddEventListener(methodCode, binding, add: true);
        }
        foreach (var child in scope.Children)
        {
            FillNullScope(child, methodCode);
        }
        foreach (var binding in PrefabsBoundAt(scope))
        {
            methodCode.AddLine($"{binding.Widget.VariableName}.SetDataSource(null);");
        }
    }

    private bool RefreshesWithNull(DataSourceScope scope) =>
        scope.Widgets.Any(x => x.CommandBindings.Count > 0) || PrefabsBoundAt(scope).Any() || scope.Children.Any(RefreshesWithNull);

    /// <summary>Enumerates all prefab instances bound to the specified scope or receiving its data source.</summary>
    private static IEnumerable<WidgetBinding> PrefabsBoundAt(DataSourceScope scope) =>
        scope.Widgets.Where(x => x.BindsItsPrefabAtItsScope).Concat(scope.PrefabHandoffs.Where(x => !x.IsRoot));

    /// <summary>Subscribes or unsubscribes a widget's command event listener.</summary>
    private static void AddEventListener(MethodCode methodCode, WidgetBinding binding, bool add)
    {
        if (binding.CommandBindings.Count > 0)
        {
            var widget = binding.Widget.VariableName;
            methodCode.AddLine($"{widget}.EventFire {(add ? "+" : "-")}= EventListenerOf{widget};");
        }
    }

    /// <summary>
    /// Subscribes or unsubscribes widget property change notification listeners for writeback.
    /// </summary>
    private static void AddWidgetPropertyListeners(MethodCode methodCode, WidgetBinding binding, bool add)
    {
        if (binding.PropertyBindings.Count > 0)
        {
            var widget = binding.Widget.VariableName;
            foreach (var typeVariant in binding.WritebackNotifications)
            {
                methodCode.AddLine($"{widget}.{typeVariant}PropertyChanged {(add ? "+" : "-")}= {typeVariant}PropertyChangedListenerOf{widget};");
            }
        }
    }

    /// <summary>
    /// Generates an expression resolving the child data source one level below <paramref name="ownerPath"/>.
    /// <para>
    /// Emits direct member access for strongly typed ViewModel properties and mixin access for mixin properties.
    /// Falls back to dynamic lookup (<c>DynamicMember.Step</c> or <c>DynamicMember.GetChild</c>) for unresolved types, lists, and polymorphic objects.
    /// </para>
    /// </summary>
    private string GetChildDataSourceAccess(BindingPath ownerPath, BindingPath childPath, bool asObject = false)
    {
        var owner = Scope(ownerPath);
        var ownerVariable = owner.FieldName;
        var memberName = childPath.LastNode;
        var member = GeneratedLiteral.Regular(memberName);
        var childType = Scope(childPath).Resolution.EmittedTypeName;
        string Typed(string objectExpression) => asObject ? objectExpression : $"{objectExpression} as {childType}";

        if (owner.ObjectFieldName is { } ownerObject)
        {
            return Typed($"{DynamicMemberType}.Step({ownerObject}, {member})");
        }
        // List indices evaluate through GetListItem.
        if (owner.IsList)
        {
            return Typed($"{DynamicMemberType}.GetListItem({ownerVariable}, {member})");
        }

        var ownerType = GetTypeAtPath(ownerPath);
        if (ViewModelMemberResolution.GetProperty(ownerType, memberName, out var mixinType) is not null)
        {
            // Inspect declared member types and mixin receiver properties.
            var declaresDataSource = GetTypeAtPath(childPath) is not null;
            if (mixinType == null)
            {
                return asObject || !declaresDataSource
                    ? Typed($"{DynamicMemberType}.AsDataSource({ownerVariable}.{GeneratedNaming.Member(memberName)})")
                    : $"{ownerVariable}.{GeneratedNaming.Member(memberName)}";
            }
            var receiver = MixinReceiver(ownerVariable, mixinType);
            if (asObject || !declaresDataSource)
            {
                return Typed(ViewModelMemberResolution.ViewModelAnswersProperty(ownerType, memberName)
                    ? $"({receiver} != null ? {DynamicMemberType}.AsDataSource({receiver}.{GeneratedNaming.Member(memberName)}) : {DynamicMemberType}.GetChild({ownerVariable}, {member}))"
                    : $"{DynamicMemberType}.AsDataSource({receiver}?.{GeneratedNaming.Member(memberName)})");
            }
            return ViewModelMemberResolution.ViewModelAnswersProperty(ownerType, memberName)
                ? $"({receiver} != null ? {receiver}.{GeneratedNaming.Member(memberName)} : {DynamicMemberType}.GetChild({ownerVariable}, {member}) as {childType})"
                : $"{receiver}?.{GeneratedNaming.Member(memberName)}";
        }

        return Typed($"{DynamicMemberType}.GetChild({ownerVariable}, {member})");
    }
}