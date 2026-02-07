# Copilot Instructions for DNNE

DNNE (Dotnet Native Exports) enables .NET managed assemblies to expose native exports consumable by native code. It generates C or Rust source from assemblies marked with `[UnmanagedCallersOnly]` and compiles them into platform-specific native binaries (`.dll`, `.so`, `.dylib`).

## Build & Test

```bash
# Build the NuGet package (includes all components)
dotnet build src/create_package.proj

# Run all unit tests
dotnet test test/DNNE.UnitTests

# Run a single test by name
dotnet test test/DNNE.UnitTests --filter "FullyQualifiedName~TestMethodName"

# Test with alternative compilers (useful for cross-compiler validation)
dotnet test test/DNNE.UnitTests -p:BuildWithGCC=true
dotnet test test/DNNE.UnitTests -p:BuildAsCPPWithMSVC=true
dotnet test test/DNNE.UnitTests -p:BuildWithClangPP=true
```

The test framework is **xUnit**. Tests target `net8.0` (and `net472` on Windows).

## Architecture

The project has a pipeline architecture where each component feeds into the next:

1. **dnne-analyzers** (`src/dnne-analyzers/`) — Roslyn source generator that emits the DNNE attribute types into consuming projects at compile time. Targets `netstandard2.0` and requires Roslyn 4.0+. Generated attributes include:
   - `ExportAttribute`, `C99DeclCodeAttribute`, `C99TypeAttribute` — for C99 output
   - `RustDeclCodeAttribute`, `RustTypeAttribute` — for Rust output

2. **dnne-gen** (`src/dnne-gen/`) — CLI tool (.NET 8.0) that reads a compiled managed assembly via reflection, finds methods marked with `[UnmanagedCallersOnly]` or `[DNNE.Export]`, and generates native source code with the corresponding export signatures. Supports two output languages selected via `-l`:
   - `c99` (default) — generates C99 source with `DNNE_API` macros, C preprocessor platform guards, and lazy-init function pointer wrappers.
   - `rust` — generates Rust source with `pub unsafe fn` wrappers intended for Rust callers (no `extern "C"` / `#[no_mangle]`), `AtomicPtr` lazy initialization, `#[cfg(target_os)]` platform guards, and `core::ffi` types.

   The `Generator` class uses language-specific type providers (`C99TypeProvider` / `RustTypeProvider`) and emitters (`EmitC99` / `EmitRust`). Attribute detection is unified through `TryGetLanguageTypeAttributeValue` and `TryGetLanguageDeclCodeAttributeValue`, which select C99 or Rust attributes based on the output language. Exports with non-primitive value types that lack a type override are skipped in Rust mode.

3. **MSBuild integration** (`src/msbuild/`) — `DNNE.props` defines configurable properties; `DNNE.targets` wires up the build pipeline (run dnne-gen, then invoke the native compiler). `DNNE.BuildTasks/` contains custom MSBuild tasks with platform-specific compilation logic in `Windows.cs`, `Linux.cs`, and `macOS.cs`.

4. **Platform layer** (`src/platform/`) — Native source that bootstraps the .NET runtime via `nethost` and dispatches calls to managed exports.
   - `platform.c` / `dnne.h` — C implementation with platform-conditional code (`#ifdef DNNE_WINDOWS` etc.) for library loading, path resolution, error state preservation, and thread-safe runtime initialization via spinlock.
   - `platform_v4.cpp` — .NET Framework v4.x activation (C++ only).
   - `platform.rs` — Rust implementation targeting .NET (Core) only. Uses `std::sync::Mutex` for thread-safe initialization, platform-specific `sys` modules for Unix/Windows, and a UTF-8 public API (`*const u8`) with internal wide-string conversion on Windows. Expects a crate-root constant `DNNE_ASSEMBLY_NAME` (for example, generated into `lib.rs` by `DNNE.targets`).

5. **dnne-pkg** (`src/dnne-pkg/`) — Orchestrates NuGet package creation, bundling analyzers, gen tool, build tasks, and platform source into the published package.

## Key Conventions

- Exported methods must be `public static` and marked with `[UnmanagedCallersOnly]` (preferred) or `[DNNE.Export]` (experimental). The enclosing class's accessibility doesn't matter.
- When using `[DNNE.Export]`, a companion `Delegate` type named `<MethodName>Delegate` must exist at the same scope.
- Custom native type mappings use language-specific attributes:
  - **C99**: `[DNNE.C99Type("...")]` on parameters/return values and `[DNNE.C99DeclCode("...")]` on methods for struct definitions or `#include` directives.
  - **Rust**: `[DNNE.RustType("...")]` on parameters/return values and `[DNNE.RustDeclCode("...")]` on methods for type definitions or `use` statements.
- The generated native binary is named `<AssemblyName>NE` by default (suffix controlled by `DnneNativeBinarySuffix` MSBuild property).
- MSBuild properties controlling behavior are defined in `src/msbuild/DNNE.props` — this is the reference for all DNNE configuration options.
- C# language version varies by component: `C# 11.0` for analyzers, `C# 9.0` for build tasks, default for other projects.
- The DNNE attribute types are source-generated into consuming projects — DNNE provides no assembly reference.
