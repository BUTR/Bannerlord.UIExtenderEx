using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

using System;
using System.Reflection;
using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

/// <summary>
/// Emits C# initialization statements for widget attributes, mirroring the parsing, conversion, and exception semantics of
/// <c>WidgetTemplate.SetAttributes</c> and <c>WidgetExtensions.SetWidgetAttributeFromString</c>.
/// </summary>
internal sealed partial class WidgetNode
{
    /// <summary>
    /// Gets a value indicating whether attribute assignment was abandoned on this widget due to an undefined constant reference.
    /// </summary>
    public bool AttributesAbandoned { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an ancestor widget or an enclosing prefab subtree abandoned attribute processing.
    /// </summary>
    public bool IsBelowAbandonedAttributes
    {
        get
        {
            for (var parent = Parent; parent is not null; parent = parent.Parent)
            {
                if (parent.AttributesAbandoned || parent.UsedPrefabAbandonsChildren)
                {
                    return true;
                }
            }
            return false;
        }
    }

    private bool? _usedPrefabAbandonsChildren;

    /// <summary>
    /// Determines whether the used prefab abandons child attribute evaluation along its logical children path.
    /// </summary>
    private bool UsedPrefabAbandonsChildren => _usedPrefabAbandonsChildren ??=
        !IsBuiltin
        && LoaderChildrenLocation.PathOf(WidgetFactory, Template) is { } path
        && LoaderChildrenLocation.AbandonsAttributes(path);

    public void FillSetAttributesMethod(MethodCode methodCode)
    {
        var values = Owner.Values;
        if (!IsBuiltin && !IsRoot)
        {
            if (PassedChildrenAttributesMethodName is { } passedChildrenAttributes)
            {
                methodCode.AddLine($"{Target}.{PrefabClass.LogicalChildrenAttributesName} = {passedChildrenAttributes};");
            }
            methodCode.AddLine($"{Target}.SetAttributes();");
        }
        foreach (var attribute in Template.AllAttributes)
        {
            var keyType = attribute.KeyType;
            var valueType = attribute.ValueType;
            var propertyName = attribute.Key;
            var attributeValue = attribute.Value;
            // Ids are emitted by FillSetIdsMethod
            if (keyType is WidgetAttributeKeyTypeId)
            {
                continue;
            }
            if (keyType is not WidgetAttributeKeyTypeAttribute)
            {
                AddSkippedAttribute(methodCode, attribute);
                continue;
            }
            switch (valueType)
            {
                case WidgetAttributeValueTypeDefault:
                    AddDefaultAttributeSet(methodCode, propertyName, attributeValue);
                    break;
                case WidgetAttributeValueTypeConstant when !values.Constants.ContainsKey(attributeValue):
                    // WidgetTemplate.SetAttributes asserts on a constant the prefab does not define and returns: the rest of
                    // this widget's attributes, and every attribute of every widget under it, are never set
                    methodCode.AddLine($"//No constant named '{GeneratedLiteral.Comment(attributeValue)}'; the loader asserts and sets nothing more here or below");
                    methodCode.AddLine($"global::TaleWorlds.Library.Debug.FailedAssert({GeneratedLiteral.Regular("Unable to find definition of constant: " + attributeValue)});");
                    AttributesAbandoned = true;
                    break;
                case WidgetAttributeValueTypeConstant:
                    AddConstantAttributeSet(methodCode, propertyName, attributeValue);
                    break;
                case WidgetAttributeValueTypeParameter:
                    AddParameterAttributeSet(methodCode, propertyName, attributeValue);
                    break;
                default:
                    AddSkippedAttribute(methodCode, attribute);
                    break;
            }
            if (AttributesAbandoned)
            {
                break;
            }
        }
        methodCode.AddLine("");
    }

    /// <summary>Records skipped or unhandled XML attributes as informative C# comments in generated source.</summary>
    private static void AddSkippedAttribute(MethodCode methodCode, WidgetAttributeTemplate attribute)
    {
        methodCode.AddLine("//");
        methodCode.AddLine($"//{attribute.KeyType}");
        methodCode.AddLine($"//{attribute.ValueType}");
        methodCode.AddLine($"//{GeneratedLiteral.Comment(attribute.Key)} {GeneratedLiteral.Comment(attribute.Value)}");
        methodCode.AddLine("//");
    }

    /// <summary>
    /// Emits attribute assignment for a constant value, resolving dynamic sprite and brush dimension constants at runtime.
    /// </summary>
    private void AddConstantAttributeSet(MethodCode methodCode, string propertyName, string constantName)
    {
        var values = Owner.Values;
        if (values.ConstantDependsOnResources(constantName)
            && TryGetResolvedAttributeSet(propertyName, PrefabValues.ResolvedValueVariable, out var resolvedAssignment))
        {
            methodCode.AddLine($"//From constant {GeneratedLiteral.Comment(constantName)}, resolved against the widget's own resources");
            if (resolvedAssignment is not null)
            {
                PrefabValues.AddResourceConstantAssignment(methodCode, constantName, Target, propertyName, resolvedAssignment);
            }
            return;
        }
        var constantValue = values.GetConstantValue(constantName);
        methodCode.AddLine($"//From constant {GeneratedLiteral.Comment(constantName)}:{GeneratedLiteral.Comment(constantValue)}");
        // WidgetTemplate.SetAttributes assigns nothing for a constant that comes out empty
        if (!string.IsNullOrEmpty(constantValue))
        {
            AddDefaultAttributeSet(methodCode, propertyName, constantValue);
        }
    }

    /// <summary>
    /// Emits attribute assignment for a parameterized attribute value, applying defaults or supplied arguments.
    /// </summary>
    private void AddParameterAttributeSet(MethodCode methodCode, string propertyName, string parameterName)
    {
        var values = Owner.Values;
        var parameterValue = values.GetParameterDefaultValue(parameterName);
        if (values.GivenParameters.TryGetValue(parameterName, out var givenParameter))
        {
            if (givenParameter.ValueType is WidgetAttributeValueTypeDefault)
            {
                parameterValue = givenParameter.Value;
            }
            else
            {
                methodCode.AddLine($"//parameter below has something different then default value type, {givenParameter.ValueType}");
            }
        }
        methodCode.AddLine($"//From parameter {GeneratedLiteral.Comment(parameterName)}:{GeneratedLiteral.Comment(parameterValue)}");
        if (!string.IsNullOrEmpty(parameterValue))
        {
            AddDefaultAttributeSet(methodCode, propertyName, parameterValue);
        }
    }

    private PropertyInfo? GetAttributeProperty(string propertyName) => WidgetPropertyPath.Resolve(WidgetFactory, Template, propertyName);

    /// <summary>
    /// Determines whether the specified property provides an accessible public setter.
    /// </summary>
    private bool HasPublicSetter(string propertyName) => GetAttributeProperty(propertyName)?.GetSetMethod() is not null;

    /// <summary>
    /// Emits dynamic runtime attribute assignment via <c>WidgetExtensions.SetWidgetAttributeFromString</c> when property types cannot be resolved statically.
    /// </summary>
    private void AddRuntimeResolvedAttributeSet(MethodCode methodCode, string propertyName, string attributeValue, string? reason = null)
    {
        // A single segment names a property of the widget's own type, which is known exactly here: missing is missing
        if (propertyName.IndexOf('.') < 0)
        {
            return;
        }

        methodCode.AddLine(reason is null
            ? $"//'{GeneratedLiteral.Comment(propertyName)}' is not reachable from the declared type, so it is resolved against the live object"
            : $"//'{GeneratedLiteral.Comment(propertyName)}' {reason}");
        methodCode.AddLine(RuntimeResolvedAttributeLine(propertyName, GeneratedLiteral.String(attributeValue)));
    }

    /// <summary>
    /// Generates a C# statement calling <c>WidgetExtensions.SetWidgetAttributeFromString</c> for dynamic runtime attribute assignment.
    /// </summary>
    private string RuntimeResolvedAttributeLine(string propertyName, string valueExpression) =>
        $"global::TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString({Target}, {GeneratedLiteral.String(propertyName)}, {valueExpression}, {Target}.Context.BrushFactory, {Target}.Context.SpriteData, null, null, null, null, null);";

    /// <summary>
    /// Attempts to generate an attribute assignment statement for a runtime-resolved value expression.
    /// </summary>
    private bool TryGetResolvedAttributeSet(string propertyName, string valueExpression, out string? line)
    {
        line = null;
        if (GetAttributeProperty(propertyName)?.PropertyType is not { } attributeType)
        {
            if (propertyName.IndexOf('.') >= 0)
            {
                line = RuntimeResolvedAttributeLine(propertyName, valueExpression);
            }
            return true;
        }
        if (!LoaderStringConversion.TryGetRuntimeConversion(attributeType, valueExpression, out var converted))
        {
            return false;
        }
        line = HasPublicSetter(propertyName)
            ? $"{Target}.{propertyName} = {converted};"
            : PrefabValues.MissingSetterStatement;
        return true;
    }

    /// <summary>
    /// Emits attribute assignment for a known constant or default attribute value, applying TaleWorlds Gauntlet string conversion rules.
    /// </summary>
    private void AddDefaultAttributeSet(MethodCode methodCode, string propertyName, string attributeValue)
    {
        if (GetAttributeProperty(propertyName)?.PropertyType is not { } attributeType)
        {
            AddRuntimeResolvedAttributeSet(methodCode, propertyName, attributeValue);
            return;
        }
        var target = $"{Target}.{propertyName}";
        var hasSetter = HasPublicSetter(propertyName);
        if (LoaderStringConversion.TryGetLiteral(attributeType, attributeValue, out var literal, out var failure))
        {
            // SetWidgetAttributeFromString walks a dotted path before it converts, and the walk has effects of its own: a
            // widget's Brush getter clones the brush and points the renderer at the clone. For a value it then rejects, or a
            // property it cannot set, the loader's own call is what reproduces both the walk and the assert
            if ((literal is null || !hasSetter) && propertyName.IndexOf('.') >= 0)
            {
                AddRuntimeResolvedAttributeSet(methodCode, propertyName, attributeValue,
                    literal is null ? $"does not convert to {attributeType.Name}; the loader walks the path first and asserts" : "has no public setter; the loader walks the path first and asserts");
            }
            else if (literal is null)
            {
                AddLoaderAssert(methodCode, propertyName, attributeValue, failure!, $"does not convert to {attributeType.Name}");
            }
            else if (!hasSetter)
            {
                AddMissingSetterAssert(methodCode, propertyName, attributeValue);
            }
            else
            {
                AddGuardedAssignment(methodCode, propertyName, attributeValue, $"{target} = {literal};");
            }
            return;
        }
        if (attributeType == typeof(XmlElement))
        {
            // The element of that name under the prefab's <CustomElements>, which may be null; a name it does not have
            // assigns nothing, and says nothing
            if (!Owner.Values.CustomElements.TryGetValue(attributeValue, out var element))
            {
                methodCode.AddLine($"//No custom element named '{GeneratedLiteral.Comment(attributeValue)}'; the loader assigns nothing");
            }
            else if (!hasSetter)
            {
                AddMissingSetterAssert(methodCode, propertyName, attributeValue);
            }
            else
            {
                methodCode.AddLine($"//Custom element {GeneratedLiteral.Comment(attributeValue)}");
                if (element is null)
                {
                    AddGuardedAssignment(methodCode, propertyName, attributeValue, $"{target} = null;");
                }
                else
                {
                    AddGuardedAssignment(methodCode, propertyName, attributeValue,
                        "var customElementDocument = new global::System.Xml.XmlDocument();",
                        $"customElementDocument.LoadXml({GeneratedLiteral.String(element.OuterXml)});",
                        $"{target} = customElementDocument.DocumentElement;");
                }
            }
        }
        else if (typeof(Widget).IsAssignableFrom(attributeType))
        {
            // The loader's own call. It runs FindChild from this widget over the tree as it stands when the attributes are
            // set, which a walk over the template cannot stand in for: a child that sits in a prefab's logical children
            // location is not a child of the widget that declares it, a prefab's own widgets can carry the same Id, and a
            // widget of the wrong type is left unassigned rather than assigned null.
            methodCode.AddLine($"//Widget found by path when the attributes are set - {GeneratedLiteral.Comment(attributeValue)}");
            methodCode.AddLine(RuntimeResolvedAttributeLine(propertyName, GeneratedLiteral.String(attributeValue)));
        }
        else if (attributeType == typeof(VisualDefinition))
        {
            // The loader looks the name up in the prefab's visual definitions first, and throws on one it lacks
            if (Owner.Values.GetVisualDefinitionCreatorName(attributeValue) is not { } creatorMethodName)
            {
                AddLoaderAssert(methodCode, propertyName, attributeValue, LoaderStringConversion.MissingKeyFailure(attributeValue), "names no visual definition");
            }
            else if (!hasSetter)
            {
                AddMissingSetterAssert(methodCode, propertyName, attributeValue);
            }
            else
            {
                // SetWidgetAttributeFromStringAux creates the definition before it invokes the setter, inside the same catch:
                // what creating it throws is quoted as it is, what the setter throws through reflection's wrapper
                const string created = "uiExtenderExCreatedVisualDefinition";
                const string creationFailure = "uiExtenderExAttributeFailure";
                methodCode.AddBlock(null, () =>
                {
                    methodCode.AddLine($"global::TaleWorlds.GauntletUI.VisualDefinition {created} = null;");
                    methodCode.AddBlock("try", () =>
                    {
                        methodCode.AddLine($"{created} = {creatorMethodName}();");
                        methodCode.AddLine($"{target} = {created};");
                    });
                    methodCode.AddBlock($"catch (global::System.Exception {creationFailure})", () =>
                        methodCode.AddLine($"{LoaderStringConversion.LoaderAssertsType}.AttributeNotSet({Target}, {GeneratedLiteral.Regular(propertyName)}, {GeneratedLiteral.Regular(attributeValue)}, " +
                            $"{created} == null ? {creationFailure}.Message : {LoaderStringConversion.LoaderAssertsType}.SetterFailure);"));
                });
            }
        }
    }

    /// <summary>
    /// Wraps property assignment statements in a try/catch block to emulate TaleWorlds Gauntlet attribute failure handling.
    /// </summary>
    private void AddGuardedAssignment(MethodCode methodCode, string propertyName, string attributeValue, params string[] statements)
    {
        const string failure = "uiExtenderExAttributeFailure";
        methodCode.AddBlock("try", () => methodCode.AddLines(statements));
        if (propertyName.IndexOf('.') >= 0)
        {
            methodCode.AddBlock($"catch (global::System.Exception {failure})", () =>
                methodCode.AddLine($"{LoaderStringConversion.LoaderAssertsType}.PathOrSetterThrew({Target}, " +
                    $"{GeneratedLiteral.Regular(propertyName)}, {GeneratedLiteral.Regular(attributeValue)}, {failure});"));
        }
        else
        {
            methodCode.AddBlock("catch (global::System.Exception)", () =>
                methodCode.AddLine(LoaderStringConversion.SetterThrewLine(Target, propertyName, attributeValue)));
        }
    }

    /// <summary>Emits an assertion failure when an attribute property lacks a public setter.</summary>
    private void AddMissingSetterAssert(MethodCode methodCode, string propertyName, string attributeValue) =>
        AddLoaderAssert(methodCode, propertyName, attributeValue, LoaderStringConversion.MissingSetterFailure, "has no public setter");

    /// <summary>Emits a Gauntlet-compatible assertion failure comment and statement for an attribute that failed to assign.</summary>
    private void AddLoaderAssert(MethodCode methodCode, string propertyName, string attributeValue, string failure, string why)
    {
        methodCode.AddLine($"//'{GeneratedLiteral.Comment(propertyName)}' {why}; the loader asserts and assigns nothing");
        methodCode.AddLine(LoaderStringConversion.AssertLine(Target, propertyName, attributeValue, failure));
    }
}