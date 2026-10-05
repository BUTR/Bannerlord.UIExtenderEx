using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

/// <summary>
/// Represents a single widget node in a template hierarchy for code generation via <see cref="PrefabClass"/>.
/// Manages variable declarations, widget instantiation, identifier assignment, and databinding associations.
/// </summary>
internal sealed partial class WidgetNode
{
    public WidgetNode(PrefabClass owner, WidgetTemplate template, string variableName, WidgetNode? parent)
    {
        Owner = owner;
        Template = template;
        VariableName = variableName;
        Parent = parent;
        Databinding = owner.Databinding is null ? null : new WidgetBinding(this);
    }

    /// <summary>Gets the owning generated prefab class containing this node.</summary>
    public PrefabClass Owner { get; }

    public WidgetTemplate Template { get; }

    public WidgetFactory WidgetFactory => Owner.WidgetFactory;

    public string VariableName { get; }

    public WidgetNode? Parent { get; }

    /// <summary>Gets the databinding definition for this widget node, or <see langword="null"/> if generation omitted databinding.</summary>
    public WidgetBinding? Databinding { get; }

    /// <summary>
    /// Gets the generated class of the prefab instantiated by this widget, or <see langword="null"/> for builtin widgets.
    /// </summary>
    public PrefabClass? UsedClass { get; private set; }

    [MemberNotNullWhen(false, nameof(Parent))]
    public bool IsRoot => Parent == null;

    /// <summary>Gets a value indicating whether this node represents a builtin widget type rather than a custom prefab.</summary>
    public bool IsBuiltin => IsUnknownType || WidgetFactory.IsBuiltinTypeIncludingRegistered(Template.Type);

    /// <summary>Gets a value indicating whether the widget type is unknown to the factory and defaults to <see cref="TaleWorlds.GauntletUI.BaseTypes.Widget"/>.</summary>
    private bool IsUnknownType => WidgetFactory.IsUnknownTypeIncludingRegistered(Template.Type);

    /// <summary>Gets the C# reference expression for this widget ('this' for root, or field variable name).</summary>
    private string Target => IsRoot ? "this" : VariableName;

    public List<WidgetNode> Children { get; } = [];

    /// <summary>
    /// Gets a value indicating whether this widget places child elements into a logical children location in the used prefab.
    /// </summary>
    public bool PassesChildrenIntoALogicalLocation => !IsRoot && UsedClass is { HasLogicalChildrenLocation: true };

    /// <summary>Gets or sets the method name responsible for assigning attributes to passed child widgets.</summary>
    public string? PassedChildrenAttributesMethodName { get; set; }

    public bool IsDescendantOf(WidgetNode ancestor)
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
        {
            if (parent == ancestor)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Collects parameters forwarded to child prefab attributes, propagating values from parent definitions.
    /// </summary>
    private Dictionary<string, WidgetAttributeTemplate> GetPassedParametersToChild()
    {
        var passedParameters = new Dictionary<string, WidgetAttributeTemplate>();
        foreach (var attribute in Template.AllAttributes)
        {
            if (attribute.KeyType is not WidgetAttributeKeyTypeParameter)
            {
                continue;
            }

            var parameter = attribute;
            if (attribute.ValueType is WidgetAttributeValueTypeParameter && Owner.Values.GivenParameters.TryGetValue(attribute.Key, out var givenParameter))
            {
                parameter = givenParameter;
            }
            passedParameters.Add(attribute.Key, parameter);
        }
        return passedParameters;
    }

    /// <summary>
    /// Collects databinding parameters forwarded to child prefabs, filtering to parameters referenced by the child.
    /// </summary>
    private Dictionary<string, WidgetAttributeTemplate> GetBindingParametersToChild()
    {
        var bindingParameters = new Dictionary<string, WidgetAttributeTemplate>();
        foreach (var attribute in Template.GetAttributesOf<WidgetAttributeKeyTypeParameter>())
        {
            bindingParameters[attribute.Key] = attribute;
        }
        // Only what the prefab can read: every other one binds nothing and would only split its class, see ReferencedParameters
        var inherited = Owner.Values.BindingParameters;
        if (inherited.Count > 0)
        {
            var read = ReferencedParameters.Of(WidgetFactory, Template.Type);
            foreach (var (name, parameter) in inherited)
            {
                if (!bindingParameters.ContainsKey(name) && read.Contains(name))
                {
                    bindingParameters.Add(name, parameter);
                }
            }
        }
        return bindingParameters;
    }

    /// <summary>Resolves or creates the generated class for the nested prefab instantiated by this widget.</summary>
    public void ResolveUsedClass()
    {
        // Resolved early for a widget whose children pass into the prefab's logical children location
        if (IsBuiltin || UsedClass is not null)
        {
            return;
        }
        var passedParameters = GetPassedParametersToChild();
        var bindingParameters = GetBindingParametersToChild();
        var options = Databinding?.OptionsForUsedPrefab() ?? new PrefabClassOptions(null);
        var kind = IsRoot ? PrefabClassKind.BasePrefab : PrefabClassKind.UsedPrefab;
        UsedClass = Owner.Classes.Find(kind, Template.Type, options, passedParameters, bindingParameters)
            ?? PrefabClass.CreateForPrefab(Owner, kind, Template.Type, options, passedParameters, bindingParameters);
    }

    private string? GetUsableTypeName() => IsUnknownType
        ? ViewModelMemberResolution.GetCodeTypeName(typeof(TaleWorlds.GauntletUI.BaseTypes.Widget))
        : IsBuiltin
            ? ViewModelMemberResolution.GetCodeTypeName(WidgetFactory.GetBuiltinTypeIncludingRegistered(Template.Type))
            : UsedClass?.ClassName;

    /// <summary>Emits an assertion statement when an unknown widget type falls back to a base widget instance.</summary>
    private void AddUnknownTypeAssert(MethodCode methodCode)
    {
        if (IsUnknownType)
        {
            Owner.Generator.Warn($"'{Template.Type}' in prefab '{Owner.PrefabName ?? Owner.Classes.MovieName}' is neither a widget class nor a prefab; it is built as a plain Widget, as the XML loader builds it.");
            methodCode.AddLine($"//'{GeneratedLiteral.Comment(Template.Type)}' is neither a prefab nor a widget class; the loader builds a plain Widget and asserts");
            methodCode.AddLine($"global::TaleWorlds.Library.Debug.FailedAssert({GeneratedLiteral.Regular("builtin widget type not found in CreateBuiltinWidget(" + Template.Type + ")")});");
        }
    }

    public VariableCode CreateVariableCode() => new()
    {
        Name = VariableName,
        AccessModifier = VariableCodeAccessModifier.Private,
        Type = GetUsableTypeName(),
    };

    public void FillCreateWidgetsMethod(MethodCode methodCode)
    {
        if (IsRoot)
        {
            methodCode.AddLine($"{VariableName} = this;");
            AddUnknownTypeAssert(methodCode);
            // The root widget needs whatever a child gets too. It is the one the enclosing prefab holds, and a command
            // argument can be any widget at all, so leaving it out would mean the refresh methods asking it for a
            // component that was never attached.
            Databinding?.AddDataComponent(methodCode);
            return;
        }

        methodCode.AddLine($"{VariableName} = new {GetUsableTypeName()}(this.Context);");
        AddUnknownTypeAssert(methodCode);
        Databinding?.AddDataComponent(methodCode);
        methodCode.AddLine(Parent.UsedClass is { HasLogicalChildrenLocation: true }
            ? $"{Parent.VariableName}.AddChildToLogicalLocation({VariableName});"
            : $"{Parent.VariableName}.AddChild({VariableName});");
        // A prefab of its own builds its children itself; a builtin widget has none to build
        if (!IsBuiltin)
        {
            methodCode.AddLine($"{VariableName}.CreateWidgets();");
        }
    }

    /// <summary>
    /// Emits the <c>SetIds</c> method assigning widget IDs, preserving TaleWorlds XML loader precedence.
    /// </summary>
    public void FillSetIdsMethod(MethodCode methodCode)
    {
        if (!IsBuiltin && !IsRoot)
        {
            methodCode.AddLine($"{Target}.SetIds();");
        }
        methodCode.AddLine($"{Target}.Id = {GeneratedLiteral.Regular(Template.Id)};");
    }
}