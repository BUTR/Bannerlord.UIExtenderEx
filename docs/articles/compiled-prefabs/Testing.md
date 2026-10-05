# Testing & Verification Strategy

> [!NOTE]
> This article is written for **UIExtenderEx maintainers and core contributors**.
> It documents the automated test suites, test harnesses, oracles, and benchmarking tools used to verify the compiled prefabs pipeline.
> For the pipeline architecture, see [Overview](Overview.md) and [Pipeline](Pipeline.md). For behavioral deviations from XML, see [Deviations from the XML Loader](XmlDeviations.md).

---

## 1. Test Project Structure

Automated testing is divided across specialized test projects located in the `tests/` directory:

| Test Project | Focus Area | External Dependencies |
| :--- | :--- | :--- |
| `Bannerlord.UIExtenderEx.Tests` | Core framework: ViewModel mixins, prefab patching engine, Harmony patches, settings, and runtime switching. | Game assemblies or reference assemblies. |
| `Bannerlord.UIExtenderEx.CodeGenerator.Tests` | Alternative code generator in isolation: tests syntax tree emission, by-name bindings, and snapshot parity against temporary XML files. | None (runs standalone). |
| `Bannerlord.UIExtenderEx.CompiledPrefabs.Tests` | Compilation manager, fingerprint calculations, disk caching, real `UIContext` integration, and past oracle/fuzzer regression tests. | Game UI assemblies. |
| `Bannerlord.UIExtenderEx.Corpus.Tests` | Full-scale oracle comparison over every prefab shipped by the installed game; runs in dedicated multi-process chunks. | Full Mount & Blade II game installation. |
| `Bannerlord.UIExtenderEx.Analyzers.Tests` | Roslyn analyzers, code fixes, and diagnostic targets compiled against .NET Framework 4.7.2. | BUTR game reference packages (no game required). |

### Shared Testing Infrastructure (`Bannerlord.UIExtenderEx.Tests.Shared`)

`Bannerlord.UIExtenderEx.Tests.Shared` is a shared library providing test fixtures and utilities across projects:
- **Test Oracles**: `LoaderOracle` and `BindingLoaderOracle`.
- **Introspection Tools**: `WidgetSnapshot`, `ViewModelSynthesizer`, `OracleMemoryGuard`, and `OracleRetention`.
- **Test Environments**: `PrefabWorkspace`, `TestUIContext`, `CompiledMovie`, and `TestGame`.
- **Runtime Bootstrap**: `TestRuntime.Start`, providing standardized test environment setup for all test assemblies.

---

## 2. Executing Tests

Run the primary test suites via the .NET CLI:

```powershell
foreach ($project in 'Tests', 'CodeGenerator.Tests', 'CompiledPrefabs.Tests', 'Corpus.Tests') {
    dotnet test "tests/Bannerlord.UIExtenderEx.$project/Bannerlord.UIExtenderEx.$project.csproj" `
        --configuration Debug --framework net472 -p:GameFolder="C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
}
```

### Locating Game Assemblies

The build property `GameFolder` defines whether test projects reference local binaries from `bin/Win64_Shipping_Client` or fallback to NuGet reference packages (`Bannerlord.ReferenceAssemblies.Core`).

If not specified explicitly, `GameFolder` resolves in order:
1. Environment variable `BANNERLORD_GAME_DIR`.
2. Windows Steam Registry installation paths.
3. Default Steam library path.

Runtime tests inspect `TestGame.Directory` to verify that `TaleWorlds.GauntletUI.CodeGenerator.dll` exists; if a game installation is absent, game-dependent tests skip automatically (`Assert.Ignore`).

### Other Game Versions

The tests need no full installation: the game's `bin/Win64_Shipping_Client` assemblies, `Version.xml`, the modules' `bin` assemblies and `SubModule.xml`, and the `GUI` folders (about 60 MB per version) are enough. `.github/resources/FileFilters.regexp` lists them for DepotDownloader; point `BANNERLORD_GAME_DIR` and `GameFolder` at a folder holding them to run the suites against that version.

CI downloads them from Steam:

| Workflow | Versions | Build |
| :--- | :--- | :--- |
| `test.yml` (pushes, pull requests) | Current stable and beta | Stable also builds against its own reference package; beta builds as it ships, since its reference package is a prerelease |
| `test-fully.yml` (weekly, or by hand) | Every version from `GameVersion` in `build/common.props` to the current beta | As it ships, against the oldest supported version |

`.github/resources/GameVersions.ps1` reads the versions and the Steam branch serving each from the build registry of Bannerlord.ReferenceAssemblies. A version whose branch is gone (a beta that moved on) cannot be downloaded and is not tested. Each job checks that `Version.xml` reports the version it asked for, because DepotDownloader falls back to the public branch for one that does not exist.

### Multi-Target Framework Validation (`net472` and `net6.0`)

Mount & Blade II: Bannerlord runs on two distinct runtime environments in production:
1. **Steam / GOG / Epic Games**: .NET Framework 4.7.2 (`net472`).
2. **Microsoft Store / Xbox App (GDK)**: .NET 6 (`net6.0`).

All changes must pass automated test suites on **both** target frameworks.

#### Platform-Specific Test Considerations

- **Detour Initialization on .NET 6**: On `net6.0`, `AccessTools2.FieldRefAccess` requires MonoMod's detour runtime to be initialized before static field references can resolve. `TestRuntime.Start` initializes a temporary `Harmony` instance in an assembly-level `[SetUpFixture]` before running tests.
- **SubModule Emulation**: The test runner does not load game SubModules automatically. `TestRuntime.Start` explicitly initializes the runtimes in declared order (`XmlPrefabRuntime.Install`, `GamePrefabRuntime.Install`, `CompiledPrefabRuntime.Install`) and executes `UIExtender`'s static constructor.
- **Harmony Versioning**: Production builds compile against Harmony 2.3.3+, but test suites run against the version bundled with the game (Harmony 2.4.2+). Earlier versions (2.2.2) suffered from an internal CLR bug (`0x80131506`) under .NET 6 JIT compilation; running against modern Harmony ensures reliable execution across both targets.

---

## 3. Test Harnesses & Shared Infrastructure

| Harness / Utility | Purpose in Test Scenarios |
| :--- | :--- |
| `PrefabWorkspace` | Creates an isolated temporary directory containing test prefab XML, an active `ResourceDepot`, a `WidgetFactory` with `PrefabDatabindingExtension`, and a `PrefabLease` tracking live prefab usage. |
| `TestUIContext` | Initializes a minimal, headless `UIContext` capable of instantiating and measuring widgets without launching the game engine graphics pipeline. |
| `TestGame` | Discovers and validates a local game installation, loading reference assemblies and Native resources. |
| `GameInstallation` | Corpus test utility that loads Native UI assets (prefabs, brushes, fonts, sprite data, and widget types). |
| `CompiledMovie` | High-level facade that compiles a movie via `RoslynCompiler`, loads the assembly into memory, registers it with Gauntlet, and provides helper methods (`SetDataSource`, `ById`, `FireEvent`) to drive interactive tests. |

---

## 4. Test Suite Taxonomy

### Generator Syntax & Verified Snapshots (`CodeGenerator.Tests`)

Tests in this suite assert that the emitted C# source code is syntactically correct, strictly qualified with `global::`, and structurally sound:
- **Verified Snapshots**: Several suites use [Verify](https://github.com/VerifyTests/Verify) (`Snapshots/*.verified.cs`). Any change in emitted code produces a `.received.cs` diff file for maintainer review. In unattended test runs, `DiffEngine_Disabled=true` suppresses GUI diff tool popups while flagging unintended regressions.

### Loader Semantics (`LoaderSemanticsTests`)

Pins Gauntlet's runtime XML loader contracts directly against TaleWorlds assemblies:
- Verifies that missing scalar bindings assign widget property defaults.
- Verifies that `SetPropertyValue` behaves as a no-op without public setters.
- Verifies collection traversal semantics over `IMBBindingList`.
- Ensures that if TaleWorlds modifies an engine rule in an update, tests fail here immediately rather than causing silent code generator divergence.

### Dynamic Member & Mixin Runtime

Validates by-name binding dispatches, mixin method lookups, and mixin shadowing against real ViewModel instances:
- Tests instances sharing types but holding differing mixin registrations.
- Tests dynamic lookups where a mixin member overrides an existing ViewModel property.

### End-to-End Equivalence (`GauntletMovie.Load`)

Executes test prefabs through `GauntletMovie.Load` in parallel—instantiating an XML movie, a compiled movie, and a control movie:
- Validates all documented exceptions in [Deviations from the XML Loader](XmlDeviations.md).
- Confirms two-way data updates, property change notifications, and command invocations.

### Manager & Fingerprint Lifecycle

Validates `CompiledPrefabManager` against test fakes:
- Confirms state transitions, fingerprint invalidation triggers, and snapshot pinning leases.
- Verifies cache archive serialization (`CompiledPrefabs.zip`) and graceful degradation when the compiler assembly is missing (`CompilerMissingTests`).

### Acceptance against TaleWorlds Generator (`GameGeneratorEquivalenceTests`)

Executes TaleWorlds' `TaleWorlds.GauntletUI.CodeGenerator` and UIExtenderEx's code generator against all unpatched Native prefabs:
- Compares emitted source code character-by-character after applying known architectural deviations (`IntendedDeviations`).
- Ensures that for standard prefabs with no mixins, the output matches TaleWorlds' emission.

---

## 5. Automated Test Oracles: Proving XML Parity

Rather than relying solely on manually written test assertions, UIExtenderEx uses **test oracles** where the Gauntlet runtime XML loader serves as the ground truth.

```
                    ┌────────────────────────────┐
                    │      Input Prefab XML      │
                    └─────────────┬──────────────┘
                                  │
                  ┌───────────────┴───────────────┐
                  ▼                               ▼
       ┌─────────────────────┐         ┌─────────────────────┐
       │     XML Loader      │         │   Compiled Prefab   │
       │  (Ground Truth)     │         │      (Candidate)    │
       └──────────┬──────────┘         └──────────┬──────────┘
                  │                               │
                  └───────────────┬───────────────┘
                                  ▼
                   ┌─────────────────────────────┐
                   │   Oracle Comparison Engine  │
                   │ (Properties, Fields, Calls) │
                   └─────────────────────────────┘
```

### 1. Static Oracle (`LoaderOracle`)

Tests prefabs without active ViewModels:
- Instantiates the prefab via both `WidgetPrefab.Instantiate` and the compiled class.
- Traverses both widget hierarchies using `WidgetSnapshot`:
  - Compares every public property to a depth of two nested objects.
  - Compares every private and public field down to the base `Widget` class (excluding dynamic event subscriber tables).
  - Validates all calls made to `Debug.FailedAssert`, matching exact diagnostic messages.
- A second XML instance is built in parallel to detect non-deterministic widget properties (such as widgets generating random IDs in constructors).

### 2. Dynamic Binding Oracle (`BindingLoaderOracle`)

Tests complete databinding lifecycles:
1. Analyzes the prefab's binding expressions and uses `ViewModelSynthesizer` to synthesize an in-memory ViewModel matching the required properties, lists, and commands.
2. Loads three movies simultaneously: the candidate compiled movie, an XML movie, and a second XML control movie.
3. Drives all three instances through standardized stress cycles:
   - Initial layout initialization.
   - Mutating every property twice.
   - Replacing nested child ViewModels.
   - Adding, inserting, removing, and clearing list collections.
   - Firing all bound commands.
   - Disconnecting the movies from the visual tree.
4. After every step, verifies that widget properties, ViewModel invocation logs, and assertion counts match across all instances.

---

## 6. Oracle Performance & Memory Management

Running the dynamic binding oracle over every Native prefab (several hundred) loads hundreds of dynamic assemblies that cannot be unloaded by .NET Framework 4.7.2. Without strict memory management, an unconstrained run can consume upwards of 40 GB of RAM and crash the test host.

The test suite enforces several safeguards:

- **Process Chunking (`OracleChunks`)**: Prefabs are executed in isolated worker processes that terminate upon completion, freeing unmanaged and JIT resources.
- **Memory Ceiling Guard (`OracleMemoryGuard`)**: An asynchronous watchdog monitors process memory against `UIEXTENDEREX_ORACLE_MAX_MEMORY_MB` (default 3 GB). If memory exceeds the threshold, the chunk process terminates cleanly and restarts with a smaller batch size.
- **Isolated UI Contexts**: Each test prefab runs in an isolated `UIContext`. Shared contexts leak memory through `EventManager` event listener tables (`EventManagerPatch`).
- **Chunk Partitioning**:
  - The static corpus runs across three parallel processes (`UIEXTENDEREX_ORACLE_PARALLEL=3`).
  - The binding corpus runs in batches of 40 prefabs across two parallel worker processes that share the memory ceiling (see the rationale in `OracleChunks.DefaultParallelism`).

### Running Corpus Tests Locally

To run the full corpus or fuzzer locally:

```powershell
# Run the static corpus over all official prefabs
dotnet test "tests/Bannerlord.UIExtenderEx.Corpus.Tests/Bannerlord.UIExtenderEx.Corpus.Tests.csproj" `
    --configuration Release --framework net472 -p:GameFolder="<path to game>"

# Run the binding oracle over a specific subset of prefabs
$env:UIEXTENDEREX_ORACLE_PREFABS="Inventory,ClanScreen,CharacterDeveloper"
dotnet test ... --filter FullyQualifiedName~EveryOfficialPrefab_BindsTheSameCompiledAsFromXml
```

Reports detailing every tested prefab and detected discrepancy are written to `%TEMP%/UIExtenderEx-oracle/`.

Diagnostic environment variables for investigating test failures (set each to `1`):

| Variable | Effect |
| :--- | :--- |
| `UIEXTENDEREX_ORACLE_DUMP_SOURCES` | Exports generated C# files for each prefab to `%TEMP%/UIExtenderEx-oracle/` (including synthesized ViewModels when using the binding oracle). |
| `UIEXTENDEREX_ORACLE_DUMP_VIEWS` | Binding oracle: dumps the XML movie's `GauntletView` visual tree upon opening. |
| `UIEXTENDEREX_ORACLE_DUMP_LOGS` | Binding oracle: logs ViewModel member invocations immediately surrounding the point where XML and compiled execution diverge. |
| `UIEXTENDEREX_ORACLE_MEMORY` | Binding oracle: tracks live memory, working set, and loaded assembly counts at each phase. (Forces garbage collection cycles; reduces test throughput). |
| `UIEXTENDEREX_ORACLE_TRACE` | Writes execution steps sequentially to `trace-<pid>.txt` to capture diagnostics before fatal process crashes (such as stack overflows). |

`UIEXTENDEREX_ORACLE_PARALLEL` and `UIEXTENDEREX_ORACLE_CHUNK` override process concurrency and batch chunk size, respectively.

---

## 7. Performance Benchmarks

Benchmark suites are marked as `[Explicit]` and measure execution throughput:

```powershell
dotnet test "tests/Bannerlord.UIExtenderEx.CompiledPrefabs.Tests/Bannerlord.UIExtenderEx.CompiledPrefabs.Tests.csproj" `
    --configuration Release --framework net472 --filter FullyQualifiedName~CompiledVersusXmlBenchmarks
```

| Benchmark Suite | What It Measures |
| :--- | :--- |
| `CompiledVersusXmlBenchmarks` | End-to-end rendering speed comparing XML loading, compiled loading with direct typed bindings, and compiled loading with dynamic by-name fallbacks. |
| `PrefabLoadBenchmarks` | Evaluates decision-path overhead in `GauntletMoviePatch`: validating cached sections, computing fingerprints, and building reference sets. |
| `DynamicMemberBenchmarks` | Nanosecond-level timing of individual reflection reads, writes, and mixin receiver dispatches. |

---

## 8. Authoring Workflow: Adding Tests for Code Generation Changes

When introducing new code generation features or fixing emission bugs, follow this checklist:

1. **Define Test Fixtures**: Add sample ViewModel and widget types to `FastPathVMs.cs` and `FastPathWidgets.cs` (or create a dedicated fixture in `Bannerlord.UIExtenderEx.Tests.Shared`).
2. **Assert Emitted Source Code**: Create a unit test in `CodeGenerator.Tests` using `PrefabWorkspace.Generate` to verify that the emitted C# source matches expectations.
3. **Verify Interactive Behavior**: Create an end-to-end integration test using `CompiledMovie.Build`. Instantiate the movie, attach the ViewModel, mutate properties, fire events, and assert widget values.
4. **Validate Equivalence**: If the change affects standard bindings, run `GameGeneratorEquivalenceTests`. If the deviation is intentional, document it in `IntendedDeviations` with technical rationale.
5. **Update Fingerprints**: If the change adds new compile-time dependencies, update `PrefabFingerprint.Compose` and add a test case to `FingerprintInvalidationTests`.

---

## 9. In-Game Verification

To verify compiled prefabs in a live game session:

1. **Check Generation Reset**: Deploying a new build updates the generation hash in `cache.txt`. On initial screen opening, all movies recompile once in the background.
2. **Verify Cache Stability**: Re-opening the same screen, or restarting the game with the same mod build, must show `0` recompilations. `CompiledPrefabs.zip` must remain unchanged.
3. **Inspect Failure Logs**: The folder `Modules/Bannerlord.UIExtenderEx/CompiledPrefabs/Failed/` must remain empty.
4. **Dump Generated Source Code**: Set `DumpGeneratedCode="true"` in `SubModule.xml` to inspect the generated C# files under `CompiledPrefabs/Sources/<Movie>/<ViewModel>/`.

---

## Related Documentation

- [Pipeline](Pipeline.md): Compilation lifecycle, background workers, and caching.
- [Code Generator](Generator.md): Emission rules, mixin bindings, and dynamic fallbacks.
- [Deviations from the XML Loader](XmlDeviations.md): Documented differences between XML and compiled execution.
