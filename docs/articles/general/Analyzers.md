# Analyzers

**`Bannerlord.UIExtenderEx.Analyzers`** is a Roslyn-based code analyzer package that inspects your ViewModel mixins, C# patch classes, and Gauntlet XML prefabs at compile time.

Many common UI modding bugs—such as mistyped property bindings, broken XPath selectors, invalid method signatures, or unsupported XML structures—silently fail or crash the game when a screen opens at runtime. This analyzer package catches these issues during your build and provides automated **Code Fixes** (Quick Actions) in your IDE to resolve them instantly.

---

## Installing

Add the package alongside your `Bannerlord.UIExtenderEx` reference. Because it is an analyzer, it runs inside the compiler and adds no runtime dependencies to your compiled mod:

```xml
<PackageReference Include="Bannerlord.UIExtenderEx.Analyzers" Version="$(UIExtenderExVersion).*" PrivateAssets="all" />
```

### Requirements & Compatibility
* **SDK:** Requires the .NET 8 SDK or Visual Studio 2022 (17.8 or higher) / JetBrains Rider.
* **UIExtenderEx Versions:** Compatible with UIExtenderEx 2.x and 3.x. Rules adapt their validation logic to the specific UIExtenderEx version referenced by your project.
* **Attributes:** Rules inspecting `[BUTRViewModelOverride]` and `[BUTRUnsafeAccessor]` ([UIX0008](#uix0008)–[UIX0010](#uix0010)) require UIExtenderEx 3.0+. For older versions, the analyzer automatically injects an `internal` copy of the [`[PrefabLink]`](../v2/PrefabLink.md) attribute into your project if it is not already present.
* **Prefab Analysis:** The analyzer automatically inspects XML files embedded in your assembly or located under your project's `GUI/` directory. To disable XML prefab analysis, add `<UIExtenderExAnalyzePrefabs>false</UIExtenderExAnalyzePrefabs>` to your `.csproj`.

---

## Checking Against the Game's Prefabs

Prefab patches (such as `[PrefabExtension]` classes) modify the game's own prefabs rather than local files in your project. To validate them, the analyzer inspects vanilla GUI metadata: movie prefabs, default ViewModel bindings, and widget property signatures.

The analyzer package includes a dependency on `Bannerlord.ReferenceAssemblies.GUI.v3.All`, which bundles GUI definitions for the latest build of every release game version (both base game and War Sails). Simply referencing the analyzer enables vanilla prefab and ViewModel validation out of the box:

* **Comprehensive Validation:** Your patches are validated against every version listed in your `supported-game-versions.txt` (or configured via `<UIExtenderExGameVersions>v1.2.12;v1.3.4;v1.4.8</UIExtenderExGameVersions>`). If no list is specified, patches are validated against the game version your project builds against (see below). Validation covers XPath expressions ([UIX0020](#uix0020)), ViewModel property bindings and command methods ([UIX0015](#uix0015)), `[PrefabLink]` associations ([UIX0022](#uix0022)), widget attributes and enum values ([UIX0012](#uix0012), [UIX0013](#uix0013)), and bound data types ([UIX0025](#uix0025)), reading each version's layout from its own `types.json`.
* **Custom & Mixin Symbols:** Members declared in your own mod (such as mixin properties and methods) are automatically recognized across all versions. Any custom ViewModel or widget not recorded in base game packages is validated against your project's compiled symbols.
* **Cross-Version Discrepancies:** If a patch succeeds in some targeted versions but fails in others (for example, if a node ID or ViewModel property was introduced or removed in a game update), the analyzer flags it as [UIX0024](#uix0024), clearly detailing the affected versions.
* **Missing Version Alerts:** Any targeted version not included in the GUI bundle (such as a beta branch or a game update released after the analyzer package) is flagged as [UIX0030](#uix0030).

The bundle dependency bundled with the analyzer reflects the latest game versions available when that analyzer was published. To validate against game versions released after your current analyzer package, reference a newer bundle package directly:

```xml
<PackageReference Include="Bannerlord.ReferenceAssemblies.GUI.v3.All" Version="*" PrivateAssets="all" />
```

Individual per-build packages provide GUI definitions for a single specific game build. Reference these when validating against versions the bundle does not contain (such as beta or Early Access branches), or to target the exact patch build your project compiles against. If a version is provided by both the bundle and a per-build reference, the per-build package takes precedence:

```xml
<PackageReference Include="Bannerlord.ReferenceAssemblies.GUI.v3" Version="$(GameVersion).*" PrivateAssets="all" />
<PackageReference Include="Bannerlord.ReferenceAssemblies.GUI.v3.NavalDLC" Version="$(GameVersion).*" PrivateAssets="all" />
```

* For Early Access versions (`e1.x`), use `Bannerlord.ReferenceAssemblies.GUI.v3.EarlyAccess`.
* For beta branches, use `$(GameVersion).*-*` to match the `-beta` prerelease suffix.
* To disable checks against vanilla prefabs entirely, set `<UIExtenderExAnalyzeGamePrefabs>false</UIExtenderExAnalyzeGamePrefabs>`.

> [!NOTE]
> The analyzer receives GUI definitions and discovered prefab paths via MSBuild targets. If you consume the analyzer transitively (such as through a `Bannerlord.UIExtenderEx` package reference) with restricted asset inclusion like `IncludeAssets="compile"`, MSBuild targets are not imported. In that case, neither vanilla prefab validation nor local XML checks will run. Reference `Bannerlord.UIExtenderEx.Analyzers` directly to ensure the targets are imported.

### The Game Version You Build Against

When no list of supported versions is specified, the analyzer checks your patches against the specific game version your project builds against. This version tags every diagnostic message (e.g. `[v1.4.8]`). The analyzer resolves this version using the following precedence:

1. `<UIExtenderExGameVersion>v1.4.8</UIExtenderExGameVersion>`, set explicitly in your project.
2. `$(GameVersion)`, set by `Bannerlord.BUTRModule.Sdk` or `Bannerlord.BuildResources`.
3. Inferred automatically from your project references:
   * **NuGet Packages:** Inferred from `Bannerlord.ReferenceAssemblies.*` (e.g., `1.3.4.102430` resolves to `v1.3.4`, and `.EarlyAccess` packages resolve to `e1.9.0`).
   * **Local Game Folders:** When referencing local game binaries, the analyzer inspects the directory containing `TaleWorlds.Library.dll` using [FetchBannerlordVersion](https://github.com/BUTR/FetchBannerlordVersion) to locate and read `Version.xml`.
4. The newest version available in the GUI bundle (fallback).

---

## Code Fixes

Most analyzer warnings include built-in Roslyn code fixes accessible via your IDE's Quick Actions menu (`Ctrl+.` in Visual Studio, `Alt+Enter` in JetBrains Rider):

1. **Automated Structural Fixes:** Rules like [UIX0004](#uix0004), [UIX0006](#uix0006), [UIX0007](#uix0007), [UIX0010](#uix0010), [UIX0017](#uix0017), and [UIX0019](#uix0019) have a single unambiguous solution. These fixes support **"Fix All in Solution/Project"**.
2. **Fuzzy Name Suggestions:** For misspelled property names, attributes, or parameters ([UIX0003](#uix0003), [UIX0012](#uix0012)–[UIX0016](#uix0016)), the analyzer offers up to three closest matching names ranked by Levenshtein distance.
3. **XML File & String Literal Fixes:** Code fixes can modify both standalone `.xml` files in your project and XML string literals embedded in C# patch classes.

---

## Which ViewModel Your XML Binds

To validate bindings (`@PropertyName`) and commands (`Command.Click="MethodName"`), the analyzer determines the active ViewModel context for each XML node:

* **Automatic Mixin Discovery:** If your patch binds a property added by one of your mixins, the analyzer automatically infers that the patch targets that mixin's `ViewModel`.
* **Explicit Linking via `[PrefabLink]`:** When a patch binds only vanilla ViewModel members, or when multiple mixins could match, annotate your assembly with [`[assembly: PrefabLink]`](../v2/PrefabLink.md) to explicitly declare the target:
  ```csharp
  [assembly: PrefabLink(typeof(MyOptionsPatch), typeof(OptionsVM), typeof(OptionsVMMixin))]
  ```
* **Replacement Prefabs:** For standalone prefabs that replace vanilla screens, link the prefab name directly to the ViewModel:
  ```csharp
  [assembly: PrefabLink("ClansPanel", typeof(ClanManagementVM))]
  ```
* **Hierarchical Scope Resolution:** The analyzer follows `DataSource="{...}"` navigation paths, list item templates, parameter delegations, and nested custom prefabs recursively.

---

## Rules Summary Table

| Rule | Severity | Category | Description | Code Fix |
| :--- | :--- | :--- | :--- | :--- |
| [UIX0001](#uix0001) | Warning | ViewModel Mixin | Mixin member replaces an existing member of the target ViewModel | — |
| [UIX0002](#uix0002) | Warning | ViewModel Mixin | Two mixins register conflicting duplicate member names on the same ViewModel | — |
| [UIX0003](#uix0003) | Warning | ViewModel Mixin | Refresh method named in `[ViewModelMixin]` not found on target ViewModel | Suggests matching method |
| [UIX0004](#uix0004) | Warning | ViewModel Mixin | Mixin extends an abstract ViewModel without `handleDerived: true` | Adds `handleDerived: true` |
| [UIX0005](#uix0005) | Warning | ViewModel Mixin | Cannot determine target ViewModel from mixin inheritance hierarchy | — |
| [UIX0006](#uix0006) | Error | ViewModel Mixin | Mixin cannot be instantiated (invalid constructor or abstract class) | Generates valid constructor |
| [UIX0007](#uix0007) | Warning | ViewModel Mixin | Member annotated with `[DataSourceProperty]` or `[DataSourceMethod]` is not public | Makes member public |
| [UIX0008](#uix0008) | Warning | Method Override | `[BUTRViewModelOverride]` matches no method on the target ViewModel | — |
| [UIX0009](#uix0009) | Warning | Unsafe Accessor | `[BUTRUnsafeAccessor]` stub member not found on target type | — |
| [UIX0010](#uix0010) | Warning | Unsafe Accessor | `[BUTRUnsafeAccessor]` stub is missing `MethodImplOptions.NoInlining` | Adds `[MethodImpl]` attribute |
| [UIX0011](#uix0011) | Error | XML Prefab | Prefab XML syntax is malformed or invalid | — |
| [UIX0012](#uix0012) | Warning | XML Prefab | Attribute does not exist on the target Widget class | Suggests matching property |
| [UIX0013](#uix0013) | Warning | XML Prefab | Attribute literal value cannot be converted to the target property type | Suggests valid enum/bool value |
| [UIX0014](#uix0014) | Warning | XML Prefab | Prefab parameter is not declared or referenced | Suggests matching parameter |
| [UIX0015](#uix0015) | Warning | XML Prefab | Binding or command name not found on the active ViewModel or mixin | Suggests matching member |
| [UIX0016](#uix0016) | Warning | XML Prefab | Patch binds a member that none of your mixins provide | Suggests member or `PrefabLink` |
| [UIX0017](#uix0017) | Error | Prefab Patch | Insert patch content member cannot be read by UIExtenderEx | Fixes accessibility / attribute |
| [UIX0018](#uix0018) | Warning | Prefab Link | `[PrefabLink]` type configuration is invalid or inconsistent | — |
| [UIX0019](#uix0019) | Warning | Prefab Link | Linked XML does not bind any members of the linked mixin | Removes unused mixin |
| [UIX0020](#uix0020) | Warning | XPath Patch | Patch XPath matches zero nodes in the target prefab | — |
| [UIX0021](#uix0021) | Warning | XPath Patch | Patch XPath matches multiple nodes (only first will be patched) | — |
| [UIX0022](#uix0022) | Warning | Prefab Link | `[PrefabLink]` specifies a different ViewModel than the game binds at the node | — |
| [UIX0023](#uix0023) | Error | XPath Patch | Patch XPath expression is syntactically invalid | — |
| [UIX0024](#uix0024) | Warning | Multi-Version | Patch XPath or binding fails in a subset of supported game versions | — |
| [UIX0025](#uix0025) | Error | Data Binding | Incompatible binding types between widget and ViewModel will throw | — |
| [UIX0026](#uix0026) | Error | Data Binding | Multi-step DataSource path will not refresh when replaced in XML loader | — |
| [UIX0027](#uix0027) | Error | Data Binding | Parameter inside `<LogicalChildrenLocation />` is not forwarded properly | — |
| [UIX0028](#uix0028) | Error | Data Binding | Indexed DataSource path (`{List\0}`) is not tracked dynamically in XML loader | — |
| [UIX0029](#uix0029) | Warning | XML Prefab | XML tag matches neither a known Widget class nor a registered prefab | — |
| [UIX0030](#uix0030) | Info | Multi-Version | Targeted game version is missing from GUI reference packages; patches cannot be validated against it | — |

---

## Detailed Rules Reference

### UIX0001
**A mixin member replaces an existing member of the ViewModel it extends.**  
*Severity: Warning*

When a mixin defines a `[DataSourceProperty]` or `[DataSourceMethod]` with the same name as a member on the target ViewModel, the mixin member shadows the original in Gauntlet's binding table. While UI bindings will resolve to your mixin, internal game code continues to invoke the original ViewModel method directly. For example, replacing `ExecuteDone` via a mixin method causes a button click to run your code, but hotkeys or engine callers will continue invoking vanilla logic.

```csharp
[ViewModelMixin]
public sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    public OptionsVMMixin(OptionsVM vm) : base(vm) { }

    // Warning UIX0001: OptionsVM already defines ExecuteDone
    [DataSourceMethod] 
    public void ExecuteDone() { } 
}
```

**Resolution:**
* If you did not intend to override vanilla behavior, rename the member with a unique prefix (e.g., `MyMod_ExecuteDone`).
* If you **do** want to intercept and take over the method, use `[BUTRViewModelOverride]` (UIExtenderEx 3.0+). Unlike basic mixin shadowing, an override replaces the method for all callers and allows chaining the original implementation:
  ```csharp
  [BUTRViewModelOverride(nameof(OptionsVM.ExecuteDone))]
  private void ExecuteDone(Action original)
  {
      SaveCustomSettings();
      original(); // Call vanilla implementation
  }
  ```

---

### UIX0002
**Two mixins add the same member name to one ViewModel.**  
*Severity: Warning*

If two distinct mixins in your mod declare a property or method with identical names on the same target ViewModel, both write to the binding table. UIExtenderEx routes bindings to whichever mixin was registered last, leaving the earlier mixin's member unbound. A warning naming both mixins is logged.

**Resolution:** Rename one of the members to ensure unique property/method names per ViewModel.

---

### UIX0003
**The refresh method specified in `[ViewModelMixin]` cannot be found on the ViewModel.**  
*Severity: Warning*

When configuring `[ViewModelMixin("RefreshMethodName")]`, UIExtenderEx looks for a parameterless method on the target ViewModel. If the name is misspelled or does not exist, the `OnRefresh` lifecycle hook is never triggered.

```csharp
// Warning UIX0003: "RefreshValuez" does not exist on OptionsVM
[ViewModelMixin("RefreshValuez")] 
```

**Resolution & Code Fix:**  
Use `nameof(TargetVM.RefreshValues)` so the C# compiler validates the method name. The automated code fix suggests matching parameterless methods found on the ViewModel.

---

### UIX0004
**A mixin extends an abstract ViewModel without `handleDerived: true`.**  
*Severity: Warning*

UIExtenderEx attaches mixins to instances whose concrete runtime type matches the target ViewModel. Because `abstract` classes cannot be instantiated, a mixin targeting an abstract ViewModel without derived handling will never execute.

**Resolution & Code Fix:**  
Apply the code fix to add `handleDerived: true` to your `[ViewModelMixin]` attribute, or change the target type to a concrete subclass.

---

### UIX0005
**UIExtenderEx cannot determine which ViewModel the mixin extends.**  
*Severity: Warning*

UIExtenderEx inspects the generic parameter of `BaseViewModelMixin<TViewModel>` to identify the target type. If a class implements `IViewModelMixin` directly or through complex multiple interfaces, the target type cannot be reliably determined.

**Resolution:** Inherit directly or indirectly from `BaseViewModelMixin<TViewModel>`.

---

### UIX0006
**The mixin cannot be instantiated by UIExtenderEx.**  
*Severity: Error*

UIExtenderEx instantiates mixins when the target ViewModel constructor finishes. To construct the mixin, it requires a public constructor accepting the ViewModel instance as its single parameter:

```csharp
public class MyMixin : BaseViewModelMixin<MyViewModel>
{
    // Required constructor
    public MyMixin(MyViewModel vm) : base(vm) { }
}
```

If the mixin is abstract, generic without arguments, or missing this constructor, instantiation throws an exception and crashes screen loading.

**Resolution & Code Fix:**  
The automated code fix generates the required public constructor or removes invalid `abstract` modifiers.

---

### UIX0007
**A member marked `[DataSourceProperty]` or `[DataSourceMethod]` is not public.**  
*Severity: Warning*

Gauntlet only exposes public members to UI prefabs. Any member marked with `[DataSourceProperty]` or `[DataSourceMethod]` that is `private`, `internal`, or `protected` is silently ignored at runtime.

**Resolution & Code Fix:**  
Use the code fix to change the member's visibility to `public`. (For properties, a `private set` accessor is supported as long as the property getter is `public`).

---

### UIX0008
**A `[BUTRViewModelOverride]` method matches no method on the ViewModel.**  
*Severity: Warning (UIExtenderEx 3.0+)*

An override method must return `void`, match the target method's parameter list, and conclude with an `Action` delegate representing the original method. If the name or signature does not match any method on the target ViewModel, UIExtenderEx cannot hook the call.

```csharp
[BUTRViewModelOverride(nameof(OptionsVM.ExecuteDone))]
private void ExecuteDone(Action original) => original();
```

**Resolution:** Ensure the method name and parameters precisely match the target ViewModel method.

---

### UIX0009
**A `[BUTRUnsafeAccessor]` stub member cannot be found.**  
*Severity: Warning (UIExtenderEx 3.0+)*

`[BUTRUnsafeAccessor]` generates high-performance IL accessors for private fields and methods. If the member signature cannot be resolved on the target type (e.g. after a game update renamed an internal field), the stub retains its default body and throws a `NotImplementedException` when called.

```csharp
[BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_categories")]
[MethodImpl(MethodImplOptions.NoInlining)]
private static ref List<ViewModel> Categories(OptionsVM instance) => throw new NotImplementedException();
```

**Resolution:** Update the stub's signature or `Name` property to match the target type's current internal structure.

---

### UIX0010
**A `[BUTRUnsafeAccessor]` stub is not marked `NoInlining`.**  
*Severity: Warning (UIExtenderEx 3.0+)*

Because UIExtenderEx dynamically rewrites the method body of accessor stubs during assembly registration, the JIT compiler must be prevented from inlining the initial throwing stub into caller methods.

**Resolution & Code Fix:**  
Apply the automated code fix to add `[MethodImpl(MethodImplOptions.NoInlining)]`.

---

### UIX0011
**Prefab XML is not well-formed.**  
*Severity: Error*

The prefab XML file or embedded C# string literal contains XML syntax errors (such as unclosed tags or mismatched quotes). Malformed XML causes patch registration or screen construction to throw an unhandled `XmlException`.

**Resolution:** Correct the XML syntax.

---

### UIX0012
**The widget has no such attribute.**  
*Severity: Warning*

Attributes in Gauntlet XML map directly to C# properties on widget classes. If an attribute name is misspelled or does not exist on the widget, Gauntlet silently drops the attribute, causing visual styling or layout settings to be ignored.

```xml
<!-- Warning UIX0012: ScrollablePanel has no 'HorizontalAlightment' attribute -->
<ScrollablePanel HorizontalAlightment="Left" />
```

When a GUI package is referenced, attributes applied to game widgets—both in `[PrefabExtensionSetAttributePatch]` classes and within your mod's own XML prefabs—are validated against widget property definitions across all supported game versions using `types.json` (along with their values under [UIX0013](#uix0013)). If an attribute exists in some targeted versions but is missing in others, the analyzer reports [UIX0024](#uix0024). Custom widget classes defined in your mod or external assemblies are validated directly against your project's compiled symbols.

**Resolution & Code Fix:**  
Apply the code fix to correct the attribute spelling (e.g., change to `HorizontalAlignment`).

---

### UIX0013
**The attribute's value cannot be converted to the target property type.**  
*Severity: Warning*

Gauntlet's loader converts literal string values to property types (parsing enums, numeric values, and booleans). If an enum value is misspelled or a numeric string is invalid, attribute conversion fails.

> [!NOTE]
> Gauntlet requires lowercase `"true"` or `"false"` for boolean attributes. A value such as `IsVisible="True"` fails conversion and evaluates to `false`.

**Resolution & Code Fix:**  
Apply the code fix to correct enum casing or replace invalid boolean strings.

---

### UIX0014
**The prefab has no such parameter.**  
*Severity: Warning*

When a tag passes a parameter (e.g., `Parameter.HeaderTitle="Custom"`), the underlying prefab must declare or reference that parameter via `*HeaderTitle`. Unused parameters have no effect.

**Resolution & Code Fix:**  
Use the code fix to update the parameter name to match the prefab's defined parameters.

---

### UIX0015
**The ViewModel has no such member.**  
*Severity: Warning*

A data binding (`@PropertyName`), command (`Command.Click="MethodName"`), or DataSource path step (`{ChildPath}`) references a member that does not exist on the active ViewModel or its attached mixins. In the XML loader, this causes silent binding failures.

With a GUI package referenced, a game ViewModel's members are read from each supported version's `types.json`. A member only some of those versions lack is reported as [UIX0024](#uix0024), naming them.

**Resolution & Code Fix:**  
Apply the code fix to select from matching properties and methods available on the active ViewModel and mixins.

---

### UIX0016
**A patch binds a member that none of your mixins provide.**  
*Severity: Warning*

When a prefab patch is not explicitly linked via `[PrefabLink]`, the analyzer infers the target ViewModel by searching your mixins. If none of your mixins declare the referenced property, the analyzer cannot determine whether the binding is a misspelling or belongs to a vanilla ViewModel.

**Resolution & Code Fix:**  
If the binding references a vanilla ViewModel member, annotate your assembly with `[assembly: PrefabLink(...)]` to link the patch to that ViewModel. Otherwise, correct the property name.

---

### UIX0017
**An insert patch content member cannot be read by UIExtenderEx.**  
*Severity: Error*

In `PrefabExtensionInsertPatch` classes, the member providing the XML content must be a public, parameterless instance method or property matching the attribute used:

| Attribute | Expected Return Type |
| :--- | :--- |
| `[PrefabExtensionText]`, `[PrefabExtensionFileName]` | `string` |
| `[PrefabExtensionXmlNode]` | `XmlNode` (or derived, like `XmlDocument`) |
| `[PrefabExtensionXmlNodes]` | `IEnumerable<XmlNode>` (e.g., `List<XmlNode>`) |

```csharp
[PrefabExtension("Options", "descendant::Widget[@Id='Panel']")]
internal sealed class CustomPanelPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    // Error UIX0017: string return type requires [PrefabExtensionText]
    [PrefabExtensionXmlNode]
    public string GetContent() => "<Widget />";
}
```

**Resolution & Code Fix:**  
Apply the code fix to update the attribute or make the member a public instance method/property.

---

### UIX0018
**A `[PrefabLink]` configuration is invalid.**  
*Severity: Warning*

A `[PrefabLink]` attribute must reference valid types: the patch class, the target ViewModel, and an optional mixin that actually extends that ViewModel.

```csharp
// Warning UIX0018: InventoryVMMixin extends SPInventoryVM, not OptionsVM
[assembly: PrefabLink(typeof(OptionsPatch), typeof(OptionsVM), typeof(InventoryVMMixin))]
```

**Resolution:** Ensure that the mixin type extends the specified ViewModel (or a base type if using `handleDerived`).

---

### UIX0019
**Linked XML does not bind any members of the linked mixin.**  
*Severity: Warning*

A `[PrefabLink]` includes a mixin parameter, but the patch's XML only references vanilla ViewModel members.

**Resolution & Code Fix:**  
Apply the code fix to simplify the link to `[assembly: PrefabLink(typeof(Patch), typeof(TargetVM))]`, removing the redundant mixin reference.

---

### UIX0020
**A patch's XPath matches zero nodes in the prefab.**  
*Severity: Warning*

The XPath selector failed to find any matching node in the targeted prefab. At runtime, the patch is skipped, an error is logged, and a red error message is displayed in-game. This frequently occurs after base game updates rename widget IDs or restructure hierarchies.

```text
warning UIX0020: 'descendant::ListPanel[@Id='OldPanelId']' matches no node of 'Options'; the patch is not applied
```

**Resolution:** Inspect the target prefab in the game files (or GUI packages) and update the XPath selector to match the current XML hierarchy.

---

### UIX0021
**A patch's XPath matches multiple nodes in the prefab.**  
*Severity: Warning*

UIExtenderEx applies patches only to the **first** node selected by an XPath expression. If multiple nodes match, a future layout change or game update could alter node order, causing your patch to attach to the wrong element.

**Resolution:** Refine the XPath query (for example, by filtering on `@Id` or unique attribute combinations) so that it uniquely selects exactly one node.

---

### UIX0022
**A `[PrefabLink]` specifies a different ViewModel than the game binds at the node.**  
*Severity: Warning*

When validating against base game GUI reference packages, the analyzer detects that the node selected by your XPath actually binds to a different ViewModel than declared in your `[PrefabLink]`, or a ViewModel that is neither a base nor a subclass of it. Each supported version is checked; a disagreement in only some of them is reported as [UIX0024](#uix0024).

**Resolution:** Update your `PrefabLink` to match the actual ViewModel used at that node location in the game's prefab.

---

### UIX0023
**A patch's XPath expression is syntactically invalid.**  
*Severity: Error*

The XPath string contains syntax errors, is `null`, or evaluates to an invalid XPath return type (such as a numeric scalar like `count(//Widget)`). This will throw an exception when the patch is applied.

**Resolution:** Fix the XPath query syntax.

---

### UIX0024
**A patch's XPath or binding fails in a subset of supported game versions.**  
*Severity: Warning*

When validating against `Bannerlord.ReferenceAssemblies.GUI.v3.All`, this rule flags patches that succeed in some versions but fail in others due to vanilla changes across game updates. The diagnostic embeds the specific underlying rule that failed in the affected versions—such as an XPath that matches nothing or multiple elements ([UIX0020](#uix0020), [UIX0021](#uix0021)), a missing ViewModel property or method ([UIX0015](#uix0015)), a conflicting `[PrefabLink]` ([UIX0022](#uix0022)), an unsupported widget attribute or value ([UIX0012](#uix0012), [UIX0013](#uix0013)), or an incompatible binding type ([UIX0025](#uix0025)). For issues specific to DLC configurations within a version, the message explicitly identifies the required DLC.

```text
warning UIX0024: [v1.4.8] In v1.0.0 to v1.3.15 only: 'descendant::*[@Id='ExtraInfoWidget']' matches no node of 'Options'; the patch is not applied
```

**Resolution:**
* Update the XPath to target an element present across all supported versions.
* Use version symbols (e.g. `#if !v1315`) to conditionally include different patches per game version.

**Code Fix:**  
When reporting [UIX0012](#uix0012), [UIX0013](#uix0013), or [UIX0015](#uix0015), automated code fixes offer suggestions that are guaranteed to be valid across **all** targeted game versions. If an attribute or member was renamed across game updates and no single identifier exists in every targeted version, no code fix is offered to prevent breaking other versions.

---

### UIX0025
**A binding attempts to pass data between incompatible types.**  
*Severity: Error*

In Gauntlet's dynamic XML loader, transferring data between incompatible types causes an unhandled `ArgumentException` inside `MethodInfo.Invoke`:
1. **ViewModel to Widget:** Setting a widget property with an incompatible ViewModel type (e.g., binding a `bool` property to a `float` property like `SuggestedWidth`). Gauntlet's loader only performs automatic conversion for `string` values, converting them into the specific target types recorded in the game version's GUI package (`stringConversions` in `types.json`); binding a `string` to an unsupported target (such as an enum like `HorizontalAlignment`) throws an exception. If no GUI package is referenced or the package records no conversions, the analyzer uses a fallback set of types supported across all Bannerlord versions: `Sprite`, `Brush`, `int` (`System.Int32`), and `Color`.
2. **Widget to ViewModel:** Many widgets announce values of different types than their property type. For example, `SuggestedWidth` announces a `float` value; binding an `int` property to it will throw on every change.

```xml
<!-- Error UIX0025: Width is an int, but SuggestedWidth announces a float -->
<Widget SuggestedWidth="@Width" />
```

**Resolution:** Align property types to match the announced value. For properties that announce strings (such as alignments in modern game versions), use an `object` property that parses the string into the enum.

---

### UIX0026
**A multi-step DataSource path will not refresh when replaced in the XML loader.**  
*Severity: Error*

When a parent ViewModel replaces a child ViewModel instance, Gauntlet's XML loader only refreshes widgets that bound to the child in a single step (`DataSource="{Child}"`). Widgets bound via multi-step paths (such as `{Parent\Child}` or `{..\Sibling}`) retain stale references to the old instance.

```xml
<Widget DataSource="{Parent}">
  <Children>
    <!-- Error UIX0026: Keeps old instance when Sibling is replaced -->
    <Widget DataSource="{..\Sibling}" /> 
  </Children>
</Widget>
```

**Resolution:** Restructure widget scopes so child ViewModels are bound directly from their owning ViewModel scope.

---

### UIX0027
**A parameter inside a `<LogicalChildrenLocation />` is not forwarded properly.**  
*Severity: Error*

When children are passed into a prefab containing a `<LogicalChildrenLocation />`, Gauntlet's XML loader evaluates parameter attributes (`*ParamName`) using the receiving prefab's parameter context rather than the declaring prefab's context.

```xml
<WindowFrame> <!-- Contains <LogicalChildrenLocation /> -->
  <Children>
    <!-- Error UIX0027: *Title is evaluated against WindowFrame, not the outer prefab -->
    <TextWidget Text="*Title" /> 
  </Children>
</WindowFrame>
```

**Resolution:** Forward the parameter explicitly on the container widget: `<WindowFrame Parameter.Title="*Title">`.

---

### UIX0028
**An indexed DataSource path (`{List\0}`) is not tracked dynamically in the XML loader.**  
*Severity: Error*

Gauntlet's XML loader does not update indexed list paths when collections are modified in place. If an item is inserted, removed, or swapped, the widget bound via `{List\0}` continues displaying the old item.

**Resolution:** Expose a dedicated ViewModel property for the specific item (e.g. `FirstItem`), or replace the collection instance entirely instead of mutating it in place.

---

### UIX0029
**An XML tag matches neither a known Widget class nor a registered prefab.**  
*Severity: Warning*

Gauntlet instantiates a generic base `Widget` when it encounters an unknown tag, logging an engine assertion and failing to load custom widget logic.

**Resolution:** Verify the tag spelling. If using a custom widget class from an external assembly or created in code, register it before the movie loads via `WidgetFactoryManager.Register(typeof(MyCustomWidget))`.

---

### UIX0030
**Targeted game version is missing from GUI reference packages.**  
*Severity: Info*

The GUI bundle includes definitions for the latest release builds available when the bundle package was published. If your mod targets a game version that is not contained in the bundle (such as a newer game update, a beta branch, or an Early Access release), the analyzer cannot validate your patches against that version, leaving potential XPath, binding, or attribute errors undetected in that specific version.

```text
info UIX0030: [v1.5.3] The game's GUI data has no v1.5.3, so the patches are not checked against it; the newest the bundle holds is v1.4.8. ...
```

**Resolution:**
* **Newer Game Versions:** Reference a newer bundle package using a floating version: `<PackageReference Include="Bannerlord.ReferenceAssemblies.GUI.v3.All" Version="*" PrivateAssets="all" />`.
* **Beta or Early Access Builds:** Reference that build's dedicated per-build package (e.g., `Bannerlord.ReferenceAssemblies.GUI.v3` with `$(GameVersion).*-*` or `Bannerlord.ReferenceAssemblies.GUI.v3.EarlyAccess`; see [Checking Against the Game's Prefabs](#checking-against-the-games-prefabs)).
