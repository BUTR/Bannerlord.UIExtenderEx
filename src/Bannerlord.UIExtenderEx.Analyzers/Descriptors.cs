using Microsoft.CodeAnalysis;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Diagnostic descriptors for compile-time rules enforced by <c>Bannerlord.UIExtenderEx.Analyzers</c>.
/// Each rule detects subtle runtime incompatibilities or misconfigurations in prefabs, mixins, and patches.
/// Documentation links point to <see href="https://butr.github.io/Bannerlord.UIExtenderEx/articles/general/Analyzers.html"/>.
/// </summary>
internal static class Descriptors
{
    private const string Category = "UIExtenderEx";
    private const string HelpBase = "https://butr.github.io/Bannerlord.UIExtenderEx/articles/general/Analyzers.html#";

    /// <summary>
    /// <c>ViewModelComponent.InitializeMixinsForVMInstance</c> copies mixin properties and methods into the host
    /// instance's binding table by name, shadowing any existing member with that name.
    /// </summary>
    public static readonly DiagnosticDescriptor MixinMemberReplacesHostMember = new(
        id: "UIX0001",
        title: "Mixin member replaces an existing member of the target ViewModel",
        messageFormat: "'{0}' on mixin '{1}' has the name of {2} '{3}.{0}' and replaces it in that ViewModel's binding table",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A mixin member shares the name of a member on the target ViewModel, shadowing it in Gauntlet's binding table. Prefab bindings will route to the mixin member, but internal game code continues calling the original ViewModel member. Rename the member, or use [BUTRViewModelOverride] (UIExtenderEx 3.0+) if you intentionally want to intercept all callers.",
        helpLinkUri: HelpBase + "uix0001");

    /// <summary>Two mixins register members with identical names on the same ViewModel; the last registered wins non-deterministically.</summary>
    public static readonly DiagnosticDescriptor DuplicateMixinMember = new(
        id: "UIX0002",
        title: "Two mixins register conflicting duplicate member names on the same ViewModel",
        messageFormat: "'{0}' is added to '{1}' by both '{2}' and '{3}'; whichever is registered last replaces the other",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Multiple mixins define a member with the same name on the same ViewModel. Because assembly registration order is non-deterministic, the member that wins in the binding table is unpredictable.",
        helpLinkUri: HelpBase + "uix0002",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>ViewModelWithMixinPatch.Patch</c> resolves the refresh hook using reflection (<c>AccessTools2.Method(type, name)</c>),
    /// which requires a parameterless method. In UIExtenderEx 3.0+ an unresolved hook is omitted; older versions throw.
    /// </summary>
    public static readonly DiagnosticDescriptor RefreshMethodNotFound = new(
        id: "UIX0003",
        title: "Refresh method named in [ViewModelMixin] not found on target ViewModel",
        messageFormat: "'{1}' has no method '{0}' UIExtenderEx can hook: {2}; {3}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The refresh method name specified in [ViewModelMixin] could not be found on the target ViewModel. Overloaded methods must have a parameterless overload to be hooked. In UIExtenderEx 3.0+, the mixin is registered without the hook and OnRefresh never executes; in older versions, registration throws an exception.",
        helpLinkUri: HelpBase + "uix0003");

    /// <summary>
    /// Mixins attach only to instances matching the exact ViewModel type specified, unless <c>handleDerived</c> is enabled.
    /// Abstract ViewModels are never instantiated directly.
    /// </summary>
    public static readonly DiagnosticDescriptor MixinHostNeverInstantiated = new(
        id: "UIX0004",
        title: "Mixin extends an abstract ViewModel without handleDerived: true",
        messageFormat: "'{0}' is abstract and handleDerived is not set; no instance is exactly a '{0}', so mixin '{1}' never runs",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Mixins attach only to instances matching the exact ViewModel type specified. Because abstract ViewModels are never instantiated directly, the mixin will never be created unless handleDerived: true is specified to attach to derived subclasses.",
        helpLinkUri: HelpBase + "uix0004");

    /// <summary>
    /// <c>ViewModelComponent.GetViewModelType</c> resolves the target ViewModel from <c>BaseViewModelMixin&lt;TViewModel&gt;</c>
    /// or the first generic argument of an implemented <c>IViewModelMixin</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor MixinHasNoViewModel = new(
        id: "UIX0005",
        title: "Cannot determine target ViewModel from mixin inheritance hierarchy",
        messageFormat: "'{0}' is marked [ViewModelMixin], but {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UIExtenderEx determines the target ViewModel from BaseViewModelMixin<TViewModel>. If implementing IViewModelMixin directly, the target type must be resolvable from the interface's generic argument. Derive from BaseViewModelMixin<TViewModel> to ensure correct resolution.",
        helpLinkUri: HelpBase + "uix0005");

    /// <summary><c>InitializeMixinsForVMInstance</c> instantiates mixins using <c>Activator.CreateInstance(mixinType, instance)</c>.</summary>
    public static readonly DiagnosticDescriptor MixinCannotBeCreated = new(
        id: "UIX0006",
        title: "Mixin cannot be instantiated",
        messageFormat: "Mixin '{0}' cannot be created for '{1}': {2}; the ViewModel's constructor will throw",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "UIExtenderEx instantiates mixins during ViewModel construction via a public constructor accepting the ViewModel instance as its sole argument. If no matching constructor exists or the class is abstract, ViewModel construction throws an unhandled exception.",
        helpLinkUri: HelpBase + "uix0006");

    /// <summary><c>InitializeMixinsForVMInstance</c> only exposes public instance properties and methods to Gauntlet's binding table.</summary>
    public static readonly DiagnosticDescriptor MixinMemberNotPublic = new(
        id: "UIX0007",
        title: "Member annotated with [DataSourceProperty] or [DataSourceMethod] is not public",
        messageFormat: "'{0}' is marked [{1}] but is not public; UIExtenderEx adds only public members, so nothing can bind it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Only public mixin members are registered into the ViewModel's binding table. Non-public members decorated with [DataSourceProperty] or [DataSourceMethod] are ignored by UIExtenderEx and cannot be bound in prefabs.",
        helpLinkUri: HelpBase + "uix0007");

    /// <summary>
    /// <c>ViewModelComponent.RegisterOverrides</c> verifies that an override method returns void, matches all parameters,
    /// and accepts the original method delegate as its final argument.
    /// </summary>
    public static readonly DiagnosticDescriptor OverrideDoesNotMatch = new(
        id: "UIX0008",
        title: "[BUTRViewModelOverride] matches no method on the target ViewModel",
        messageFormat: "Override '{0}' is not registered: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A method decorated with [BUTRViewModelOverride] must match the target ViewModel method's signature, parameter types, and conclude with an Action delegate representing the original implementation. Mismatched overrides cannot be hooked.",
        helpLinkUri: HelpBase + "uix0008");

    /// <summary><c>UnsafeAccessorPatch.Resolve</c> matches accessor signatures against target type members.</summary>
    public static readonly DiagnosticDescriptor AccessorNotResolved = new(
        id: "UIX0009",
        title: "[BUTRUnsafeAccessor] stub member not found on target type",
        messageFormat: "Accessor '{0}' is not resolved: {1}; calling it throws",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [BUTRUnsafeAccessor] stub could not be matched to an existing field or method on the target type. Unresolved stubs retain their throwing placeholder bodies and throw NotImplementedException at runtime.",
        helpLinkUri: HelpBase + "uix0009");

    /// <summary><c>UnsafeAccessorPatch.Register</c> requires <c>MethodImplOptions.NoInlining</c> so stub bodies can be dynamically rewritten.</summary>
    public static readonly DiagnosticDescriptor AccessorInlinable = new(
        id: "UIX0010",
        title: "[BUTRUnsafeAccessor] stub is missing MethodImplOptions.NoInlining",
        messageFormat: "Mark accessor '{0}' [MethodImpl(MethodImplOptions.NoInlining)], so no caller can copy the body it has before UIExtenderEx replaces it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Because UIExtenderEx replaces accessor stub bodies during runtime assembly registration, stubs must be decorated with [MethodImpl(MethodImplOptions.NoInlining)] to prevent the JIT compiler from inlining the initial throwing placeholder.",
        helpLinkUri: HelpBase + "uix0010");

    // ---------------------------------------------------------------- prefab patches

    /// <summary>
    /// <c>PrefabComponent.TryGetNodes</c> binds the patch's content member via reflection. The member must be a public
    /// parameterless instance property or method returning the required content type.
    /// </summary>
    public static readonly DiagnosticDescriptor ContentMemberCannotSupplyContent = new(
        id: "UIX0017",
        title: "Insert patch content member cannot supply patch content",
        messageFormat: "'{0}' cannot supply the patch's content: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A member decorated with a prefab content attribute ([PrefabExtensionFileName], [PrefabExtensionText], [PrefabExtensionXmlNode], or [PrefabExtensionXmlNodes]) must be a public parameterless instance property or method returning the required type. Any other signature fails patch registration.",
        helpLinkUri: HelpBase + "uix0017");

    // ---------------------------------------------------------------- prefab XML: reported once the whole mod is known

    /// <summary>Malformed XML syntax throws <see cref="System.Xml.XmlException"/> during patch registration or screen construction.</summary>
    public static readonly DiagnosticDescriptor XmlNotWellFormed = new(
        id: "UIX0011",
        title: "Prefab XML syntax is malformed or invalid",
        messageFormat: "The XML is not well-formed: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The prefab XML file or patch string literal contains XML syntax errors (such as unclosed tags or mismatched quotes). Malformed XML causes patch registration or screen construction to throw an unhandled XmlException.",
        helpLinkUri: HelpBase + "uix0011",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>WidgetExtensions.GetObjectAndProperty</c> finds no property, causing Gauntlet to silently discard the attribute.</summary>
    public static readonly DiagnosticDescriptor UnknownWidgetAttribute = new(
        id: "UIX0012",
        title: "Attribute does not exist on the target Widget class",
        messageFormat: "'{1}' has no attribute '{0}'; the loader drops it without a message",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The specified attribute does not match any public property on the target Widget class. Gauntlet's XML loader silently discards unrecognized attributes when constructing widgets.",
        helpLinkUri: HelpBase + "uix0012",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>WidgetExtensions.SetWidgetAttributeFromStringAux</c> parses literals via <c>Enum.Parse</c>, <c>Convert.ToInt32</c>/<c>ToSingle</c>, or compares booleans with <c>\"true\"</c>.</summary>
    public static readonly DiagnosticDescriptor InvalidAttributeValue = new(
        id: "UIX0013",
        title: "Attribute literal value cannot be converted to target property type",
        messageFormat: "'{0}' is not a value '{1}' can take: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Gauntlet's XML loader converts attribute text into property types using basic parsing (Enum.Parse, Convert.ToInt32, Convert.ToSingle, or string comparison with 'true' for booleans). Invalid enum names or unparseable numbers cause attribute assignment to fail; boolean attributes with any value other than 'true' evaluate to false.",
        helpLinkUri: HelpBase + "uix0013",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>A parameter passed to a prefab tag that is neither declared under <c>&lt;Parameters&gt;</c> nor referenced as <c>*Name</c> has no effect.</summary>
    public static readonly DiagnosticDescriptor UnknownPrefabParameter = new(
        id: "UIX0014",
        title: "Prefab parameter is neither declared nor referenced",
        messageFormat: "Prefab '{1}' neither declares nor reads a parameter '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Parameter attributes on a prefab tag (Parameter.Name=\"...\") supply values to attributes inside the prefab referenced via *Name. Specifying a parameter that the prefab neither declares under <Parameters> nor references internally has no effect.",
        helpLinkUri: HelpBase + "uix0014",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>ViewModel.GetPropertyValue</c>, <c>GetViewModelAtPath</c>, or <c>ExecuteCommand</c> finds no matching member on the active ViewModel context.</summary>
    public static readonly DiagnosticDescriptor BindingNotFound = new(
        id: "UIX0015",
        title: "Binding or command name not found on active ViewModel or mixin",
        messageFormat: "'{1}' has no {2} '{0}', and no mixin of this mod adds one",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The property or method referenced by a data binding (@Name) or command (Command.Click=\"...\") does not exist on the active ViewModel type or any registered mixin. In Gauntlet's XML loader, missing members result in unassigned widgets or unhandled reflection errors.",
        helpLinkUri: HelpBase + "uix0015",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>In the absence of an explicit <c>[assembly: PrefabLink]</c>, the patch's target ViewModel cannot be inferred from the mod's mixins.</summary>
    public static readonly DiagnosticDescriptor BindingOnNoMixinViewModel = new(
        id: "UIX0016",
        title: "Patch binds a member not provided by any of the mod's mixins",
        messageFormat: "'{0}' is on none of the ViewModels this mod's mixins extend ({1}); if the patch binds a game ViewModel, link it with [assembly: PrefabLink]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a patch does not specify an explicit [assembly: PrefabLink], the analyzer infers the target ViewModel from the mixins providing the bound members. If none of the mod's mixins declare the member, declare a [PrefabLink] to indicate the target ViewModel.",
        helpLinkUri: HelpBase + "uix0016",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>GauntletView.OnViewPropertyChanged</c> and <c>GauntletView.RefreshBinding</c> invoke property accessors via reflection
    /// without coercion, throwing <see cref="System.ArgumentException"/> for incompatible types.
    /// </summary>
    public static readonly DiagnosticDescriptor BindingThrowsBetweenWidgetAndViewModel = new(
        id: "UIX0025",
        title: "Incompatible binding types between widget and ViewModel will throw",
        messageFormat: "'{0}' cannot be bound to '{1}.{2}': {3}; {4}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Gauntlet's XML loader performs data transfer via reflection without automatic type coercion (except for string values converted to registered conversion types: Sprite, Brush, int, Color). Setting incompatible types or receiving announced widget changes of an incompatible type causes MethodInfo.Invoke to throw an ArgumentException at runtime.",
        helpLinkUri: HelpBase + "uix0025",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>GauntletView.OnPropertyChanged</c> refreshes only direct child views bound in a single step (<c>DataSource="{Child}"</c>).
    /// Multi-step paths retain stale references when the child ViewModel is replaced.
    /// </summary>
    public static readonly DiagnosticDescriptor DataSourceNotRefreshedWhenReplaced = new(
        id: "UIX0026",
        title: "Multi-step DataSource path will not refresh when replaced in XML loader",
        messageFormat: "DataSource '{0}' reaches '{1}' of '{2}' through more than one step: when '{2}' replaces '{1}', the XML loader leaves this widget, and everything under it, on the old one; take it as DataSource=\"{{{1}}}\" on a widget whose own DataSource is '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When a parent ViewModel replaces a child ViewModel instance, Gauntlet's XML loader only refreshes widgets bound directly in a single step (DataSource=\"{Child}\"). Widgets bound via multi-step paths ({Parent\\Child} or {..\\Sibling}) retain stale references to the old instance.",
        helpLinkUri: HelpBase + "uix0026",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>WidgetTemplate.SetAttributes</c> evaluates parameter attributes on passed children using the receiving prefab's context
    /// rather than the caller's context.
    /// </summary>
    public static readonly DiagnosticDescriptor ParameterInPassedChildren = new(
        id: "UIX0027",
        title: "Parameter inside LogicalChildrenLocation is not forwarded properly",
        messageFormat: "'*{0}' is inside the children passed to '{1}', which puts them in its logical children location: the XML loader resolves it with the parameters '{1}' is given, not with this prefab's; pass it on with Parameter.{0}=\"*{0}\" on '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When children are passed into a prefab containing a <LogicalChildrenLocation />, Gauntlet's XML loader evaluates parameter attributes (*ParamName) using the receiving prefab's parameter context rather than the declaring prefab's context. Forward the parameter explicitly on the container widget (Parameter.Name=\"*Name\").",
        helpLinkUri: HelpBase + "uix0027",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>GauntletView.RefreshBinding</c> evaluates indexed paths only on full view refresh. In-place collection mutations
    /// (<c>ListChanged</c>) are not tracked by indexed child bindings.
    /// </summary>
    public static readonly DiagnosticDescriptor DataSourceIntoAListByIndex = new(
        id: "UIX0028",
        title: "Indexed DataSource path ({List\\0}) is not tracked dynamically in XML loader",
        messageFormat: "DataSource '{0}' steps into list '{1}' by index: when an item is added, removed or replaced, the XML loader leaves this widget, and everything under it, on the item that was at that index; give the item a property of its own on the ViewModel, or replace the list rather than changing it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Gauntlet's XML loader evaluates indexed paths ({List\\0}) only on initial view construction or full view refresh. Mutating the list in place (Add, Remove, Insert) does not update widgets bound by index, leaving them pointing to stale or removed elements.",
        helpLinkUri: HelpBase + "uix0028",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// The referenced GUI bundle does not contain definitions for this targeted game version, preventing compile-time validation against it.
    /// </summary>
    public static readonly DiagnosticDescriptor VersionNotInGuiPackages = new(
        id: "UIX0030",
        title: "Targeted game version is missing from GUI reference packages",
        messageFormat: "The game's GUI data has no {0}, so the patches are not checked against {1}; the newest the bundle holds is {2}. Reference Bannerlord.ReferenceAssemblies.GUI.v3.All with Version=\"*\" for a newer bundle, or the build's Bannerlord.ReferenceAssemblies.GUI.v3 package.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The referenced GUI reference bundle does not contain definitions for this targeted game version (e.g. a newly released game update, beta branch, or Early Access release). Prefab patches cannot be validated against this version at compile time.",
        helpLinkUri: HelpBase + "uix0030",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>WidgetFactory.CreateBuiltinWidget</c> instantiates a plain base <c>Widget</c> and logs an engine assertion when encountering an unrecognized tag name.</summary>
    public static readonly DiagnosticDescriptor UnknownWidgetTag = new(
        id: "UIX0029",
        title: "XML tag matches neither a known Widget class nor a registered prefab",
        messageFormat: "'{0}' is neither a widget class nor a prefab the build can see: the loader builds a plain Widget for it and asserts, so nothing it was meant to do happens; if another mod provides it, this can be ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The tag name does not match any known Widget class or registered prefab. Gauntlet instantiates a plain base Widget when encountering unknown tags, logging an engine assertion and failing to load custom widget logic.",
        helpLinkUri: HelpBase + "uix0029",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    // ---------------------------------------------------------------- the game's prefabs, from the GUI packages

    /// <summary><c>PrefabComponent.RegisterPatch</c> finds no matching node via <c>SelectSingleNode(xpath)</c>, causing the patch to be skipped at runtime.</summary>
    public static readonly DiagnosticDescriptor XPathMatchesNothing = new(
        id: "UIX0020",
        title: "Patch XPath matches zero nodes in target prefab",
        messageFormat: "'{0}' matches no node of '{1}'{2}; the patch is not applied",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UIExtenderEx applies patches at the first node matched by the patch's XPath query. When the XPath matches nothing, the patch is skipped and a warning is logged at runtime.",
        helpLinkUri: HelpBase + "uix0020",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>SelectSingleNode</c> selects only the first matching node in document order; subsequent nodes are ignored.</summary>
    public static readonly DiagnosticDescriptor XPathMatchesSeveral = new(
        id: "UIX0021",
        title: "Patch XPath matches multiple nodes in target prefab",
        messageFormat: "'{0}' matches {1} nodes of '{2}'{3}; UIExtenderEx patches only the first",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The XPath query selects multiple nodes in the target prefab. UIExtenderEx only modifies the first matched node in document order. If vanilla prefabs change in future updates, the patch may unintentionally attach to a different node.",
        helpLinkUri: HelpBase + "uix0021",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>The ViewModel declared by <c>[assembly: PrefabLink]</c> disagrees with the ViewModel bound by the vanilla prefab at the target XPath location.</summary>
    public static readonly DiagnosticDescriptor PrefabLinkDisagreesWithGame = new(
        id: "UIX0022",
        title: "[PrefabLink] specifies a different ViewModel than the game binds at the node",
        messageFormat: "'{0}' is linked to '{1}', but the game binds {2} where the patch goes in",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The ViewModel declared by [PrefabLink] does not match the ViewModel type bound by the vanilla prefab at the target XPath node. Update the link to match the actual runtime ViewModel.",
        helpLinkUri: HelpBase + "uix0022",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>XmlNode.SelectSingleNode</c> throws for malformed, empty, or non-node-selecting XPath expressions.
    /// </summary>
    public static readonly DiagnosticDescriptor XPathInvalid = new(
        id: "UIX0023",
        title: "Patch XPath expression is syntactically invalid",
        messageFormat: "The XPath{0} cannot be applied: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The XPath expression syntax is invalid, empty, or evaluates to an unsupported XPath return type (such as a numeric scalar). Invalid expressions throw an exception when the patch is registered.",
        helpLinkUri: HelpBase + "uix0023",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// A diagnostic (UIX0012, UIX0013, UIX0015, UIX0020, UIX0021, UIX0022, or UIX0025) triggers only in specific targeted game versions.
    /// </summary>
    public static readonly DiagnosticDescriptor HoldsForSomeVersions = new(
        id: "UIX0024",
        title: "Patch fails in a subset of supported game versions",
        messageFormat: "In {0} only: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When validating against Bannerlord.ReferenceAssemblies.GUI.v3.All across multiple supported game versions, this patch succeeds in some versions but fails in others due to vanilla changes. Use version symbols (#if) to condition the patch per game version, or adjust the XPath/binding to be cross-version compatible.",
        helpLinkUri: HelpBase + "uix0024",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    // ---------------------------------------------------------------- [assembly: PrefabLink]

    /// <summary>A <c>[assembly: PrefabLink]</c> specifies invalid patch, prefab, or ViewModel types and is ignored during analysis.</summary>
    public static readonly DiagnosticDescriptor PrefabLinkDoesNotHold = new(
        id: "UIX0018",
        title: "[PrefabLink] configuration is invalid or inconsistent",
        messageFormat: "The link is left out: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [PrefabLink] attribute must reference a valid patch or prefab, the ViewModel active at the insertion point, and optionally a mixin extending that ViewModel (or a base ViewModel with handleDerived: true). Invalid links are ignored during analysis.",
        helpLinkUri: HelpBase + "uix0018",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>The XML targets a mixin declared in <c>[assembly: PrefabLink]</c>, but contains no bindings or commands referencing members from that mixin.</summary>
    public static readonly DiagnosticDescriptor LinkedMixinNotBound = new(
        id: "UIX0019",
        title: "Linked XML does not bind any members of the linked mixin",
        messageFormat: "'{0}' is linked to mixin '{1}', but binds none of its members {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [PrefabLink] links a patch or prefab to a mixin, but the XML contains no bindings or commands referencing members declared by that mixin. The mixin parameter may be unneeded or reference the wrong mixin.",
        helpLinkUri: HelpBase + "uix0019",
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}