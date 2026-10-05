using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using System;
using System.Collections.Generic;

using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Library.CodeGeneration;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Emits property binding logic in both directions: propagating ViewModel property values to target widgets upon scope assignment
/// and property change events, and writing modified widget properties back into ViewModels.
/// </summary>
internal sealed partial class DatabindingEmitter
{
    /// <summary>
    /// Defines the eight typed notification variants supported by <see cref="ViewModel"/> property changes and their respective payload types.
    /// </summary>
    private static readonly (string Variant, Type PayloadType)[] PropertyChangedValueVariants =
    [
        ("", typeof(object)),
        ("Bool", typeof(bool)),
        ("Int", typeof(int)),
        ("Float", typeof(float)),
        ("UInt", typeof(uint)),
        ("Color", typeof(Color)),
        ("Double", typeof(double)),
        ("Vec2", typeof(Vec2)),
    ];

    private const string BoundByNameComment = "//Not declared on the ViewModel type; bound by name, as the XML loader does";

    // --- ViewModel to widget -------------------------------------------------------------------------------------------

    /// <summary>
    /// Emits property assignment for unresolved ViewModel properties that bind by name.
    /// <para>
    /// When properties cannot be statically resolved on declared types or mixins, accesses members dynamically via <see cref="DynamicMemberType"/>
    /// to support polymorphic runtime subtypes. Applies values using <see cref="WidgetFastAssignment"/> where eligible, falling back to
    /// <c>WidgetExtensions.SetWidgetAttribute</c>.
    /// </para>
    /// </summary>
    private static void AddUnresolvedViewModelPropertyAssignment(MethodCode methodCode, PropertyBinding binding, string ownerVariable, string widgetVariable)
    {
        if (!binding.BindsByName)
        {
            methodCode.AddLine("//Couldn't find property in ViewModel");
            methodCode.AddLine($"//{widgetVariable}.{binding.Property} = {ownerVariable}.{GeneratedLiteral.Comment(binding.Path)};");
            return;
        }
        AddViewModelPropertyAssignmentByName(methodCode, binding, ownerVariable, widgetVariable, BoundByNameComment);
    }

    /// <summary>Emits code reading a ViewModel property dynamically by name and assigning it to the target widget.</summary>
    private static void AddViewModelPropertyAssignmentByName(MethodCode methodCode, PropertyBinding binding, string ownerVariable, string widgetVariable, string comment)
    {
        methodCode.AddLine(comment);
        var read = $"{DynamicMemberType}.Get({ownerVariable}, {GeneratedLiteral.Regular(binding.Path)})";
        if (!WidgetFastAssignment.IsEligible(binding))
        {
            methodCode.AddLine(SetWidgetAttributeLine(binding, widgetVariable, read));
            return;
        }

        // Store dynamically read values in unique local variables to support multiple assignment evaluation checks.
        var local = "uiExtenderExValue_" + widgetVariable + "_" + binding.Property;
        methodCode.AddBlock(null, () =>
        {
            methodCode.AddLine($"object {local} = {read};");
            WidgetFastAssignment.AddFromUntypedValue(methodCode, binding, widgetVariable, local, SetWidgetAttributeLine(binding, widgetVariable, local));
        });
    }

    /// <summary>
    /// Generates a fallback invocation of <c>WidgetExtensions.SetWidgetAttribute</c> to perform Gauntlet XML reflection assignment and string/resource conversions.
    /// </summary>
    private static string SetWidgetAttributeLine(PropertyBinding binding, string widgetVariable, string valueExpression) =>
        $"global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttribute(this.Context, {widgetVariable}, {GeneratedLiteral.Regular(binding.Property)}, {valueExpression});";

    /// <summary>Emits code assigning a ViewModel property value to a widget property, inserting mixin availability guards when applicable.</summary>
    private void AddViewModelPropertyAssignment(MethodCode methodCode, PropertyBinding binding, string ownerVariable, string widgetVariable)
    {
        if (binding.ViewModelPropertyType is not { } viewModelPropertyType)
        {
            return;
        }
        // When an instance lacks the required mixin, fall back to ViewModel dynamic lookup or reset the widget attribute to null.
        Action byNameWithoutMixin = binding.FallsBackToViewModel
            ? () => AddViewModelPropertyAssignmentByName(methodCode, binding, ownerVariable, widgetVariable, WithoutMixinComment)
            : () => methodCode.AddLine(SetWidgetAttributeLine(binding, widgetVariable, "null"));
        if (binding.WidgetPropertyType is not { } widgetPropertyType)
        {
            // Handle complex widget property paths lacking direct setter reflection by delegating to SetWidgetAttribute.
            EmitAgainstReceiver(methodCode, binding.MixinType, ownerVariable, "property", inOwnScope: true,
                receiver => methodCode.AddLine(SetWidgetAttributeLine(binding, widgetVariable, $"{receiver}.{GeneratedNaming.Member(binding.Path)}")),
                byNameWithoutMixin);
            return;
        }

        if (binding.RequiresConversion)
        {
            methodCode.AddLine("//Requires conversion");
        }
        EmitAgainstReceiver(methodCode, binding.MixinType, ownerVariable, "property", inOwnScope: true,
            receiver =>
            {
                var lines = CreateViewModelToWidgetLines($"{receiver}.{GeneratedNaming.Member(binding.Path)}", viewModelPropertyType, $"{widgetVariable}.{binding.Property}", widgetPropertyType);
                // Delegate to SetWidgetAttribute when runtime instances may satisfy unconvertible declared types.
                if (lines.Length == 0 && MayHoldAtRuntime(viewModelPropertyType, widgetPropertyType))
                    lines = [SetWidgetAttributeLine(binding, widgetVariable, $"{receiver}.{GeneratedNaming.Member(binding.Path)}")];
                methodCode.AddLines(lines);
            },
            byNameWithoutMixin);
    }

    /// <summary>
    /// Generates assignment statements converting a ViewModel property value to the target widget property type, matching <c>WidgetExtensions.ConvertObject</c>.
    /// </summary>
    private static string[] CreateViewModelToWidgetLines(string sourceVariable, Type sourceType, string targetVariable, Type targetType)
    {
        if (sourceType == typeof(string) && sourceType != targetType)
        {
            // Convert strings to Sprite, Brush, Color, or integer values matching WidgetExtensions.ConvertObject.
            if (targetType == typeof(Sprite))
                return AssignOrNull(sourceVariable, targetVariable, $"this.Context.SpriteData.GetSprite({sourceVariable})");
            if (targetType == typeof(Brush))
                return AssignOrNull(sourceVariable, targetVariable, $"this.Context.BrushFactory.GetBrush({sourceVariable})");
            if (targetType == typeof(int))
                return AssignWhenNotNull(sourceVariable, targetVariable, $"global::System.Convert.ToInt32({sourceVariable})");
            if (targetType == typeof(Color))
                return AssignWhenNotNull(sourceVariable, targetVariable, $"global::TaleWorlds.Library.Color.ConvertStringToColor({sourceVariable})");
        }
        return CreateDirectAssignmentLines(sourceVariable, sourceType, targetVariable, targetType);
    }

    /// <summary>
    /// Emits direct assignment statements across compatible types or numeric widening conversions via <see cref="WidgetAssignmentConversion"/>.
    /// </summary>
    private static string[] CreateDirectAssignmentLines(string sourceVariable, Type sourceType, string targetVariable, Type targetType) =>
        WidgetAssignmentConversion.GetAssignedExpression(sourceVariable, sourceType, targetType) is { } assigned
            ? [$"{targetVariable} = {assigned};"]
            : [];

    private static string[] AssignOrNull(string sourceVariable, string targetVariable, string conversion) =>
    [
        $"if ({sourceVariable} != null)",
        "{",
        $"{targetVariable} = {conversion};",
        "}",
        "else",
        "{",
        $"{targetVariable} = null;",
        "}",
    ];

    private static string[] AssignWhenNotNull(string sourceVariable, string targetVariable, string conversion) =>
    [
        $"if ({sourceVariable} != null)",
        "{",
        $"{targetVariable} = {conversion};",
        "}",
    ];

    /// <summary>
    /// Determines whether a declared ViewModel type could polymorphically assign to the target widget property type at runtime.
    /// </summary>
    private static bool MayHoldAtRuntime(Type declared, Type widgetPropertyType) =>
        declared != widgetPropertyType && (declared.IsAssignableFrom(widgetPropertyType)
                                           || (declared.IsInterface && !widgetPropertyType.IsSealed)
                                           || (widgetPropertyType.IsInterface && declared.IsClass && !declared.IsSealed));

    // --- widget to ViewModel -------------------------------------------------------------------------------------------

    /// <summary>
    /// The other direction of <see cref="AddUnresolvedViewModelPropertyAssignment"/>. A write by name does nothing when the
    /// instance has no such member or the member has no public setter, which is what the loader does too, so no check is
    /// needed here.
    /// </summary>
    private static void AddUnresolvedWidgetToViewModelAssignment(MethodCode methodCode, PropertyBinding binding, string widgetVariable, string ownerVariable)
    {
        // The widget side is read as typed code, so unlike the other direction it needs a property that exists on the widget class
        if (!binding.BindsByName || binding.WidgetPropertyType == null)
        {
            methodCode.AddLine("//Couldn't find property in ViewModel");
            methodCode.AddLine($"//{ownerVariable}.{GeneratedLiteral.Comment(binding.Path)} = {widgetVariable}.{binding.Property};");
            return;
        }
        AddWidgetToViewModelAssignmentByName(methodCode, binding, ownerVariable, BoundByNameComment);
    }

    /// <summary>The by-name write of <see cref="AddUnresolvedWidgetToViewModelAssignment"/>, whatever led to it.</summary>
    private static void AddWidgetToViewModelAssignmentByName(MethodCode methodCode, PropertyBinding binding, string ownerVariable, string comment)
    {
        methodCode.AddLine(comment);
        // The announced value, as GauntletView.OnViewPropertyChanged hands SetPropertyValue
        methodCode.AddLine($"{DynamicMemberType}.Set({ownerVariable}, {GeneratedLiteral.Regular(binding.Path)}, value);");
    }

    /// <summary>ViewModel property assignment from a widget property, guarded when the property lives on a mixin.</summary>
    private void AddWidgetToViewModelAssignment(MethodCode methodCode, PropertyBinding binding, string widgetVariable, string ownerVariable)
    {
        if (binding.WidgetPropertyType is not { } widgetPropertyType || binding.ViewModelPropertyType is not { } viewModelPropertyType)
        {
            return;
        }

        // Use the value announced by the widget event rather than re-reading the property, preserving writeback ordering for cascading property setters.
        var typeName = ViewModelMemberResolution.GetCodeTypeName(widgetPropertyType);
        var viewModelTypeName = ViewModelMemberResolution.GetCodeTypeName(viewModelPropertyType);
        var usableProperty = GeneratedNaming.GetUsableName(binding.Property);
        var announced = $"uiExtenderExAnnounced_{widgetVariable}_{usableProperty}";
        Action? byNameWithoutMixin = binding.FallsBackToViewModel
            ? () => AddWidgetToViewModelAssignmentByName(methodCode, binding, ownerVariable, WithoutMixinComment)
            : null;
        void AssignTo(Action<string> assign, Action? withoutMixin) =>
            EmitAgainstReceiver(methodCode, binding.MixinType, ownerVariable, "property", inOwnScope: true, assign, withoutMixin);
        void Assign(string sourceVariable) => AssignTo(
            receiver => methodCode.AddLines(CreateDirectAssignmentLines(sourceVariable, widgetPropertyType, $"{receiver}.{GeneratedNaming.Member(binding.Path)}", viewModelPropertyType)),
            byNameWithoutMixin);
        // Write null event payloads back as default values when targets are value types, replicating MethodInfo.Invoke semantics.
        void AssignNull() => AssignTo(receiver => methodCode.AddLine($"{receiver}.{GeneratedNaming.Member(binding.Path)} = default({viewModelTypeName});"), byNameWithoutMixin);

        var reread = $"{widgetVariable}.{binding.Property}";
        // Handle assignments across mutually incompatible types where direct conversions do not exist.
        if (CreateDirectAssignmentLines(reread, widgetPropertyType, reread, viewModelPropertyType).Length == 0 && !binding.FallsBackToViewModel)
        {
            // Truncate numeric values via explicit unchecked cast when narrowing conversions occur (e.g. float to int), matching UIX0025 design guidelines.
            if (typeName is not null && viewModelTypeName is not null && IsNumber(widgetPropertyType) && IsNumber(viewModelPropertyType))
            {
                methodCode.AddBlock($"if (value is {typeName} {announced})", () =>
                    AssignTo(receiver => methodCode.AddLine($"{receiver}.{GeneratedNaming.Member(binding.Path)} = unchecked(({viewModelTypeName}){announced});"), null));
                methodCode.AddBlock("else if (value == null)", AssignNull);
                return;
            }
            if (viewModelTypeName is not null)
            {
                methodCode.AddBlock("if (value == null)", AssignNull);
            }
            // Write polymorphic values directly into ViewModel properties that accept diverse types at runtime.
            if (TakesOtherTypesAtRuntime(viewModelPropertyType))
            {
                methodCode.AddBlock(viewModelTypeName is not null ? "else" : null, () =>
                    AddWidgetToViewModelAssignmentByName(methodCode, binding, ownerVariable, "//Of a type the widget property's does not reach; written back as the loader writes it"));
            }
            return;
        }
        if (typeName is null || viewModelTypeName is null)
        {
            Assign(reread);
            return;
        }
        methodCode.AddBlock($"if (value is {typeName} {announced})", () => Assign(announced));
        methodCode.AddBlock("else if (value == null)", AssignNull);
        // Parse string values back into enum members when widgets announce enum names as strings (e.g. alignment attributes).
        if (widgetPropertyType.IsEnum && !viewModelPropertyType.IsAssignableFrom(typeof(string)))
        {
            var name = $"uiExtenderExAnnouncedName_{widgetVariable}_{usableProperty}";
            var parsed = $"uiExtenderExParsed_{widgetVariable}_{usableProperty}";
            methodCode.AddBlock($"else if (value is global::System.String {name} && global::System.Enum.TryParse<{typeName}>({name}, out {typeName} {parsed}))", () => Assign(parsed));
        }
        // Fall back to dynamic property writeback when announced types differ from declared property types.
        methodCode.AddBlock("else", () =>
            AddWidgetToViewModelAssignmentByName(methodCode, binding, ownerVariable, "//Announced as another type than the widget property's; written back as the loader writes it"));
    }

    /// <summary>Determines whether a ViewModel property type accepts polymorphic runtime assignments (e.g. <see cref="object"/>, interfaces, or unsealed classes).</summary>
    private static bool TakesOtherTypesAtRuntime(Type viewModelPropertyType) =>
        viewModelPropertyType == typeof(object) || viewModelPropertyType.IsInterface || (viewModelPropertyType.IsClass && !viewModelPropertyType.IsSealed);

    /// <summary>Determines whether a type represents a numeric primitive.</summary>
    private static bool IsNumber(Type type) =>
        type == typeof(sbyte) || type == typeof(byte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint)
        || type == typeof(long) || type == typeof(ulong) || type == typeof(float) || type == typeof(double);

    /// <summary>
    /// Emits widget property change listener methods and routing handlers for two-way databinding writeback.
    /// </summary>
    private void CreateWidgetPropertyChangedMethods(ClassCode classCode)
    {
        foreach (var scope in _scopes)
        {
            // Binding list scopes do not support scalar property writeback.
            if (scope.IsList)
            {
                continue;
            }
            var field = scope.FieldName;
            var viewModelType = GetTypeAtPath(scope.Path);
            foreach (var binding in scope.Widgets)
            {
                // Skip writeback generation if no property notifications are monitored.
                if (binding.PropertyBindings.Count == 0 || binding.WritebackNotifications.Count == 0)
                {
                    continue;
                }
                var widgetVariable = binding.Widget.VariableName;
                foreach (var typeVariant in binding.WritebackNotifications)
                {
                    AddWidgetPropertyChangedListener(classCode, widgetVariable, typeVariant);
                }
                var handlerMethod = new MethodCode
                {
                    Name = $"HandleWidgetPropertyChangeOf{widgetVariable}",
                    MethodSignature = "(global::System.String propertyName, global::System.Object value)",
                    AccessModifier = MethodCodeAccessModifier.Private,
                };
                classCode.AddMethod(handlerMethod);
                foreach (var propertyBinding in binding.PropertyBindings.Values)
                {
                    handlerMethod.AddBlock($"if (propertyName == {GeneratedLiteral.Regular(propertyBinding.Property)})", () =>
                    {
                        if (propertyBinding.ViewModelPropertyType == null)
                        {
                            AddUnresolvedWidgetToViewModelAssignment(handlerMethod, propertyBinding, widgetVariable, field);
                        }
                        // Verify public setter availability before generating writeback.
                        else if (ViewModelMemberResolution.GetProperty(viewModelType, propertyBinding.Path, out _)?.GetSetMethod() == null)
                        {
                            handlerMethod.AddLine("//Property in ViewModel does not have a set method");
                            handlerMethod.AddLine($"//{field}.{GeneratedLiteral.Comment(propertyBinding.Path)} = {widgetVariable}.{propertyBinding.Property};");
                        }
                        else
                        {
                            AddWidgetToViewModelAssignment(handlerMethod, propertyBinding, widgetVariable, field);
                        }
                        handlerMethod.AddLine("return;");
                    });
                }
            }
        }
    }

    /// <summary>
    /// Maps a widget notification type variant to its fully qualified C# type name.
    /// </summary>
    private static string WidgetPayloadTypeName(string typeVariant) => typeVariant switch
    {
        "" => "global::System.Object",
        "Vec2" => "global::TaleWorlds.Library.Vec2",
        "Vector2" => "global::System.Numerics.Vector2",
        "Color" => "global::TaleWorlds.Library.Color",
        _ => typeVariant,
    };

    private static void AddWidgetPropertyChangedListener(ClassCode classCode, string widgetVariable, string typeVariant)
    {
        var methodCode = new MethodCode
        {
            Name = $"{typeVariant}PropertyChangedListenerOf{widgetVariable}",
            MethodSignature = $"(global::TaleWorlds.GauntletUI.PropertyOwnerObject propertyOwnerObject, global::System.String propertyName, {WidgetPayloadTypeName(typeVariant)} e)",
            AccessModifier = MethodCodeAccessModifier.Private,
        };
        // Pass the announced widget property value to the dispatch handler.
        methodCode.AddLine($"HandleWidgetPropertyChangeOf{widgetVariable}(propertyName, e);");
        classCode.AddMethod(methodCode);
    }

    // --- ViewModel notifications ---------------------------------------------------------------------------------------

    /// <summary>
    /// Emits ViewModel property change listeners, typed payload handlers, and scope refresh dispatchers for the specified scope.
    /// </summary>
    private void CreateViewModelPropertyChangedMethods(ClassCode classCode)
    {
        foreach (var scope in _scopes)
        {
            if (scope.IsList)
            {
                continue;
            }
            var field = scope.FieldName;
            var listener = new MethodCode
            {
                Name = $"ViewModelPropertyChangedListenerOf{field}",
                MethodSignature = "(global::System.Object sender, global::System.ComponentModel.PropertyChangedEventArgs e)",
                AccessModifier = MethodCodeAccessModifier.Private,
            };
            listener.AddLine($"HandleViewModelPropertyChangeOf{field}(e.PropertyName);");
            classCode.AddMethod(listener);
            var forwardsPayload = CreatePayloadHandlers(classCode, scope);
            foreach (var (typeVariant, _) in PropertyChangedValueVariants)
            {
                AddViewModelPropertyChangedWithValueListener(classCode, field, typeVariant, forwardsPayload);
            }
            var handlerMethod = new MethodCode
            {
                Name = $"HandleViewModelPropertyChangeOf{field}",
                MethodSignature = "(global::System.String propertyName)",
                AccessModifier = MethodCodeAccessModifier.Private,
            };
            classCode.AddMethod(handlerMethod);
            handlerMethod.AddLine("//DataSource property section");
            foreach (var child in scope.Children)
            {
                handlerMethod.AddBlock($"if (propertyName == {GeneratedLiteral.Regular(child.Path.LastNode)})", () =>
                {
                    handlerMethod.AddLine($"RefreshDataSource{child.FieldName}({GetChildDataSourceAccess(scope.Path, child.Path, asObject: child.ObjectFieldName is not null)});");
                    handlerMethod.AddLine("return;");
                });
            }
            handlerMethod.AddLine("//Primitive property section");
            foreach (var (propertyPath, bindings) in GroupByViewModelProperty(scope, _ => true))
            {
                handlerMethod.AddBlock($"if (propertyName == {GeneratedLiteral.Regular(propertyPath)})", () =>
                {
                    foreach (var (binding, propertyBinding) in bindings)
                    {
                        if (propertyBinding.ViewModelPropertyType == null)
                        {
                            AddUnresolvedViewModelPropertyAssignment(handlerMethod, propertyBinding, field, binding.Widget.VariableName);
                        }
                        else
                        {
                            AddViewModelPropertyAssignment(handlerMethod, propertyBinding, field, binding.Widget.VariableName);
                        }
                    }
                    handlerMethod.AddLine("return;");
                });
            }
        }
    }

    /// <summary>
    /// Groups property bindings by ViewModel property name according to the specified filter predicate.
    /// </summary>
    private static Dictionary<string, List<(WidgetBinding Widget, PropertyBinding Binding)>> GroupByViewModelProperty(DataSourceScope scope, Func<PropertyBinding, bool> include)
    {
        var byPath = new Dictionary<string, List<(WidgetBinding Widget, PropertyBinding Binding)>>();
        foreach (var binding in scope.Widgets)
        {
            foreach (var propertyBinding in binding.PropertyBindings.Values)
            {
                if (!include(propertyBinding))
                {
                    continue;
                }
                if (!byPath.TryGetValue(propertyBinding.Path, out var bindings))
                {
                    byPath[propertyBinding.Path] = bindings = [];
                }
                bindings.Add((binding, propertyBinding));
            }
        }
        return byPath;
    }

    /// <summary>Generates the method identifier for a typed ViewModel property change payload handler.</summary>
    private static string PayloadHandlerName(string typeVariant, string field) =>
        $"HandleViewModelPropertyChangeWith{typeVariant}ValueOf{field}";

    private static void AddViewModelPropertyChangedWithValueListener(ClassCode classCode, string field, string typeVariant, bool forwardsPayload)
    {
        var methodCode = new MethodCode
        {
            Name = $"ViewModelPropertyChangedWith{typeVariant}ValueListenerOf{field}",
            MethodSignature = $"(global::System.Object sender, global::TaleWorlds.Library.PropertyChangedWith{typeVariant}ValueEventArgs e)",
            AccessModifier = MethodCodeAccessModifier.Private,
        };
        if (forwardsPayload)
        {
            methodCode.AddBlock($"if ({PayloadHandlerName(typeVariant, field)}(e.PropertyName, e.Value))", () => methodCode.AddLine("return;"));
        }
        methodCode.AddLine($"HandleViewModelPropertyChangeOf{field}(e.PropertyName);");
        classCode.AddMethod(methodCode);
    }

    /// <summary>
    /// Emits specialized typed payload handlers for property change events.
    /// <para>
    /// Assigns notification payload values directly to bound widgets without re-reading properties, avoiding race conditions
    /// with clamped two-way setters and eliminating unnecessary boxing allocations across primitive variants.
    /// Defers to full scope refresh when changed properties represent child data source scopes.
    /// </para>
    /// </summary>
    private static bool CreatePayloadHandlers(ClassCode classCode, DataSourceScope scope)
    {
        // Filter out child data source scopes so full refresh handlers can execute.
        var byPath = GroupByViewModelProperty(scope, x => x.ViewModelPropertyType != null || x.BindsByName);
        foreach (var child in scope.Children)
        {
            byPath.Remove(child.Path.LastNode);
        }
        if (byPath.Count == 0)
        {
            return false;
        }

        foreach (var (typeVariant, payloadType) in PropertyChangedValueVariants)
        {
            AddPayloadHandler(classCode, scope.FieldName, typeVariant, payloadType, byPath);
        }
        return true;
    }

    private static void AddPayloadHandler(ClassCode classCode, string field, string typeVariant, Type payloadType,
        Dictionary<string, List<(WidgetBinding Widget, PropertyBinding Binding)>> byPath)
    {
        var carriesObject = payloadType == typeof(object);
        var handlerMethod = new MethodCode
        {
            Name = PayloadHandlerName(typeVariant, field),
            MethodSignature = $"(global::System.String propertyName, {ViewModelMemberResolution.GetCodeTypeName(payloadType)} value)",
            ReturnParameter = "global::System.Boolean",
            AccessModifier = MethodCodeAccessModifier.Private,
        };
        classCode.AddMethod(handlerMethod);
        if (carriesObject)
        {
            // Ignore ViewModel or binding list payloads in untyped handlers, deferring to scope refresh.
            handlerMethod.AddLine("//A child data source changing is not a widget value; the rereading handler deals with it");
            handlerMethod.AddBlock("if (value is global::TaleWorlds.Library.ViewModel || value is global::TaleWorlds.Library.IMBBindingList)", () => handlerMethod.AddLine("return false;"));
        }
        foreach (var (propertyPath, bindings) in byPath)
        {
            handlerMethod.AddBlock($"if (propertyName == {GeneratedLiteral.Regular(propertyPath)})", () =>
            {
                foreach (var (binding, propertyBinding) in bindings)
                {
                    var widgetVariable = binding.Widget.VariableName;
                    var fallback = SetWidgetAttributeLine(propertyBinding, widgetVariable, "value");
                    if (carriesObject)
                    {
                        WidgetFastAssignment.AddFromUntypedValue(handlerMethod, propertyBinding, widgetVariable, "value", fallback);
                    }
                    else
                    {
                        WidgetFastAssignment.AddFromTypedValue(handlerMethod, propertyBinding, widgetVariable, "value", payloadType, fallback);
                    }
                }
                handlerMethod.AddLine("return true;");
            });
        }
        handlerMethod.AddLine("return false;");
    }
}