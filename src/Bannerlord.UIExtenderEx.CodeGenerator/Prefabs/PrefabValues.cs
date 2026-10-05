using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library.CodeGeneration;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

/// <summary>
/// Manages named prefab values (constants, parameters, visual definitions, and custom XML elements) and emits code to instantiate them.
/// </summary>
/// <remarks>
/// Handles emission of visual definition builder methods and runtime constant resolvers for values that depend on sprite or brush dimensions.
/// </remarks>
public sealed class PrefabValues
{
    private readonly BrushFactory _brushFactory;

    private readonly SpriteData _spriteData;

    /// <summary>Stores the prefab's visual definitions indexed by template name.</summary>
    private readonly Dictionary<string, VisualDefinitionTemplate> _visualDefinitions = new();

    /// <summary>
    /// Maps visual definition template names to unique generated C# method names to prevent identifier collisions.
    /// </summary>
    private readonly Dictionary<string, string> _visualDefinitionCreatorNames = new();

    public PrefabValues(BrushFactory brushFactory, SpriteData spriteData)
    {
        _brushFactory = brushFactory;
        _spriteData = spriteData;
    }

    /// <summary>Gets the collection of constant definitions declared by the prefab.</summary>
    public Dictionary<string, ConstantDefinition> Constants { get; } = new();

    /// <summary>Gets declared prefab parameters mapped to their default string values.</summary>
    public Dictionary<string, string> ParameterDefaults { get; } = new();

    /// <summary>Gets the parameters provided to the prefab via <c>Parameter.X</c> attributes on the referencing widget.</summary>
    public Dictionary<string, WidgetAttributeTemplate> GivenParameters { get; private set; } = new();

    /// <summary>
    /// Gets the parameters available during databinding resolution, including transitively inherited parameters from enclosing prefabs.
    /// </summary>
    /// <remarks>
    /// <c>PrefabDatabindingExtension.AfterAttributesSet</c> propagates enclosing prefab parameters down to nested prefabs
    /// when not explicitly overridden, whereas <c>WidgetTemplate.SetAttributes</c> only observes directly supplied attributes.
    /// </remarks>
    public Dictionary<string, WidgetAttributeTemplate> BindingParameters { get; private set; } = new();

    /// <summary>
    /// Gets the custom XML elements declared within <c>&lt;CustomElements&gt;</c>, indexed by name.
    /// </summary>
    public Dictionary<string, XmlElement?> CustomElements { get; } = new();

    public void SetGivenParameters(Dictionary<string, WidgetAttributeTemplate> givenParameters, Dictionary<string, WidgetAttributeTemplate> bindingParameters)
    {
        GivenParameters = givenParameters;
        BindingParameters = bindingParameters;
    }

    public void FillFromPrefab(WidgetPrefab prefab)
    {
        foreach (var constantDefinition in prefab.Constants.Values)
        {
            Constants.Add(constantDefinition.Name, constantDefinition);
        }
        foreach (var (parameterName, defaultValue) in prefab.Parameters)
        {
            ParameterDefaults.Add(parameterName, defaultValue);
        }
        var takenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var visualDefinitionTemplate in prefab.VisualDefinitionTemplates.Values)
        {
            _visualDefinitions.Add(visualDefinitionTemplate.Name, visualDefinitionTemplate);
            _visualDefinitionCreatorNames.Add(visualDefinitionTemplate.Name,
                GeneratedNaming.GetUniqueName($"CreateVisualDefinition{GeneratedNaming.GetUsableName(visualDefinitionTemplate.Name)}", takenNames));
        }
        if (prefab.CustomElements is { } customElements)
        {
            foreach (var (elementName, element) in customElements)
            {
                CustomElements[elementName] = element;
            }
        }
    }

    public string GetConstantValue(string constantName)
    {
        var constantDefinition = Constants[constantName];
        // Throws if a constant circular dependency is detected to avoid stack overflow crashes.
        if (ConstantResourceAnalysis.ReferencesItself(constantDefinition, Constants))
        {
            throw new InvalidOperationException($"Constant '{constantName}' refers to itself, which the loader cannot resolve either.");
        }
        return constantDefinition.GetValue(_brushFactory, _spriteData, Constants, GivenParameters, ParameterDefaults);
    }

    public string GetParameterDefaultValue(string parameterName) =>
        ParameterDefaults.TryGetValue(parameterName, out var defaultValue) ? defaultValue : "";

    /// <summary>Retrieves the method name responsible for instantiating the specified visual definition, or <see langword="null"/> if none exists.</summary>
    public string? GetVisualDefinitionCreatorName(string definitionName) =>
        _visualDefinitionCreatorNames.TryGetValue(definitionName, out var methodName) ? methodName : null;

    public void FillVisualDefinitionCreators(ClassCode classCode)
    {
        foreach (var visualDefinitionTemplate in _visualDefinitions.Values)
        {
            var name = visualDefinitionTemplate.Name;
            var methodCode = new MethodCode
            {
                Name = _visualDefinitionCreatorNames[name],
                AccessModifier = MethodCodeAccessModifier.Private,
                ReturnParameter = "global::TaleWorlds.GauntletUI.VisualDefinition",
            };
            // Uses invariant culture formatting for float literals to ensure valid C# syntax regardless of system culture.
            var transitionDuration = GeneratedLiteral.Float(visualDefinitionTemplate.TransitionDuration);
            var delayOnBegin = GeneratedLiteral.Float(visualDefinitionTemplate.DelayOnBegin);
            var easeType = $"global::TaleWorlds.GauntletUI.AnimationInterpolation.Type.{visualDefinitionTemplate.EaseType}";
            var easeFunction = $"global::TaleWorlds.GauntletUI.AnimationInterpolation.Function.{visualDefinitionTemplate.EaseFunction}";
            methodCode.AddLine($"var visualDefinition = new global::TaleWorlds.GauntletUI.VisualDefinition({GeneratedLiteral.Regular(name)}, {transitionDuration}, {delayOnBegin}, {easeType}, {easeFunction});");
            foreach (var visualStateTemplate in visualDefinitionTemplate.VisualStates.Values)
            {
                methodCode.AddLine("");
                methodCode.AddBlock(null, () =>
                {
                    methodCode.AddLine($"var visualState = new global::TaleWorlds.GauntletUI.VisualState({GeneratedLiteral.Regular(visualStateTemplate.State)});");
                    foreach (var (attributeName, attributeValue) in visualStateTemplate.GetAttributes())
                    {
                        AddVisualStateAttribute(methodCode, attributeName, attributeValue);
                    }
                    methodCode.AddLine("visualDefinition.AddVisualState(visualState);");
                });
            }
            methodCode.AddLine("");
            methodCode.AddLine("return visualDefinition;");
            classCode.AddMethod(methodCode);
        }
    }

    /// <summary>
    /// Generates code assigning an attribute on a <see cref="VisualState"/> instance matching <c>VisualStateTemplate.CreateVisualState</c> semantics.
    /// </summary>
    /// <remarks>
    /// Values depending on runtime sprite or brush dimensions are deferred to widget creation time; static values are resolved and emitted as literals.
    /// </remarks>
    private void AddVisualStateAttribute(MethodCode methodCode, string attributeName, string attributeValue)
    {
        if (WidgetPropertyPath.Resolve(typeof(VisualState), attributeName) is not { } property)
        {
            methodCode.AddLine($"//VisualState has no '{GeneratedLiteral.Comment(attributeName)}'; the loader assigns nothing");
            return;
        }
        // Validates public property setter presence before emitting assignment code.
        var hasSetter = property.GetSetMethod() is not null;
        var target = $"visualState.{attributeName}";
        // Defers unresolved or resource-dependent attributes to runtime resolution when instantiating the visual definition.
        if ((DependsOnResources(attributeValue) || FailsToResolve(attributeValue))
            && LoaderStringConversion.TryGetRuntimeConversion(property.PropertyType, ResolvedValueVariable, out var converted))
        {
            // Passes through raw values without empty string suppression to match CreateVisualState behavior.
            AddResourceValueAssignment(methodCode, attributeValue, "visualState", attributeName,
                hasSetter ? $"{target} = {converted};" : MissingSetterStatement, skipEmpty: false);
            return;
        }
        var actualValue = ConstantDefinition.GetActualValueOf(attributeValue, _brushFactory, _spriteData, Constants, GivenParameters, ParameterDefaults);
        if (!LoaderStringConversion.TryGetLiteral(property.PropertyType, actualValue, out var literal, out var failure))
        {
            methodCode.AddLine($"//The loader does not convert into the {property.PropertyType.Name} of '{GeneratedLiteral.Comment(attributeName)}', and assigns nothing");
        }
        else if (literal is null || !hasSetter)
        {
            methodCode.AddLine($"//'{GeneratedLiteral.Comment(attributeName)}' of VisualState {(literal is null ? "does not take this value" : "has no public setter")}; the loader asserts and assigns nothing");
            methodCode.AddLine(LoaderStringConversion.AssertLine("visualState", attributeName, actualValue, failure ?? LoaderStringConversion.MissingSetterFailure));
        }
        else
        {
            methodCode.AddLine($"{target} = {literal};");
        }
    }

    /// <summary>
    /// Statement emitted when a property lacks a public setter in runtime-resolved assignments, simulating the XML loader's NullReferenceException catch-and-assert behavior.
    /// </summary>
    public const string MissingSetterStatement = "throw new global::System.NullReferenceException();";

    /// <summary>
    /// Member names for the generated runtime resource constant resolver helper and backing fields.
    /// </summary>
    private const string ResolverMethodName = "UIExtenderEx_ResolveResourceValue";
    private const string EnsureMethodName = "UIExtenderEx_EnsureConstantDefinitions";
    private const string ConstantsFieldName = "_uiExtenderExConstantDefinitions";
    private const string ParametersFieldName = "_uiExtenderExGivenParameters";
    private const string DefaultParametersFieldName = "_uiExtenderExDefaultParameters";

    private const string DefinitionTypeName = "global::TaleWorlds.GauntletUI.PrefabSystem.ConstantDefinition";
    private const string DefinitionTypeEnumName = "global::TaleWorlds.GauntletUI.PrefabSystem.ConstantDefinitionType";
    private const string AttributeTemplateType = "global::TaleWorlds.GauntletUI.PrefabSystem.WidgetAttributeTemplate";
    private const string ConstantsDictionaryType = "global::System.Collections.Generic.Dictionary<string, " + DefinitionTypeName + ">";
    private const string ParametersDictionaryType = "global::System.Collections.Generic.Dictionary<string, " + AttributeTemplateType + ">";
    private const string DefaultParametersDictionaryType = "global::System.Collections.Generic.Dictionary<string, string>";

    /// <summary>
    /// Determines whether the specified XML attribute value references a runtime resource-dependent constant.
    /// </summary>
    public bool DependsOnResources(string value) => ConstantResourceAnalysis.ValueDependsOnResources(value, Constants);

    /// <summary>
    /// Determines whether evaluating <paramref name="value"/> throws an exception during compile-time constant resolution.
    /// </summary>
    private bool FailsToResolve(string value)
    {
        try
        {
            ConstantDefinition.GetActualValueOf(value, _brushFactory, _spriteData, Constants, GivenParameters, ParameterDefaults);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>Determines whether the specified constant definition depends on runtime sprite or brush dimensions.</summary>
    public bool ConstantDependsOnResources(string constantName) =>
        Constants.TryGetValue(constantName, out var definition) && ConstantResourceAnalysis.DependsOnResources(definition, Constants);

    /// <summary>
    /// Identifier of the local variable storing the runtime-resolved constant value.
    /// </summary>
    public const string ResolvedValueVariable = "uiExtenderExResourceValue";

    private const string ResolvedFailureVariable = "uiExtenderExResourceFailure";

    /// <summary>
    /// Generates a runtime resource-dependent property assignment block matching XML loader error-handling semantics.
    /// </summary>
    /// <remarks>
    /// Resolves the value once via the runtime resolver, suppresses assignment on empty values when <paramref name="skipEmpty"/> is <see langword="true"/>,
    /// and wraps assignment conversions in try-catch blocks delegating to Gauntlet loader assertion methods.
    /// </remarks>
    public static void AddResourceValueAssignment(MethodCode methodCode, string value, string targetExpression, string propertyName, string assignment, bool skipEmpty = true)
    {
        methodCode.AddBlock(null, () =>
        {
            methodCode.AddLine($"string {ResolvedValueVariable} = this.{ResolverMethodName}({GeneratedLiteral.String(value)});");
            if (skipEmpty)
            {
                methodCode.AddBlock($"if (!string.IsNullOrEmpty({ResolvedValueVariable}))", AddAssignment);
            }
            else
            {
                AddAssignment();
            }
        });

        void AddAssignment()
        {
            methodCode.AddBlock("try", () => methodCode.AddLine(assignment));
            // Quotes the target, attribute, value, and exception message matching TaleWorlds SetWidgetAttributeFromString catch behavior.
            methodCode.AddBlock($"catch (global::System.Exception {ResolvedFailureVariable})", () =>
                methodCode.AddLine($"{LoaderStringConversion.LoaderAssertsType}.AttributeNotSet({targetExpression}, {GeneratedLiteral.Regular(propertyName)}, " +
                    $"{ResolvedValueVariable}, {ResolvedFailureVariable}.Message);"));
        }
    }

    /// <summary>Generates a runtime resource-dependent assignment for a named constant reference (<c>!ConstantName</c>).</summary>
    public static void AddResourceConstantAssignment(MethodCode methodCode, string constantName, string targetExpression, string propertyName, string assignment) =>
        AddResourceValueAssignment(methodCode, "!" + constantName, targetExpression, propertyName, assignment);

    /// <summary>
    /// Emits runtime constant resolution helper methods and static backing dictionaries into the generated class when resource-dependent constants are present.
    /// </summary>
    /// <remarks>
    /// Caches constant and parameter definitions in static fields while resolving sprite and brush dimensions against the widget's active UIContext at runtime.
    /// </remarks>
    public void FillResourceConstantResolver(ClassCode classCode)
    {
        var needed = false;
        foreach (var definition in Constants.Values)
        {
            if (ConstantResourceAnalysis.DependsOnResources(definition, Constants))
            {
                needed = true;
                break;
            }
        }
        // Defers visual state attributes that cannot be resolved at code generation time.
        needed |= _visualDefinitions.Values.SelectMany(x => x.VisualStates.Values).SelectMany(x => x.GetAttributes()).Any(x => FailsToResolve(x.Value));
        if (!needed)
        {
            return;
        }

        classCode.AddVariable(new() { Name = ConstantsFieldName, Type = ConstantsDictionaryType, IsStatic = true, AccessModifier = VariableCodeAccessModifier.Private });
        classCode.AddVariable(new() { Name = ParametersFieldName, Type = ParametersDictionaryType, IsStatic = true, AccessModifier = VariableCodeAccessModifier.Private });
        classCode.AddVariable(new() { Name = DefaultParametersFieldName, Type = DefaultParametersDictionaryType, IsStatic = true, AccessModifier = VariableCodeAccessModifier.Private });

        var ensure = new MethodCode { Name = EnsureMethodName, AccessModifier = MethodCodeAccessModifier.Private, IsStatic = true };
        ensure.AddBlock($"if ({ConstantsFieldName} != null)", () => ensure.AddLine("return;"));
        WriteConstantDefinitions(ensure);
        WriteGivenParameters(ensure);
        WriteDefaultParameters(ensure);
        classCode.AddMethod(ensure);

        var resolver = new MethodCode { Name = ResolverMethodName, AccessModifier = MethodCodeAccessModifier.Private, ReturnParameter = "string", MethodSignature = "(string value)" };
        resolver.AddLine($"{EnsureMethodName}();");
        // Queries the widget's active UIContext for runtime sprite and brush dimensions matching XML loader resolution.
        resolver.AddLine($"return {DefinitionTypeName}.GetActualValueOf(value, this.Context.BrushFactory, this.Context.SpriteData, {ConstantsFieldName}, {ParametersFieldName}, {DefaultParametersFieldName});");
        classCode.AddMethod(resolver);
    }

    /// <summary>
    /// Emits initialization logic for all constant definitions in the prefab's constant dictionary.
    /// </summary>
    private void WriteConstantDefinitions(MethodCode methodCode)
    {
        methodCode.AddLine($"var constants = new {ConstantsDictionaryType}();");
        foreach (var (name, definition) in Constants)
        {
            methodCode.AddBlock(null, () =>
            {
                methodCode.AddLine($"var constant = new {DefinitionTypeName}({GeneratedLiteral.String(definition.Name)});");
                methodCode.AddLine($"constant.Type = {DefinitionTypeEnumName}.{definition.Type};");
                AddStringAssignment(methodCode, "constant.Value", definition.Value);
                AddStringAssignment(methodCode, "constant.SpriteName", definition.SpriteName);
                AddStringAssignment(methodCode, "constant.BrushName", definition.BrushName);
                AddStringAssignment(methodCode, "constant.LayerName", definition.LayerName);
                AddStringAssignment(methodCode, "constant.Additive", definition.Additive);
                AddStringAssignment(methodCode, "constant.Prefix", definition.Prefix);
                AddStringAssignment(methodCode, "constant.Suffix", definition.Suffix);
                AddStringAssignment(methodCode, "constant.OnTrueValue", definition.OnTrueValue);
                AddStringAssignment(methodCode, "constant.OnFalseValue", definition.OnFalseValue);
                methodCode.AddLine($"constant.MultiplyResult = {GeneratedLiteral.Float(definition.MultiplyResult)};");
                methodCode.AddLine($"constants.Add({GeneratedLiteral.String(name)}, constant);");
            });
        }
        methodCode.AddLine($"{ConstantsFieldName} = constants;");
    }

    /// <summary>
    /// Emits initialization logic for parameters passed from the enclosing parent prefab.
    /// </summary>
    private void WriteGivenParameters(MethodCode methodCode)
    {
        methodCode.AddLine($"var parameters = new {ParametersDictionaryType}();");
        foreach (var (parameterName, parameter) in GivenParameters)
        {
            methodCode.AddBlock(null, () =>
            {
                methodCode.AddLine($"var parameter = new {AttributeTemplateType}();");
                AddStringAssignment(methodCode, "parameter.Key", parameter.Key);
                AddStringAssignment(methodCode, "parameter.Value", parameter.Value);
                methodCode.AddLine($"parameters.Add({GeneratedLiteral.String(parameterName)}, parameter);");
            });
        }
        methodCode.AddLine($"{ParametersFieldName} = parameters;");
    }

    private void WriteDefaultParameters(MethodCode methodCode)
    {
        methodCode.AddLine($"var defaultParameters = new {DefaultParametersDictionaryType}();");
        foreach (var (parameterName, defaultValue) in ParameterDefaults)
        {
            methodCode.AddLine($"defaultParameters.Add({GeneratedLiteral.String(parameterName)}, {GeneratedLiteral.String(defaultValue)});");
        }
        methodCode.AddLine($"{DefaultParametersFieldName} = defaultParameters;");
    }

    /// <summary>Emits a string property assignment statement when the source value is non-null.</summary>
    private static void AddStringAssignment(MethodCode methodCode, string target, string? value)
    {
        if (value is not null)
        {
            methodCode.AddLine($"{target} = {GeneratedLiteral.String(value)};");
        }
    }
}