using Microsoft.CodeAnalysis;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// The rules. Each one is something the runtime does silently, or fails at, that the build can see coming; the runtime
/// behaviour each mirrors is named on the rule. The help links point at docs/articles/general/Analyzers.md.
/// </summary>
internal static class Descriptors
{
    private const string Category = "UIExtenderEx";
    private const string HelpBase = "https://butr.github.io/Bannerlord.UIExtenderEx/articles/general/Analyzers.html#";

    /// <summary>
    /// <c>ViewModelComponent.InitializeMixinsForVMInstance</c> writes each mixin member into the host instance's binding
    /// table by name, over whatever the table held.
    /// </summary>
    public static readonly DiagnosticDescriptor MixinMemberReplacesHostMember = new(
        id: "UIX0001",
        title: "Mixin member replaces a member of the ViewModel it extends",
        messageFormat: "'{0}' on mixin '{1}' has the name of {2} '{3}.{0}' and replaces it in that ViewModel's binding table",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A mixin's members are added to the ViewModel under their names, over the ViewModel's own. Every binding and command in a prefab then reaches the mixin's member, while the game's own code goes on calling the ViewModel's: a replaced command runs from the button but not from a hotkey or anything else that calls the method. Rename the member; to take over a method on purpose, use [BUTRViewModelOverride] (UIExtenderEx 3.0 and later), which every caller reaches.",
        helpLinkUri: HelpBase + "uix0001");

    /// <summary>The same table write, done by two mixins: the one registered last wins.</summary>
    public static readonly DiagnosticDescriptor DuplicateMixinMember = new(
        id: "UIX0002",
        title: "Two mixins add the same name to one ViewModel",
        messageFormat: "'{0}' is added to '{1}' by both '{2}' and '{3}'; whichever is registered last replaces the other",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Both mixins write the name into the ViewModel's binding table, and bindings reach only the one written last. Registration order is the order the types come out of the assembly, which nothing guarantees.",
        helpLinkUri: HelpBase + "uix0002",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>ViewModelWithMixinPatch.Patch</c> finds the refresh method with <c>AccessTools2.Method(type, name)</c>, which
    /// answers null for a name the type does not have, and for an overloaded one with no parameterless overload. From 3.0
    /// the mixin is registered without the hook; before, the null is dereferenced and <c>UIExtender.Register</c> throws.
    /// </summary>
    public static readonly DiagnosticDescriptor RefreshMethodNotFound = new(
        id: "UIX0003",
        title: "Refresh method cannot be found on the ViewModel",
        messageFormat: "'{1}' has no method '{0}' UIExtenderEx can hook: {2}; {3}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The refresh method name on [ViewModelMixin] is looked up on the ViewModel by name. An overloaded name resolves only to its parameterless overload. When nothing resolves, UIExtenderEx 3.0 and later register the mixin without the hook, and OnRefresh never runs; older versions throw from UIExtender.Register, and the mod's types after this one are not registered.",
        helpLinkUri: HelpBase + "uix0003");

    /// <summary>
    /// Mixins are created for an instance whose type is exactly the registered one, unless <c>handleDerived</c> registers
    /// every derived type as well. From 3.0 <c>ViewModelComponent.RegisterViewModelMixin</c> refuses an abstract one without
    /// it; before, it registers the mixin for a type no instance has.
    /// </summary>
    public static readonly DiagnosticDescriptor MixinHostNeverInstantiated = new(
        id: "UIX0004",
        title: "Mixin extends a ViewModel no instance can be",
        messageFormat: "'{0}' is abstract and handleDerived is not set; no instance is exactly a '{0}', so mixin '{1}' never runs",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A mixin is attached to instances whose type is exactly the ViewModel it extends. An abstract ViewModel has none, so the mixin never runs: UIExtenderEx 3.0 and later refuse to register it and say so in the log, older versions register it without a word. Extend the concrete ViewModel, or pass handleDerived: true to reach every type derived from it.",
        helpLinkUri: HelpBase + "uix0004");

    /// <summary>
    /// <c>ViewModelComponent.GetViewModelType</c>: the argument of the closed <c>BaseViewModelMixin&lt;&gt;</c>, or for any
    /// other <c>IViewModelMixin</c> the first generic argument of the first type from the mixin up that implements it.
    /// </summary>
    public static readonly DiagnosticDescriptor MixinHasNoViewModel = new(
        id: "UIX0005",
        title: "UIExtenderEx cannot tell which ViewModel the mixin extends",
        messageFormat: "'{0}' is marked [ViewModelMixin], but {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UIExtenderEx takes the ViewModel from the BaseViewModelMixin<TViewModel> the mixin derives from. A mixin that implements IViewModelMixin some other way has it taken from the first type argument of the first type, from the mixin up, that implements the interface. Derive from BaseViewModelMixin<TViewModel>.",
        helpLinkUri: HelpBase + "uix0005");

    /// <summary><c>InitializeMixinsForVMInstance</c> creates each mixin with <c>Activator.CreateInstance(mixinType, instance)</c>.</summary>
    public static readonly DiagnosticDescriptor MixinCannotBeCreated = new(
        id: "UIX0006",
        title: "Mixin cannot be created",
        messageFormat: "Mixin '{0}' cannot be created for '{1}': {2}; the ViewModel's constructor will throw",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "UIExtenderEx creates a mixin at the end of the ViewModel's constructor, through a public constructor taking the ViewModel as its only argument. If it cannot, the exception leaves the ViewModel's constructor, and the screen that was opening fails.",
        helpLinkUri: HelpBase + "uix0006");

    /// <summary><c>InitializeMixinsForVMInstance</c> collects members with <c>Type.GetProperties()</c> and <c>GetMethods()</c>, public ones only.</summary>
    public static readonly DiagnosticDescriptor MixinMemberNotPublic = new(
        id: "UIX0007",
        title: "Marked mixin member is not public",
        messageFormat: "'{0}' is marked [{1}] but is not public; UIExtenderEx adds only public members, so nothing can bind it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Only public members of a mixin are added to the ViewModel. A non-public member carrying [DataSourceProperty] or [DataSourceMethod] is skipped without a message.",
        helpLinkUri: HelpBase + "uix0007");

    /// <summary>
    /// <c>ViewModelComponent.RegisterOverrides</c>: the override's own shape, then <c>AccessTools.Method(host, name,
    /// parameters)</c> on the ViewModel.
    /// </summary>
    public static readonly DiagnosticDescriptor OverrideDoesNotMatch = new(
        id: "UIX0008",
        title: "Override does not match a method of the ViewModel",
        messageFormat: "Override '{0}' is not registered: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [BUTRViewModelOverride] method takes the ViewModel method's parameters followed by the original, a delegate taking the same parameters, and returns void, as the ViewModel method has to. UIExtenderEx leaves out an override it cannot match, and the ViewModel method runs as if it were not there.",
        helpLinkUri: HelpBase + "uix0008");

    /// <summary><c>UnsafeAccessorPatch.Resolve</c>.</summary>
    public static readonly DiagnosticDescriptor AccessorNotResolved = new(
        id: "UIX0009",
        title: "Accessor stub names no member UIExtenderEx can find",
        messageFormat: "Accessor '{0}' is not resolved: {1}; calling it throws",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [BUTRUnsafeAccessor] stub is static and names its member by its signature: the instance as the first parameter for an instance member, the type on the attribute for a static one, the member's parameters and return type, and a ref return for a field. A stub UIExtenderEx cannot resolve keeps its own body.",
        helpLinkUri: HelpBase + "uix0009");

    /// <summary><c>UnsafeAccessorPatch.Register</c> checks the stub's implementation flags.</summary>
    public static readonly DiagnosticDescriptor AccessorInlinable = new(
        id: "UIX0010",
        title: "Accessor stub can be inlined",
        messageFormat: "Mark accessor '{0}' [MethodImpl(MethodImplOptions.NoInlining)], so no caller can copy the body it has before UIExtenderEx replaces it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UIExtenderEx replaces an accessor's body when the assembly is registered. A caller compiled before that could have inlined the original body, which throws.",
        helpLinkUri: HelpBase + "uix0010");

    // ---------------------------------------------------------------- prefab patches

    /// <summary>
    /// <c>PrefabComponent.TryGetNodes</c> looks for the content member among the patch's public members, and binds it with
    /// <c>Delegate.CreateDelegate</c> to a parameterless instance delegate returning the attribute's type, which throws
    /// for anything else.
    /// </summary>
    public static readonly DiagnosticDescriptor ContentMemberCannotSupplyContent = new(
        id: "UIX0017",
        title: "Content member cannot supply the patch's content",
        messageFormat: "'{0}' cannot supply the patch's content: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The member with a content attribute has to be a public instance property, or a public parameterless instance method, of the type its attribute names: string for [PrefabExtensionFileName] and [PrefabExtensionText], XmlNode for [PrefabExtensionXmlNode], IEnumerable<XmlNode> for [PrefabExtensionXmlNodes]. Anything else fails the patch when the mod's UI is registered.",
        helpLinkUri: HelpBase + "uix0017");

    // ---------------------------------------------------------------- prefab XML: reported once the whole mod is known

    /// <summary><c>XmlDocument.LoadXml</c> in a patch's constructor, or the game's prefab loader, fails on it.</summary>
    public static readonly DiagnosticDescriptor XmlNotWellFormed = new(
        id: "UIX0011",
        title: "Prefab XML is not well-formed",
        messageFormat: "The XML is not well-formed: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A patch's XML that is not well-formed throws when the patch is created, which fails the registration of the mod's UI; a prefab file that is not well-formed fails when the game loads it.",
        helpLinkUri: HelpBase + "uix0011",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>WidgetExtensions.GetObjectAndProperty</c> finds no property, and the attribute is dropped without a word.</summary>
    public static readonly DiagnosticDescriptor UnknownWidgetAttribute = new(
        id: "UIX0012",
        title: "Widget has no such attribute",
        messageFormat: "'{1}' has no attribute '{0}'; the loader drops it without a message",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An attribute names a public property of the widget. One the widget does not have - usually a misspelling - is dropped when the prefab loads, and the widget keeps its default.",
        helpLinkUri: HelpBase + "uix0012",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>WidgetExtensions.SetWidgetAttributeFromStringAux</c>: <c>Enum.Parse</c>, <c>value == "true"</c>, <c>Convert.ToInt32</c>/<c>ToSingle</c>.</summary>
    public static readonly DiagnosticDescriptor InvalidAttributeValue = new(
        id: "UIX0013",
        title: "Attribute value does not fit its type",
        messageFormat: "'{0}' is not a value '{1}' can take: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The loader converts an attribute's text to the property's type: an enum by member name, a number by parsing, a bool by comparing with \"true\". A name that is not a member or text that is not a number fails the attribute; any bool other than \"true\" reads as false.",
        helpLinkUri: HelpBase + "uix0013",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>A parameter the prefab neither declares nor reads with <c>*Name</c> reaches nothing.</summary>
    public static readonly DiagnosticDescriptor UnknownPrefabParameter = new(
        id: "UIX0014",
        title: "Prefab has no such parameter",
        messageFormat: "Prefab '{1}' neither declares nor reads a parameter '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Parameter.Name on a prefab's tag reaches the attributes in the prefab written *Name. A name the prefab does not use goes nowhere.",
        helpLinkUri: HelpBase + "uix0014",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>ViewModel.GetPropertyValue</c>, <c>GetViewModelAtPath</c> and <c>ExecuteCommand</c> find nothing by the name.</summary>
    public static readonly DiagnosticDescriptor BindingNotFound = new(
        id: "UIX0015",
        title: "ViewModel has no such member",
        messageFormat: "'{1}' has no {2} '{0}', and no mixin of this mod adds one",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A binding or command names a member of the ViewModel at that point of the XML. Neither the ViewModel, the types derived from it, nor this mod's mixins have it: a misspelling, or a member another mod's mixin is expected to add.",
        helpLinkUri: HelpBase + "uix0015",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>The patch's ViewModel is taken from the mod's mixins, and none of their ViewModels answers the name.</summary>
    public static readonly DiagnosticDescriptor BindingOnNoMixinViewModel = new(
        id: "UIX0016",
        title: "Patch binds a member none of the mod's mixin ViewModels has",
        messageFormat: "'{0}' is on none of the ViewModels this mod's mixins extend ({1}); if the patch binds a game ViewModel, link it with [assembly: PrefabLink]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Without a [PrefabLink], a patch's ViewModel is taken from the mod's mixins whose members it binds. A name that none of those ViewModels answers is a misspelling, or a member of a ViewModel no mixin of the mod extends, which a [PrefabLink] names.",
        helpLinkUri: HelpBase + "uix0016",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    // ---------------------------------------------------------------- the game's prefabs, from the GUI packages

    /// <summary><c>PrefabComponent.RegisterPatch</c>: <c>SelectSingleNode(xpath)</c> on the prefab's document answers null.</summary>
    public static readonly DiagnosticDescriptor XPathMatchesNothing = new(
        id: "UIX0020",
        title: "Patch XPath matches no node of the prefab",
        messageFormat: "'{0}' matches no node of '{1}'{2}; the patch is not applied",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UIExtenderEx applies a patch at the first node its XPath selects in the prefab the patch names. When it selects nothing, the patch is skipped and a message is shown in game. Checked against the game's prefabs from a Bannerlord.ReferenceAssemblies.GUI.v2 package, in the game without and with each DLC package referenced, and against the mod's own prefab of that name.",
        helpLinkUri: HelpBase + "uix0020",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary><c>SelectSingleNode</c> takes the first node in document order.</summary>
    public static readonly DiagnosticDescriptor XPathMatchesSeveral = new(
        id: "UIX0021",
        title: "Patch XPath matches several nodes of the prefab",
        messageFormat: "'{0}' matches {1} nodes of '{2}'{3}; UIExtenderEx patches only the first",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UIExtenderEx patches the first node the XPath selects. When it selects several, a game update that adds a matching node before the intended one moves the patch without a word. Narrow the XPath, by an Id for example.",
        helpLinkUri: HelpBase + "uix0021",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>The link names one ViewModel; the game's movie table and types say another binds at the patch's node.</summary>
    public static readonly DiagnosticDescriptor PrefabLinkDisagreesWithGame = new(
        id: "UIX0022",
        title: "Prefab link disagrees with the game",
        messageFormat: "'{0}' is linked to '{1}', but the game binds {2} where the patch goes in",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The ViewModel a [PrefabLink] names for a patch is not the one the game binds at the node the patch's XPath selects, nor a base or subclass of it. The link is still used to check the patch; the game's scope is shown so either can be corrected.",
        helpLinkUri: HelpBase + "uix0022",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>XmlNode.SelectSingleNode</c> throws for an XPath it cannot compile or that does not select nodes, and
    /// <c>PrefabComponent.RegisterPatch</c> hands it an empty one when the patch names none.
    /// </summary>
    public static readonly DiagnosticDescriptor XPathInvalid = new(
        id: "UIX0023",
        title: "Patch XPath is not valid",
        messageFormat: "The XPath{0} cannot be applied: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "UIExtenderEx selects the patch's node with SelectSingleNode, which throws for an XPath that does not parse or does not select nodes, and for an empty one, which is what a patch that names no XPath passes. The patch then fails when the movie loads.",
        helpLinkUri: HelpBase + "uix0023",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    // ---------------------------------------------------------------- [assembly: PrefabLink]

    /// <summary>Nothing at runtime reads the link; one that does not hold together would check the XML against the wrong ViewModel.</summary>
    public static readonly DiagnosticDescriptor PrefabLinkDoesNotHold = new(
        id: "UIX0018",
        title: "Prefab link does not hold together",
        messageFormat: "The link is left out: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "[assembly: PrefabLink] names a prefab patch of the mod or a prefab of the mod by name, the ViewModel its XML binds where it goes in, and optionally a mixin attached to that ViewModel: one extending it, or one extending a base of it with handleDerived. A link that does not is left out, and the patch or prefab is checked as if it had none.",
        helpLinkUri: HelpBase + "uix0018",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>The linked mixin's members are bound nowhere at the point the link names.</summary>
    public static readonly DiagnosticDescriptor LinkedMixinNotBound = new(
        id: "UIX0019",
        title: "Linked XML binds none of the mixin's members",
        messageFormat: "'{0}' is linked to mixin '{1}', but binds none of its members {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [PrefabLink] naming a mixin says the XML binds what that mixin adds. XML that binds none of the mixin's members where it goes in is linked to the wrong mixin, or the link outlived the bindings it was for.",
        helpLinkUri: HelpBase + "uix0019",
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}
