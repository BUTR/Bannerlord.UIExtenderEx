# Handling Discrepancies from the XML Loader

> [!NOTE]
> This article is written for **UIExtenderEx maintainers and core contributors**.
> It defines the governance rules and triage process for addressing behavioral differences between compiled C# prefabs and Gauntlet's runtime XML loader.
> For the authoritative catalog of active exceptions, see [Deviations from the XML Loader](XmlDeviations.md).

---

## 1. The Core Triage Principles

Compiled prefabs must replicate the behavior of Gauntlet UI's runtime XML loader as closely as possible. When a discrepancy is discovered—whether through automated test oracles, the prefab fuzzer, code review, or user bug reports—it is evaluated against three hierarchical rules:

```
[Discrepancy Discovered]
        │
        ▼
   Does XML fail?
   ├── No ──────────► Rule 3: Replicate XML behavior. (Optional: emit analyzer warning).
   └── Yes
        │
        ▼
   Can a Roslyn analyzer statically prevent it in user mods?
   ├── Yes ─────────► Rule 1: Fix in compiled prefab + emit compile-time analyzer error.
   └── No ──────────► Rule 2: Replicate XML failure (ensures fallback compatibility).
```

### Rule 1: Correct with Analyzer Guard
*Where the XML loader fails, and that failure is demonstrably incorrect for a UI framework, the compiled prefab executes the correct behavior, **provided that an accompanying Roslyn analyzer rule flags the construct at compile time**.*

Both conditions are mandatory:
- The analyzer ensures that any mod compiled against modern UIExtenderEx analyzers will never ship the problematic construct, guaranteeing the mod behaves identically in XML and compiled modes.
- Unupdated mods running compiled prefabs benefit from the corrected, stable behavior rather than experiencing unexpected crashes or dropped values.

### Rule 2: XML Parity as the Safe Fallback
*Where compile-time static analysis cannot guarantee detection, the compiled prefab must replicate the XML loader's behavior, including its failures.*

Because any movie can fall back to the XML loader at runtime (e.g., if compiled prefabs are disabled, an environment error occurs, or generation fails), a mod that succeeds in compiled mode but crashes in XML mode creates an unacceptable runtime failure for end users.

### Rule 3: Never Stricter than XML
*The compiled prefab must never be stricter than the XML loader.*

If a construct functions under the XML loader, existing mods may already rely on it in production. Compiled prefabs must continue supporting it at runtime. If the pattern is undesirable or error-prone, a Roslyn analyzer warning should guide developers away from it in new code without breaking existing deployments.

---

## 2. Decision Framework

When evaluating a newly reported behavioral difference, work through the following questions:

### 1. Does the XML loader fail?
- Does Gauntlet throw an exception, refuse to open the screen, or silently discard values?
- If XML succeeds, **Rule 3 applies**: The compiled prefab must produce the same result as XML. If the construct is problematic, consider an informational analyzer diagnostic.

### 2. Is the failure a bug in Gauntlet, or an error in the prefab?
- An unhandled exception triggered by a widget's property setter during normal databinding is a framework flaw; a screen should not fail to open over a single non-critical binding error.
- Conversely, silently substituting an invalid value with an arbitrary fallback is not a "fix." Any deviation under Rule 1 must represent the unambiguously correct UI behavior.

### 3. Can a Roslyn analyzer reliably detect it at compile time?
- Static detection is feasible when the construct is visible within the mod's own prefab XML and ViewModel classes, or against the reference assembly metadata provided by BUTR's game packages (e.g., `Bannerlord.ReferenceAssemblies`).
- Static detection is **not** feasible if the behavior depends on dynamic runtime values, external mods, texture packs, or reflection. If compile-time detection cannot be guaranteed, **Rule 2 applies** (replicate XML behavior).

### 4. Is declining code generation justified?
- A movie that declines code generation falls back to XML. While the player experiences slower XML loading, functionality is preserved.
- However, **a decline is considered a defect, not a pass**. If the XML loader can parse and render a prefab, the code generator is expected to support it. Test oracles flag unhandled declines as failures (`OracleOutcome.NotGenerated`).
- A permanent decline must be explicitly approved by maintainers, documented in `OracleDeclines.Intended`, and covered by automated regression tests.

---

## 3. Protocol for Adopting an Intentional Deviation

When maintainers approve an intentional deviation under Rule 1, the following steps must be completed:

1. **Maintainer Approval**: Document the technical rationale for diverging from the XML loader.
2. **Regression Testing**: Write an integration test pinning both behaviors:
   - Verifying what the XML loader does.
   - Verifying what the compiled prefab does instead.
3. **Oracle Exemption**: Register the discrepancy in the test oracles (e.g., `BindingLoaderOracle.IntendedDeviations` or `LoaderOracle.IgnoredProperties`) so automated test runs remain green.
4. **Documentation**: Add a detailed entry in [Deviations from the XML Loader](XmlDeviations.md), citing the corresponding Roslyn analyzer diagnostic ID (`UIX...`).
5. **Analyzer Implementation**: Implement and publish the diagnostic rule in `Bannerlord.UIExtenderEx.Analyzers`.

### Documented Exceptions to the Analyzer Requirement

Maintainers may permit an intentional deviation without a corresponding analyzer rule under two narrowly defined exceptions:

1. **Benign Local State Parity**: The discrepancy involves internal widget-local state that is never exposed to ViewModels or databinding, and the construct is present throughout TaleWorlds' own native prefabs:
   - *Example*: [A list reached back out of a replaced child is not rebuilt](XmlDeviations.md#8-list-reached-out-of-replaced-child-scope-is-not-rebuilt)
   - *Example*: [A list whose path begins with a replaced property's name is not rebuilt](XmlDeviations.md#9-list-with-path-prefix-sharing-replaced-property-name-is-not-rebuilt)
2. **Unanalyzable Engine Defects**: The compiled prefab implements the correct behavior, the XML loader's result is caused by an internal Gauntlet bug, and writing a static analyzer rule would require hardcoding game-version-specific engine implementation quirks that do not belong in public SDK analyzers:
   - *Example*: [A widget leaving the tree writes nothing back](XmlDeviations.md#10-disconnected-widgets-do-not-write-back-reset-values)

---

## Related Documentation

- [Deviations from the XML Loader](XmlDeviations.md): Authoritative catalog of all intentional differences and analyzer diagnostics.
- [Code Generator](Generator.md): Generator architecture, data sources, and member resolution.
- [Testing](Testing.md): Automated verification, test oracles, and corpus test suites.
