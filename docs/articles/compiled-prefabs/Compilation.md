# In-Memory Compilation & Roslyn Integration

> [!NOTE]
> This article is written for **UIExtenderEx maintainers and core contributors**.
> It explains how UIExtenderEx bundles, isolates, and executes the Roslyn C# compiler at runtime to compile generated prefab widgets.
> For the compilation pipeline lifecycle, see [Pipeline](Pipeline.md). For how the alternative code generator emits C# source code, see [Code Generator](Generator.md).

---

## 1. Overview

UIExtenderEx embeds an in-memory C# compilation engine directly within the module. The Roslyn compiler (`Microsoft.CodeAnalysis.CSharp`) and all required support libraries are merged into `Bannerlord.UIExtenderEx.Compiler` using **ILRepack** with full type internalization.

This design achieves several critical architectural goals:
- **Zero Namespace / Assembly Collisions**: Internalizing Roslyn prevents version conflicts with other mods, tools (such as ButterLib), or the game engine.
- **Standalone Autonomy**: Compiles C# in-memory without requiring the .NET SDK, external compiler executables (`csc.exe`), or third-party framework dependencies.
- **Graceful Fault Tolerance**: The compiled runtime references the compiler assembly strictly through abstract interfaces (`ICSharpCompiler`). If `Bannerlord.UIExtenderEx.Compiler.dll` is missing or quarantined by antivirus software, the runtime fails gracefully, and patched movies automatically route through the native XML loader without crashing the game ([Overview](Overview.md#runtime-dependencies--isolation)).

---

## 2. Dedicated Compiler Project Architecture

The compiler implementation is housed in an independent project (`Bannerlord.UIExtenderEx.Compiler`) rather than integrated directly into the compiled runtime assembly.

### The Optimization Invariant

ILRepack merges dependencies into the target host assembly, and the merged code inherits the host assembly's `DebuggableAttribute`. When Roslyn was previously embedded directly inside `CompiledPrefabs`, compiling a Debug build forced Roslyn to run with optimizations disabled (`DisableOptimizations`), resulting in a **3× performance degradation** (e.g., compiling `ClanScreen` took 2.4 seconds instead of 0.9 seconds).

By isolating the compiler in a dedicated project:
- The project file enforces `<Optimize>true</Optimize>` across **all** build configurations (including Debug).
- Roslyn always executes with full JIT compiler optimizations.
- Maintainers can build and debug the rest of the UIExtenderEx solution with standard debugging symbols and unoptimized stepping.

### Build Configuration & Merge Safety

- **Comprehensive Configuration Targets**: The ILRepack build property `ILRepackTargetConfigurations` is configured as `$(Configuration)`, ensuring the merge runs across all release channels (`Debug`, `Release`, `Stable_Release`, `Beta_Release`).
- **Intermediate Assembly Cleanup**: ILRepack merges output back into `@(IntermediateAssembly)` in the `obj/` folder. Without intervention, MSBuild sees the merged binary as newer than source files, skipping subsequent compilations and causing ILRepack to recursively merge Roslyn into its own output (ballooning file size from 12 MB to 35 MB over successive builds). The MSBuild target `RecompileBeforeILRepack` purges `@(IntermediateAssembly)` prior to `CoreCompile`, preventing recursive bloat.
- **Bundled Roslyn Version**: The project bundles Roslyn version **5.9.0** (`RoslynVersion` in `build/common.props`), which keeps the binary footprint at ~12 MB. For compile times, see [Measured Compile Times](#measured-compile-times).

---

## 3. The Rationale for Bundling Roslyn

Roslyn depends on tightly coupled pairs of foundational libraries: `System.Collections.Immutable` and `System.Reflection.Metadata`.

Each release of `System.Reflection.Metadata` is compiled against an exact version of `System.Collections.Immutable` and exchanges generic `ImmutableArray<T>` instances across assembly boundaries. In a modded game environment, different components ship conflicting versions of these assemblies:

| Environment / Mod Component | `System.Collections.Immutable` | `System.Reflection.Metadata` |
| :--- | :--- | :--- |
| **Game Engine** (`mono/lib/mono/4.5`) | `1.2.3.0` | `1.4.3.0` |
| **ButterLib / Community Frameworks** | `1.2.5.0` | `1.4.5.0` |

### The Boundary Mismatch Problem

When both versions exist in the same process, .NET's assembly resolution can load `System.Reflection.Metadata` from one source and `System.Collections.Immutable` from another. Because the CLR treats `ImmutableArray<T>` from different assembly versions as distinct, incompatible types, any call across the assembly seam fails immediately with a `MissingMethodException`.

Furthermore, multiple competing `AppDomain.AssemblyResolve` handlers (TaleWorlds' `AssemblyLoader`, BLSE's `AssemblyResolverFeature`, and mod-specific resolvers) probe directories by simple assembly name without version checks, making resolution order dependent on arbitrary module load ordering.

**Solution**: Merging Roslyn and internalizing all its types within `Bannerlord.UIExtenderEx.Compiler` eliminates external assembly binding entirely. Roslyn always binds to its own private, co-packaged copies of `System.Collections.Immutable` and `System.Reflection.Metadata`.

Auxiliary components that inspect metadata directly (`PrefabAssemblyReference` and `ReferencePublicizer`) reside within this same assembly to leverage the internalized metadata reader.

### Alternatives Not Taken

Bundling Roslyn directly replaced loading the game's native Roslyn 3.3 from `bin/Win64_Shipping_Client/mono/lib/mono/4.5`. Several alternative approaches were evaluated and rejected:

- **Using the game's Roslyn with a custom assembly resolver:** UIExtenderEx's `AssemblyResolve` handler is registered after BLSE's. Because BLSE resolves assemblies by simple name from the first module `bin/` folder in load order without comparing versions, any mod shipping either `System.Collections.Immutable` or `System.Reflection.Metadata` dictates which version Roslyn receives. Preloading the game's copies with `Assembly.LoadFrom` does not prevent this, as assemblies loaded that way probe their own directory first and can pull in a mismatched copy of the other library.
- **Out-of-process compilation via `csc.exe` or `VBCSCompiler.exe`:** The game ships compiler executables configured with redirects for the dependency pair, but `csc` lacks an `IgnoreAccessibility` option. Consequently, every reference would require disk-persisted member-level publicizing rather than the fast in-memory `TypeDef` patching performed by `ReferencePublicizer`. Furthermore, an external compiler process would require lifecycle monitoring and timeout handling. Bundling Roslyn adds ~12 MB to disk with zero runtime overhead (the binary is memory-mapped and warmed up asynchronously).
- **Isolated `AppDomain` (.NET Framework) or `AssemblyLoadContext` (.NET 6):** This would require maintaining separate isolation logic for each runtime target. Furthermore, `AppDomain` is obsolete on modern .NET, and fingerprint metadata extraction on the main thread would remain outside the isolated domain.
- **Maintaining a secondary compiler fallback:** A previous `csc` fallback silently masked complete Roslyn failures. For example, when another mod shipped a conflicting `System.Numerics.Vectors`, Roslyn failed on `Vector2` (CS0012), and `csc` compiled the templates without logging the underlying error while limiting generated code to C# 5. Standardizing on a single compiler ensures errors are surfaced cleanly while allowing movies to fall back to XML (the `Vector2` collision is now explicitly handled by `PrefabReferenceSet.DropDuplicateVector2`).

---

## 4. Packaging: Merged vs. Excluded Dependencies

The ILRepack task packages the compiler's output directory, internalizing all dependencies while applying specific exclusions:

| Classification | Assemblies | Rationale |
| :--- | :--- | :--- |
| **Merged & Internalized** | `Microsoft.CodeAnalysis`<br/>`Microsoft.CodeAnalysis.CSharp`<br/>`System.Collections.Immutable`<br/>`System.Reflection.Metadata`<br/>`System.Runtime.CompilerServices.Unsafe`<br/>`System.Text.Encoding.CodePages`<br/>*(On net472 also: `System.Memory`, `System.Buffers`, `System.Threading.Tasks.Extensions`)* | Isolates the complete compilation engine from the external environment. |
| **Excluded from Merge & Distribution** | `System.Numerics.Vectors` | Excluded to preserve hardware SIMD acceleration (see below). |
| **Excluded Resources** | `<culture>/Microsoft.CodeAnalysis*.resources.dll` | Satellite localized assemblies are omitted; compiler diagnostic messages are emitted in English. |

### Preserving SIMD Hardware Acceleration (`System.Numerics.Vectors`)

`System.Numerics.Vectors` is explicitly excluded from the merge via `ExcludeAssembliesFromILRepack`.

**Technical Rationale**: The .NET runtime JIT compiler only treats `Vector<T>` as a hardware-accelerated SIMD type when it resides in an assembly named `System.Numerics.Vectors`. When merged into another assembly:
- `Vector.IsHardwareAccelerated` evaluates to `false`.
- `Vector<byte>.Count` drops from 32 to 16.
- SIMD-accelerated memory utilities (such as `Span<byte>.IndexOf` used extensively by `System.Memory` and `System.Reflection.Metadata`) run **5× slower**.

`System.Numerics.Vectors` is referenced as a compile-only asset (`ExcludeAssets="runtime"`). At runtime, the assembly binds directly to the copy shipped in Mount & Blade II: Bannerlord's base directory.

### Dynamic Binding Redirects (`VectorsBinding`)

On `.NET Framework 4.7.2` (`net472`), strong-name assembly binding requires exact version matching:
- `System.Memory` on NuGet requests `System.Numerics.Vectors 4.1.6.0`.
- Mount & Blade II: Bannerlord ships `System.Numerics.Vectors 4.1.3.0`.
- Game modules cannot register XML binding redirects (`app.config`) with the host process.

`VectorsBinding` solves this on `net472` via a programmatic module initializer (`ModuleInitializerAttribute`):
1. Subscribes to `AppDomain.CurrentDomain.AssemblyResolve` before any method requiring `Vector<T>` is JIT-compiled.
2. Intercepts failed binding requests for `System.Numerics.Vectors`.
3. Resolves the request using the highest version already loaded in the process, or loads the game's file from the application root by simple name.
4. Because the assembly retains its genuine identity, the JIT continues to vectorize operations correctly.

---

## 5. In-Memory Compilation Options

Compilation is executed by `RoslynCompiler.Compile` using configured options:

```csharp
var options = new CSharpCompilationOptions(
    OutputKind.DynamicallyLinkedLibrary,
    optimizationLevel: OptimizationLevel.Release,
    deterministic: true)
    .WithMetadataImportOptions(MetadataImportOptions.All);

IgnoreAccessibility(options); // Enables TopLevelBinderFlags = 0x400000 via reflection
```

### Configuration Details

- **Deterministic Builds (`deterministic: true`)**: Given identical source code and reference sets, the compiler produces bit-for-bit identical binary output and matching MVIDs.
- **Modern Language Parsing**: Syntax trees are parsed using `LanguageVersion.Latest`.
- **Importing Non-Public Metadata (`MetadataImportOptions.All`)**: Instructs Roslyn to load all internal and private type declarations from reference assemblies.
- **Suppressing Binder Accessibility Checks (`IgnoreAccessibility`)**: Uses reflection to set the internal Roslyn binder flag `TopLevelBinderFlags = 0x400000`. This enables the compiler to reference and bind against `internal` and `private` members across referenced assemblies.
- **In-Memory Emission**: Emits raw PE assembly bytes directly to a memory stream without generating PDB files.

### Diagnostic Triage

When compilation fails, warnings are suppressed, and only diagnostics of severity `DiagnosticSeverity.Error` are retained. `CompiledPrefabManager` prefixes diagnostics with `Roslyn (bundled)`, writes the full error report to `Failed/<Movie>/<ViewModel>/errors.txt`, and logs the first five diagnostics to the trace log.

---

## 6. Accessing Internal Types & Publicization

Mod ViewModels and mixin classes are frequently declared as `internal`. While the Roslyn binder flag `IgnoreAccessibility` permits member access inside method bodies, C# forbids `internal` types from appearing in public signatures or type declarations (e.g., class inheritance, field types, or parameter signatures).

To support `internal` types without triggering compilation errors, UIExtenderEx employs a dual-stage strategy:

```
[Reference Assembly on Disk]
           │
           ├─► 1. ReferencePublicizer (In-Memory PE Patch)
           │      Rewrites TypeDef visibility flags to public.
           │      Passed to Roslyn as MetadataReference.
           │
           └─► 2. IgnoresAccessChecksSource (C# Source Emission)
                  Emits [assembly: IgnoresAccessChecksTo("<Name>")]
                  Bypasses CLR runtime accessibility checks upon execution.
```

### 1. In-Memory Reference Publicization (`ReferencePublicizer`)

For every non-framework assembly passed to the compiler, `ReferencePublicizer` creates an in-memory publicized copy:
- Inspects the assembly's ECMA-335 CLI metadata tables directly via `System.Reflection.Metadata`.
- Patches the `TypeDef` table flags: top-level types are rewritten to `TypeAttributes.Public`, and nested types to `TypeAttributes.NestedPublic`.
- Member accessibility is left untouched (handled by Roslyn's `IgnoreAccessibility`).
- Framework assemblies (`mscorlib`, `netstandard`, `System.*`, `Microsoft.*`) are skipped.
- The publicized image is passed to Roslyn as an in-memory `MetadataReference.CreateFromImage(bytes, filePath: realPath)`.

### 2. Runtime Access Check Suppression (`IgnoresAccessChecksSource`)

Publicized metadata satisfies Roslyn during compile time, but at runtime the CLR will enforce access boundaries when the compiled code references `internal` members in unpublicized mod assemblies.

To satisfy the CLR, `IgnoresAccessChecksSource` appends `IgnoresAccessChecks.gen.cs` to every compilation unit:
- Declares `System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute`.
- Emits an attribute declaration for each referenced assembly:
  ```csharp
  [assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("TargetModAssembly")]
  ```
- The CLR runtime honors this attribute, allowing generated assemblies to access internal members of target assemblies without reflection.

### Reference Metadata Caching (`References.Cache`)

Parsing assembly metadata into Roslyn `MetadataReference` instances is computationally expensive. `References.Cache` maintains a static cache of `MetadataReference` instances keyed by file path, length, and last-write timestamp:
- Warm-up compilation against the broad reference set pre-populates this cache.
- If a mod DLL is recompiled or overwritten while the game is running, the timestamp check detects the modification and automatically invalidates the cached reference.

---

## 7. Assembly Loading & Gauntlet UI Integration

Once Roslyn successfully emits PE assembly bytes, the assembly must be integrated into the running game:

### Measured Compile Times

From one in-game session on 2026-09-20, after warm-up. "Main thread" is the preparation done before the job goes to the worker; the movie opens through XML meanwhile.

| Movie | Generated C# | Main thread | Roslyn | Load |
| :--- | ---: | ---: | ---: | ---: |
| KingdomManagement | 10,798 KB | 335 ms | 1,592 ms | 153 ms |
| ClanScreen | 8,357 KB | 427 ms | 1,369 ms | 206 ms |
| Inventory | 4,490 KB | 235 ms | 830 ms | 266 ms |
| CharacterDeveloper | 1,958 KB | 132 ms | 530 ms | 239 ms |
| TutorialScreen | 244 KB | 55 ms | 65 ms | 116 ms |

The Roslyn warm-up at startup took 2,040 ms in the same session.

### Off-Thread Assembly Loading

Compiled assemblies are loaded via `Assembly.Load(byte[])` **on a background worker thread** via `GameCompiledPrefabEnvironment.LoadAssembly`.

**Performance Rationale**: In modded environments, many mods install `AppDomain.CurrentDomain.AssemblyLoad` event listeners. Measured in a modded game, `Assembly.Load` took **90–210 ms** with third-party listener processing, compared to 1–15 ms for the load itself. Executing the load on worker threads prevents UI frame drops during gameplay.

Loading from a byte array also ensures:
- `Assembly.Location` is empty, ensuring dynamic prefab assemblies are never mistakenly identified as external dependency files.
- No file locks are held on disk, allowing cache archives to be updated and reorganized freely.

### Gauntlet Widget Table Registration (`WidgetInfoTable`)

Gauntlet UI maintains a static lookup table of all known widget types:

```csharp
WidgetInfo.AddWidgetType(Type widgetType)
```

The game populates this table once at startup. If a UI screen attempts to instantiate a `Widget` subclass defined in an assembly that was loaded later, Gauntlet throws a `KeyNotFoundException`.

To ensure compiled widgets can be instantiated:
1. `WidgetInfoTable.Add` reflects into Gauntlet's private `_widgetInfos` dictionary.
2. Directly inserts a `new WidgetInfo(type)` for every `Widget` subclass defined in the compiled assembly.
3. If the internal table is not yet initialized or cannot be reached via reflection, it falls back to Gauntlet's native `WidgetInfo.Refresh` method.

---

## Related Documentation

- [Overview](Overview.md): High-level architecture, runtime split, and dependencies.
- [Pipeline](Pipeline.md): Compilation pipeline lifecycle, fingerprinting, and cache mechanics.
- [Code Generator](Generator.md): Source code emission rules and databinding generation.
- [Testing](Testing.md): Automated verification suites and compiler test harnesses.
