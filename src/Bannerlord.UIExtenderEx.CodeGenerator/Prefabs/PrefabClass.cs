using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using System;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

/// <summary>
/// Represents a generated C# class corresponding to a root movie, an inherited/dependent prefab, or an item template.
/// </summary>
/// <remarks>
/// Builds the widget hierarchy (<see cref="WidgetNode"/>), initializes visual definitions and attributes, and emits databinding infrastructure (<see cref="DatabindingEmitter"/>).
/// </remarks>
internal sealed class PrefabClass
{
    /// <summary>
    /// Member name of the delegate invoked to configure attributes on children injected into a logical children location.
    /// </summary>
    /// <remarks>
    /// Matches Gauntlet loader traversal order: sets attributes on injected child widgets at the logical location following the location's own children.
    /// </remarks>
    public const string LogicalChildrenAttributesName = "LogicalChildrenAttributes";

    private WidgetTemplate? _rootTemplate;

    private WidgetNode? _rootWidget;

    private PrefabClass(PrefabCodeGenerator generator, MovieClasses classes, PrefabClassKind kind, string className, PrefabClassOptions options, PrefabValues values)
    {
        Generator = generator;
        Classes = classes;
        Kind = kind;
        ClassName = className;
        Options = options;
        Values = values;
        Databinding = options.DataSourceType is null ? null : new DatabindingEmitter(this);
    }

    public PrefabCodeGenerator Generator { get; }

    public WidgetFactory WidgetFactory => Generator.WidgetFactory;

    /// <summary>Gets the collection of sibling and dependency classes belonging to the active movie.</summary>
    public MovieClasses Classes { get; }

    public PrefabClassKind Kind { get; }

    public string ClassName { get; }

    public PrefabClassOptions Options { get; }

    /// <summary>Gets the constants, parameters, and scoped resource definitions associated with the prefab.</summary>
    public PrefabValues Values { get; }

    /// <summary>Gets the databinding emitter, or <see langword="null"/> if the class is generated without a ViewModel data source.</summary>
    public DatabindingEmitter? Databinding { get; }

    /// <summary>Gets the prefab identifier for movie and dependent prefab classes.</summary>
    public string? PrefabName { get; private set; }

    public string? VariantName { get; private set; }

    /// <summary>Gets all widget nodes in the template ordered depth-first (parents before children).</summary>
    public List<WidgetNode> Widgets { get; } = [];

    public bool HasLogicalChildrenLocation { get; private set; }

    /// <summary>Gets the root widget template of the prefab hierarchy.</summary>
    private WidgetTemplate RootTemplate => _rootTemplate
        ?? throw new InvalidOperationException($"The generation context for '{ClassName}' was used before it was prepared.");

    /// <summary>Gets a value indicating whether the template is rooted in another custom prefab rather than a builtin widget class.</summary>
    public bool InheritsAnotherPrefab => WidgetFactory.IsCustomTypeIncludingRegistered(RootTemplate.Type);

    public static PrefabClass CreateMovie(PrefabCodeGenerator generator, string prefabName, string variantName, PrefabClassOptions options)
    {
        var className = GetUsableClassName(prefabName, variantName);
        var prefabClass = new PrefabClass(generator, new MovieClasses(prefabName, className), PrefabClassKind.Movie, className, options, new PrefabValues(generator.BrushFactory, generator.SpriteData));
        prefabClass.PrepareAsPrefab(prefabName, variantName, new(), new());
        return prefabClass;
    }

    /// <summary>Creates a <see cref="PrefabClass"/> representing a base or dependent prefab instantiated by <paramref name="user"/>.</summary>
    public static PrefabClass CreateForPrefab(PrefabClass user, PrefabClassKind kind, string prefabName, PrefabClassOptions options,
        Dictionary<string, WidgetAttributeTemplate> givenParameters, Dictionary<string, WidgetAttributeTemplate> bindingParameters)
    {
        // The variant names are the game generator's, and are part of every class name
        var variantName = kind == PrefabClassKind.BasePrefab ? "InheritedPrefab" : "DependendPrefab";
        var className = $"{user.Classes.NextName()}_{GetUsableClassName(prefabName, variantName)}";
        var prefabClass = new PrefabClass(user.Generator, user.Classes, kind, className, options, new PrefabValues(user.Generator.BrushFactory, user.Generator.SpriteData));
        prefabClass.PrepareAsPrefab(prefabName, variantName, givenParameters, bindingParameters);
        user.Classes.Add(prefabClass);
        return prefabClass;
    }

    /// <summary>Creates a <see cref="PrefabClass"/> representing an item template within a list widget in <paramref name="owner"/>.</summary>
    public static PrefabClass CreateForItemTemplate(PrefabClass owner, WidgetTemplate itemTemplate, string identifierName, PrefabClassOptions options)
    {
        var className = $"{owner.Classes.NextName()}_{identifierName}";
        var prefabClass = new PrefabClass(owner.Generator, owner.Classes, PrefabClassKind.ItemTemplate, className, options, owner.Values)
        {
            _rootTemplate = itemTemplate,
        };
        owner.Classes.Add(prefabClass);
        return prefabClass;
    }

    private void PrepareAsPrefab(string prefabName, string variantName, Dictionary<string, WidgetAttributeTemplate> givenParameters, Dictionary<string, WidgetAttributeTemplate> bindingParameters)
    {
        PrefabName = prefabName;
        VariantName = variantName;
        Values.SetGivenParameters(givenParameters, bindingParameters);
        var prefab = WidgetFactory.GetCustomTypeIncludingRegistered(prefabName);
        _rootTemplate = prefab.RootTemplate;
        HasLogicalChildrenLocation = FindLogicalChildrenLocation() != null;
        Values.FillFromPrefab(prefab);
    }

    private static string GetUsableClassName(string name, string variantName) =>
        $"{GeneratedNaming.GetUsableName(name)}__{GeneratedNaming.GetUsableName(variantName)}";

    /// <summary>
    /// Resolves the <see cref="WidgetTemplate"/> designated as the logical children location within this prefab hierarchy.
    /// </summary>
    private WidgetTemplate? FindLogicalChildrenLocation() => LoaderChildrenLocation.LocationIn(WidgetFactory, RootTemplate);

    /// <summary>Gets the <see cref="WidgetNode"/> designated as the logical children location, if defined.</summary>
    public WidgetNode? LogicalChildrenLocationNode => LogicalChildrenLocation;

    private WidgetNode? LogicalChildrenLocation => HasLogicalChildrenLocation && FindLogicalChildrenLocation() is { } template
        ? Widgets.Find(x => x.Template == template)
        : null;

    private bool _prepared;

    /// <summary>
    /// Builds widget node hierarchies and resolves dependent prefab classes prior to code emission.
    /// </summary>
    /// <remarks>
    /// Prepares all movie classes before code generation to ensure cross-prefab databinding paths settle correctly.
    /// </remarks>
    public void Prepare()
    {
        if (_prepared)
        {
            return;
        }
        _prepared = true;
        CreateWidgetNodes(RootTemplate, "_widget", null);
        foreach (var widget in Widgets)
        {
            widget.ResolveUsedClass();
        }
    }

    /// <summary>Prepares the movie and its dependency classes, settling outer databinding paths across prefab boundaries.</summary>
    private void PrepareMovie()
    {
        Prepare();
        Classes.Prepare();
        var classes = new[] { this }.Concat(Classes.All).Where(x => x.Databinding is not null).ToList();
        if (Databinding is null)
        {
            return;
        }
        // Iteratively propagates outer databinding paths until all dependencies stabilize.
        var mostRounds = DatabindingEmitter.SettleRootDepths(Databinding, classes.Select(x => x.Databinding!).ToList());
        var rounds = 0;
        while (classes.Aggregate(false, (changed, x) => x.Databinding!.SettleOuterPaths() | changed))
        {
            if (++rounds > mostRounds)
            {
                throw new InvalidOperationException($"The data sources the classes of '{PrefabName}' bind above their roots did not settle.");
            }
        }
    }

    public void GenerateInto(NamespaceCode namespaceCode)
    {
        var classCode = new ClassCode
        {
            Name = ClassName,
            AccessModifier = ClassCodeAccessModifier.Public,
        };
        if (Kind == PrefabClassKind.Movie)
        {
            PrepareMovie();
        }
        if (HasLogicalChildrenLocation)
        {
            GenerateAddChildToLogicalLocationMethod(classCode);
            classCode.AddVariable(new VariableCode
            {
                Name = LogicalChildrenAttributesName,
                AccessModifier = VariableCodeAccessModifier.Public,
                Type = "global::System.Action",
            });
        }
        // Emits resource constant resolvers prior to visual definitions that reference them.
        Values.FillResourceConstantResolver(classCode);
        Values.FillVisualDefinitionCreators(classCode);
        foreach (var widget in Widgets)
        {
            classCode.AddVariable(widget.CreateVariableCode());
        }
        GenerateCreateWidgetsMethod(classCode);
        GenerateSetIdsMethod(classCode);
        GenerateSetAttributesMethod(classCode);
        classCode.InheritedInterfaces.Add(GetBaseTypeName());
        Databinding?.GenerateInto(classCode);
        classCode.AddConsturctor(new ConstructorCode
        {
            MethodSignature = "(global::TaleWorlds.GauntletUI.UIContext context)",
            BaseCall = "(context)",
        });
        classCode.CommentSection = CreateCommentSection();
        namespaceCode.AddClass(classCode);
        if (Kind == PrefabClassKind.Movie)
        {
            Classes.GenerateInto(namespaceCode);
        }
    }

    private void CreateWidgetNodes(WidgetTemplate template, string variableName, WidgetNode? parent)
    {
        var widget = new WidgetNode(this, template, variableName, parent);
        Widgets.Add(widget);
        if (parent != null)
        {
            parent.Children.Add(widget);
        }
        else
        {
            _rootWidget = widget;
        }
        widget.Databinding?.Initialize();
        // Prepares dependent prefabs with logical children locations to establish databinding scopes for injected children.
        if (widget.Databinding is { } binding && !widget.IsRoot && !widget.IsBuiltin && template.ChildCount > 0)
        {
            widget.ResolveUsedClass();
            if (widget.UsedClass is { HasLogicalChildrenLocation: true, Databinding: not null } used)
            {
                used.Prepare();
                binding.PassChildrenThrough(used);
            }
        }
        for (var i = 0; i < template.ChildCount; i++)
        {
            CreateWidgetNodes(template.GetChildAt(i), $"{variableName}_{i}", widget);
        }
    }

    /// <summary>Resolves the C# base type name, inheriting from a generated prefab class for custom root types or the builtin widget class.</summary>
    private string GetBaseTypeName()
    {
        var rootTemplateType = RootTemplate.Type;
        var baseTypeName = !InheritsAnotherPrefab
            // Uses standard widget factory resolution to produce clear diagnostic errors on missing root widget types.
            ? WidgetFactory.TryGetWidgetTypeWithinPrefabRoots(rootTemplateType, out var rootType) ? ViewModelMemberResolution.GetCodeTypeName(rootType) : null
            : _rootWidget?.UsedClass?.ClassName;
        // Validates base type resolution before generating class source text.
        if (string.IsNullOrEmpty(baseTypeName))
        {
            throw new InvalidOperationException($"Could not resolve the type '{rootTemplateType}' that prefab '{PrefabName}' is rooted in.");
        }
        return baseTypeName!;
    }

    private CommentSection CreateCommentSection()
    {
        var commentSection = new CommentSection();
        foreach (var (name, value) in Options.Describe())
        {
            commentSection.AddCommentLine(GeneratedLiteral.Comment($"Data: {name} - {value}"));
        }
        foreach (var (parameterName, parameter) in Values.GivenParameters)
        {
            commentSection.AddCommentLine(GeneratedLiteral.Comment($"Given Parameter: {parameterName} - {parameter.Value} {parameter.KeyType} {parameter.ValueType}"));
        }
        return commentSection;
    }

    /// <summary>
    /// Configures method polymorphism, setting override flags when deriving from an inherited prefab or virtual when acting as a base prefab.
    /// </summary>
    public void MakeOverridable(MethodCode methodCode, string baseCall)
    {
        if (InheritsAnotherPrefab)
        {
            methodCode.PolymorphismInfo = MethodCodePolymorphismInfo.Override;
            methodCode.AddLine(baseCall);
        }
        else if (Kind == PrefabClassKind.BasePrefab)
        {
            methodCode.PolymorphismInfo = MethodCodePolymorphismInfo.Virtual;
        }
    }

    private void GenerateAddChildToLogicalLocationMethod(ClassCode classCode)
    {
        // Validates presence of the logical children location before emitting delegation method.
        if (LogicalChildrenLocation is not { } location)
        {
            throw new InvalidOperationException($"Prefab '{PrefabName}' declares a logical children location that is not in its template.");
        }

        var methodCode = new MethodCode
        {
            Name = "AddChildToLogicalLocation",
            AccessModifier = MethodCodeAccessModifier.Public,
            MethodSignature = "(global::TaleWorlds.GauntletUI.BaseTypes.Widget widget)",
        };
        methodCode.AddLine($"{location.VariableName}.AddChild(widget);");
        classCode.AddMethod(methodCode);
    }

    public MethodCode GenerateCreatorMethod()
    {
        var methodCode = new MethodCode
        {
            Name = $"Create{ClassName}",
            ReturnParameter = "global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult",
            MethodSignature = "(global::TaleWorlds.GauntletUI.UIContext context, global::System.Collections.Generic.Dictionary<string, object> data)",
        };
        methodCode.AddLine($"var widget = new {ClassName}(context);");
        methodCode.AddLine("widget.CreateWidgets();");
        methodCode.AddLine("widget.SetIds();");
        methodCode.AddLine("widget.SetAttributes();");
        methodCode.AddLine("var result = new global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult(widget);");
        Databinding?.AddExtrasToCreatorMethod(methodCode);
        methodCode.AddLine("return result;");
        return methodCode;
    }

    private void GenerateCreateWidgetsMethod(ClassCode classCode)
    {
        var methodCode = new MethodCode { Name = "CreateWidgets" };
        classCode.AddMethod(methodCode);
        MakeOverridable(methodCode, "base.CreateWidgets();");
        foreach (var widget in Widgets)
        {
            widget.FillCreateWidgetsMethod(methodCode);
        }
    }

    private void GenerateSetIdsMethod(ClassCode classCode)
    {
        var methodCode = new MethodCode { Name = "SetIds" };
        classCode.AddMethod(methodCode);
        MakeOverridable(methodCode, "base.SetIds();");
        foreach (var widget in Widgets)
        {
            widget.FillSetIdsMethod(methodCode);
        }
    }

    private void GenerateSetAttributesMethod(ClassCode classCode)
    {
        var methodCode = new MethodCode { Name = "SetAttributes" };
        classCode.AddMethod(methodCode);

        // Emits dedicated attribute assignment methods for children injected into dependent prefabs at logical children locations.
        var passedChildrenMethods = new Dictionary<WidgetNode, MethodCode>();
        foreach (var widget in Widgets)
        {
            if (widget.PassesChildrenIntoALogicalLocation && widget.Children.Count > 0)
            {
                var passedChildrenMethod = new MethodCode
                {
                    Name = $"SetAttributesOfChildrenPassedBy{widget.VariableName}",
                    AccessModifier = MethodCodeAccessModifier.Private,
                };
                classCode.AddMethod(passedChildrenMethod);
                passedChildrenMethods[widget] = passedChildrenMethod;
                widget.PassedChildrenAttributesMethodName = passedChildrenMethod.Name;
            }
        }
        MethodCode MethodOf(WidgetNode widget)
        {
            for (var parent = widget.Parent; parent is not null; parent = parent.Parent)
            {
                if (passedChildrenMethods.TryGetValue(parent, out var passedChildrenMethod))
                    return passedChildrenMethod;
            }
            return methodCode;
        }

        // Locates the logical children location in the widget list to sequence injected child attribute initialization.
        var location = LogicalChildrenLocation;
        var locationEnd = location is null ? -1 : Widgets.FindLastIndex(x => x == location || x.IsDescendantOf(location));
        MakeOverridable(methodCode, "base.SetAttributes();");
        for (var i = 0; i < Widgets.Count; i++)
        {
            var widget = Widgets[i];
            // Assigns widget attributes in template declaration order (parents before children).
            if (!widget.IsBelowAbandonedAttributes)
            {
                widget.FillSetAttributesMethod(MethodOf(widget));
            }
            // Emits logical children attribute callback invocation unless attribute processing was abandoned.
            if (i == locationEnd && !location!.AttributesAbandoned && !location.IsBelowAbandonedAttributes)
            {
                MethodOf(location).AddLine("// Invokes the logical children attributes delegate when reached during traversal");
                MethodOf(location).AddLine($"{LogicalChildrenAttributesName}?.Invoke();");
            }
        }
    }
}