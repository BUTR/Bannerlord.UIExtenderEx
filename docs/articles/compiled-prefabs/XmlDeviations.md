# Deviations from the XML Loader

> [!NOTE]
> This article is written for **UIExtenderEx maintainers and core contributors**.
> It catalogs all intentional behavioral differences where compiled C# prefabs diverge from Gauntlet's runtime XML loader.
> For the triage rules governing how differences are approved, see [Handling Discrepancies from the XML Loader](HandlingDifferences.md).
> For differences between UIExtenderEx and TaleWorlds' offline code generator, see [Code Generator](Generator.md#4-intended-behavioral-deviations-from-taleworlds-generator).

---

## Overview

A compiled prefab replicates the behavior of Gauntlet UI's runtime XML loader—including its asserts, non-fatal quirks, and failure modes—everywhere except the documented cases below.

Each deviation represents a scenario where the XML loader either produces corrupted UI state or crashes on benign developer mistakes, and the compiled prefab intentionally executes the correct behavior. Every deviation is pinned by automated tests covering **both** the XML behavior and the compiled behavior.

---

## Catalog of Intentional Deviations

### 1. Replaced Child ViewModel Updates Every Bound Widget

- **XML Behavior**: When a parent ViewModel replaces a child ViewModel instance, `GauntletView` only refreshes views directly nested beneath the view bound to that parent. Any widget that accesses the child ViewModel through a sibling scope (e.g., `DataSource="{..\Visual}"` under an adjacent `"{Hint}"` scope) fails to update and continues displaying stale data from the previous ViewModel instance.
- **Compiled Behavior**: All widgets bound to paths traversing the replaced child ViewModel are rebound to the new instance. Widgets whose bound values remain identical are reassigned without side effects (only internal brush `Version` counters reflect the assignment).
- **Rationale**: Retaining stale data on screen is a silent failure that corrupts UI state.
- **Roslyn Analyzer**: [UIX0026](../general/Analyzers.md#uix0026) flags such sibling-traversing data source expressions as compile-time errors.
- **Test Pinning**: Verified by `BindingLoaderOracle.IntendedDeviations["stale child ViewModel"]`.

---

### 2. Child in Logical Children Location Resolves Parameters in Declaring Scope

- **XML Behavior**: When a widget passes child elements to a prefab that defines a `<LogicalChildrenLocation />`, `WidgetTemplate.CreateWidgets` moves those children under the target prefab's internal container. When `SetAttributes` subsequently evaluates parameters, it resolves `*Parameter` expressions against the *receiving* prefab's parameters rather than the *declaring* prefab's parameters. Consequently, any parameter passed into the declaring prefab is ignored, falling back to default values:

  ```xml
  <!-- MyPanel.xml -->
  <Parameters><Parameter Name="Title" DefaultValue="Untitled" /></Parameters>
  <Window>
    <WindowFrame> <!-- Contains <LogicalChildrenLocation /> -->
      <Children><TextWidget Text="*Title" /></Children>
    </WindowFrame>
  </Window>

  <!-- Usage in another prefab: XML renders "Untitled", Compiled renders "Recruits" -->
  <MyPanel Parameter.Title="Recruits" />
  ```

- **Compiled Behavior**: Parameter references (`*Parameter`) are consistently resolved against the parameters of the prefab in which the widget was declared.
- **Rationale**: In the XML loader, whether a prefab's parameters reach its own widgets depends entirely on the internal implementation details of child prefabs. Introducing a content slot into a prefab silently breaks all consuming widgets.
- **Roslyn Analyzer**: [UIX0027](../general/Analyzers.md#uix0027) flags parameter lookups inside logical children locations as compile-time errors and suggests explicitly forwarding them.
- **Test Pinning**: Verified by `FuzzFindingTests.APassedChildsParameter_InALogicalChildrenLocation_IsResolvedWhereItIsDeclared`.

---

### 3. Incompatible Bound Property Value Assigns Nothing on Initial Load

- **XML Behavior**: If a bound ViewModel property cannot be converted by `WidgetExtensions.ConvertObject` or widened via reflection (e.g., binding an `int` property to a `string` widget property, or a `string` to an `XmlElement`), `MethodInfo.Invoke` throws an `ArgumentException` during initial movie binding, aborting screen loading.
- **Compiled Behavior**: The initial binding pass skips the assignment, allowing the widget property to retain its XML default value. Subsequent notifications carrying incompatible payloads route through `SetWidgetAttribute` and throw as XML does. Types that might be assignable at runtime (`object`, unsealed classes, or interfaces) are always evaluated dynamically.
- **Rationale**: Crashing an entire screen and locking the player out of gameplay over a single non-critical binding mistake is unacceptable UI behavior.
- **Roslyn Analyzer**: [UIX0025](../general/Analyzers.md#uix0025) reports incompatible type bindings as compile-time errors.
- **Test Pinning**: Verified by `XmlLoaderParityTests.APairNeitherConvertsNorWidens_IsNotAssignedAtAll`.

---

### 4. Incompatible Widget Value Is Not Written Back to ViewModel

- **XML Behavior**: In two-way bindings, `GauntletView.OnViewPropertyChanged` passes the widget's announced value unconverted to `ViewModel.SetPropertyValue`. When the ViewModel property cannot accept the type (e.g., a `BrushWidget` announcing a `Brush` bound to a `string` property), reflection throws an `ArgumentException` every time the widget property changes. The ViewModel value does not update, and the exception disrupts execution.
- **Compiled Behavior**: The write-back assignment is skipped when types are demonstrably incompatible. Null values are written back normally, numeric conversions are handled via casting, and flexible types (`object`, interfaces) are assigned dynamically.
- **Rationale**: An unhandled exception firing continuously on UI changes without changing state is a framework defect.
- **Roslyn Analyzer**: [UIX0025](../general/Analyzers.md#uix0025) flags incompatible two-way write-back bindings at compile time.
- **Test Pinning**: Verified by `NeverFittingWriteBackTests` and `BindingLoaderOracle.IntendedDeviations["value the property can never take not written back"]`.

---

### 5. `Widget.Tag` Is Initialized to Null

- **XML Behavior**: The `WidgetTemplate` constructor assigns `Tag = Guid.NewGuid()` on every parse for the benefit of TaleWorlds' internal prefab editor.
- **Compiled Behavior**: `Widget.Tag` remains `null`.
- **Rationale**: `Tag` is purely internal editor metadata that generates non-deterministic values per parse. TaleWorlds' own pre-compiled assemblies leave `Tag` as `null`.
- **Roslyn Analyzer**: None required (editor-only metadata).
- **Test Pinning**: Verified by `LoaderOracle.IgnoredProperties["Tag"]`.

---

### 6. Alignment Properties Bound to Enum Properties Function Bi-directionally

- **XML Behavior**: `Widget.HorizontalAlignment` and `VerticalAlignment` announce change events using string representations of enum values (e.g., `"Top"`). `GauntletView.OnViewPropertyChanged` passes this string directly to `ViewModel.SetPropertyValue`, triggering an `ArgumentException` when attempting to assign a `string` to an enum property.
- **Compiled Behavior**: When an alignment property is bound to an enum ViewModel property, the announced string is parsed back into the appropriate enum value and assigned successfully.
- **Rationale**: Prevents crashes on two-way alignment bindings while ensuring the ViewModel reflects the active alignment state.
- **Roslyn Analyzer**: [UIX0025](../general/Analyzers.md#uix0025) flags the type mismatch at compile time.
- **Test Pinning**: Verified by `AlignmentBindingPatternTests` and `BindingLoaderOracle.IntendedDeviations["alignment name written back"]`.

---

### 7. Floating-Point Numbers Written Back to Integers Are Cast

- **XML Behavior**: Certain widget properties (such as `Widget.SuggestedWidth`) announce values as `float`. If two-way bound to an `int` ViewModel property, reflection rejects the narrowing conversion, throwing an `ArgumentException` on each change.
- **Compiled Behavior**: The announced floating-point value is truncated to an integer using an `unchecked` cast, allowing the binding to complete and synchronize bi-directionally.
- **Rationale**: Eliminates continuous runtime exceptions while maintaining intuitive UI synchronization.
- **Roslyn Analyzer**: [UIX0025](../general/Analyzers.md#uix0025) reports narrowing assignments at compile time.
- **Test Pinning**: Verified by `WriteBackTypeTests` and `BindingLoaderOracle.IntendedDeviations["number cast written back"]`.

---

### 8. List Reached Out of Replaced Child Scope Is Not Rebuilt

- **XML Behavior**: Replacing a child ViewModel causes `GauntletView` to refresh all views in its hierarchy. Refreshing a list view destroys and re-instantiates all item widgets. This occurs even when the list is reached via a parent traversal (`{..}`) back to an unchanged collection on the root ViewModel:

  ```xml
  <Widget DataSource="{SigilItem}">
    <Children>
      <Widget DataSource="{..}">
        <Children>
          <!-- Rebuilt from scratch in XML when SigilItem changes -->
          <ListPanel DataSource="{Cultures}" />
        </Children>
      </Widget>
    </Children>
  </Widget>
  ```

  Destroying items unnecessarily resets animations, interrupts active user interactions, and leaks gamepad navigation event subscriptions.
- **Compiled Behavior**: Because the collection at `Root\Cultures` has not changed, the list retains its existing widget instances.
- **Rationale**: Rebuilding unchanged items is redundant and leaks memory.
- **Exemption Status**: **Approved Rule 1 exception without an analyzer**. The pattern is used extensively in Native prefabs (`Lobby.Profile`, `PartyTroopTuple`), and prohibiting it would penalize mod authors copying native layouts.
- **Test Pinning**: Verified by `ListRebuiltThroughParentScopeTests` and `BindingLoaderOracle.IntendedDeviations["list rebuilt through {..}"]`.

---

### 9. List With Path Prefix Sharing Replaced Property Name Is Not Rebuilt

- **XML Behavior**: `GauntletView.OnPropertyChanged` uses `BindingPath.IsRelatedWithPathAsString`, which performs a plain `StartsWith` string check. If a ViewModel replaces property `Player`, a sibling view at `Root\PlayerActions` is erroneously treated as related and rebuilt from scratch.
- **Compiled Behavior**: Only views whose data source paths match the modified scope are refreshed. `PlayerActions` remains untouched.
- **Rationale**: Rebuilding sibling collections due to substring collisions is a clear bug in the XML loader's path comparison logic.
- **Exemption Status**: **Approved Rule 1 exception without an analyzer**. Native prefabs exhibit this naming pattern (e.g., `Lobby.Friends`).
- **Test Pinning**: Verified by `ListRebuiltThroughANamePrefixTests` and `BindingLoaderOracle.IntendedDeviations["list rebuilt through a name prefix"]`.

---

### 10. Disconnected Widgets Do Not Write Back Reset Values

- **XML Behavior**: When widgets are removed from the hierarchy, `EventManager.OnWidgetDisconnectedFromRoot` resets navigation properties (`GamepadNavigationIndex = -1`, `UsedNavigationMovements = None`, `IsUsingNavigation = false`). Because Gauntlet's XML loader does not detach property-changed listeners prior to widget removal, these reset values are written back into the ViewModel, corrupting persistent state when a screen closes or an item leaves a list.
- **Compiled Behavior**: Generated classes unsubscribe two-way binding handlers in `DestroyDataSource` before disconnecting widgets from the visual tree, preventing reset values from polluting the ViewModel.
- **Rationale**: Corrupting ViewModel state upon screen closing or item deletion is an engine bug.
- **Exemption Status**: **Approved Rule 1 exception without an analyzer**. Detecting which widget properties undergo internal engine resets would require hardcoding game-version-specific implementation details into public SDK analyzers.
- **Test Pinning**: Verified by `ResetWrittenBackOnRemovalTests` and `BindingLoaderOracle.IntendedDeviations["reset written back on removal"]`.

---

### 11. Paths Traversing Lists by Index Follow Live Mutations

- **XML Behavior**: A `DataSource` path traversing a list by index (e.g., `{Perks\0\CandidatePerks}`) is only evaluated when the view is initially refreshed. If the underlying collection is modified in place (items added, removed, or sorted), the view fails to re-evaluate the index and remains bound to the previous item or an item that is no longer in the list.
- **Compiled Behavior**: Whenever the collection raises a collection-changed event, the index path is re-evaluated, and the widget rebinds to whichever item currently resides at that index.
- **Rationale**: Remaining bound to a displaced item produces stale or invalid screen data.
- **Roslyn Analyzer**: [UIX0028](../general/Analyzers.md#uix0028) reports indexed list path bindings as compile-time errors.
- **Test Pinning**: Verified by `IndexedDataSourceTests` and `BindingLoaderOracle.IntendedDeviations["indexed item followed"]`.

---

## Non-Deviations

To avoid confusion, the following scenarios are **not** considered deviations:

1. **Compilation Declines**: When the code generator declines a movie (e.g., due to recursive constants, circular prefab roots, or unresolvable properties), the movie runs via the XML loader. Declines represent capability gaps rather than behavioral deviations and are tracked as oracle failures (`OracleOutcome.NotGenerated`).
2. **Missing Resource Constant Arithmetic**: If a constant performs mathematical operations on a missing sprite or brush layer dimension (e.g., `Additive="8"` on a missing sprite), Gauntlet's XML loader throws a `FormatException` and aborts movie loading. Because the presence of sprite resources depends on external user mod configurations that cannot be validated statically, compiled prefabs replicate the XML loader's exception behavior.

---

## Related Documentation

- [Handling Discrepancies from the XML Loader](HandlingDifferences.md): Governance rules, triage procedures, and exemption policies.
- [Code Generator](Generator.md): Generator architecture, data sources, and member resolution.
- [General Analyzers Documentation](../general/Analyzers.md): Index of all `UIX...` diagnostic rules.
