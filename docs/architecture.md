# CSteroid — Architecture & Details

Detailed reference for agent tasks that touch generator internals, testing, or packaging.
For the essentials (stack, commands, rules), see `AGENTS.md`.

## Project descriptions

* **CSteroid.Core** – Public API surface consumers reference: attributes, enums,
  `IMapper<TSource,TDest>`, and the lightweight validation rule engine
  (`IValidator<T>`, `IAsyncValidator<T>`, `IRuleBuilder<T>`, etc.).
* **CSteroid.Orm** – ORM target selection: the `OrmTarget` flags enum and
  `OrmTargetAttribute` (namespace `CSteroid`). Consumers apply
  `[OrmTarget(OrmTarget.EFCore)]` alongside `[CanonicalModel(...)]` on the same
  interface to route the model to the matching ORM generator package.
* **CSteroid.Generator** – Base model generator (`CSteroidGenerator`): plain
  partial model classes and DataAnnotations forwarding. Skips models with an
  `OrmTarget` flag (those are emitted by the matching ORM package) and reports
  `CSG101` when conflicting ORM flags are combined.
* **CSteroid.Generator.Mapping** – Mapping generator (`CSteroidMappingGenerator`):
  extension methods, cast operators, mapper classes, DI registrations,
  projection expressions.
* **CSteroid.Generator.Validation** – Validation generator
  (`CSteroidValidationGenerator`): default rules from DataAnnotations,
  custom/async validators, nested validation wiring.
* **CSteroid.Generator.EFCore / .Dapper / .CosmosDB / .MongoDB / .PostgreSQL** –
  ORM generators, one package per target. Each emits the *complete* model (ORM
  conventions included) for models carrying exactly its `OrmTarget` flag,
  reusing `ModelSourceBuilder` from the shared library.
* **CSteroid.Generator.Shared** – Code shared by all generators (`ModelInfo`,
  symbol scanning, `ModelSourceBuilder`, hint-path resolution, nested-target
  helpers, literal formatting). Build-time only; not packable on its own.
* **CSteroid.Generator.Testing** – Shared xUnit test harness
  (`GeneratorTestHarness`) used by the test projects.
* **CSteroid.Generator.Tests / .Mapping.Tests / .Validation.Tests / .EFCore.Tests /
  .Dapper.Tests / .CosmosDB.Tests / .MongoDB.Tests / .PostgreSQL.Tests** – xUnit
  tests per generator, running the generators against in-memory compilations
  and asserting on emitted source and runtime behavior.

## Technology details

* Generator projects emit their build output as an **Analyzer**
  (`<OutputItemType>Analyzer</OutputItemType>`) and are marked **packable** for
  NuGet distribution.
* Roslyn packages: `Microsoft.CodeAnalysis.CSharp` 4.10.0,
  `Microsoft.CodeAnalysis.Analyzers` 3.3.4.

## How the generators work

All generators share the same interface discovery pipeline (in
`CSteroid.Generator.Shared`): they scan the compilation for interfaces decorated
with at least one `[CanonicalModelAttribute]`, build a `ModelInfo` per
attribute instance, and emit deterministic source text.

### 1. CSteroidGenerator (base models)

For every attribute instance it generates a **partial class** named after the
attribute's `Target` argument, placed in the namespace `CSteroid.Generated.Models`
(or the attribute's `CustomNamespace`). It also forwards DataAnnotations
attributes. Models with an `OrmTarget` flag are skipped (the matching ORM
package emits them); combining multiple ORM flags produces error `CSG101`.

Models flagged `Simple = true` are emitted **sealed**. When the target is
declared exactly once and carries no cast-operator flags, the model is a
closed `public sealed class` — no `partial`, so consumers cannot extend it via
partial files. When duplicate declarations of the target exist, or cast
operators apply (the mapping generator merges its operator part into the
class), the model is `public sealed partial class` instead — there `partial`
is the merge mechanism, not an extensibility hook. Structs are never sealed
(keyword invalid; implicitly sealed); ORM models are never sealed (EF
lazy-loading proxies need unsealed classes with virtual members).

### 1a. ORM generators

Each ORM package emits the **complete model** (including ORM conventions:
constructors, `virtual` navigation properties, attributes) for models carrying
exactly its `OrmTarget` flag. Consumers must reference the ORM package matching
the flag they use, or the model is not generated at all.

Property handling:

* Gathers properties from the interface **and** all base interfaces recursively.
* Properties are sorted alphabetically for deterministic output.
* Excludes a property if:
  * its name appears in the attribute's `ExcludedProperties` array, **or**
  * the property is decorated with `[ExcludeFromGeneration]` and its
    `ExcludedModels` list contains the current model name.

### 2. CSteroidMappingGenerator

When a `CanonicalModelAttribute` has the `ExtensionMethod` flag set in
`MappingTypes`, it generates:

* An extension method `MapTo<Dst>(this Src item)` inside
  `CSteroid.Generated.Mappings`.
* A mapper class `<Src>To<Dst>Mapper`. It implements `IMapper<Src,Dst>` only
  when the `DependencyInjection` flag is set — the DI registration is the
  interface's only consumer.

All generated pairs are sorted by source name then destination name to keep
output deterministic.

The mapper class `<Src>To<Dst>Mapper` is the single customization point
(`ConfigureMapping` hook), so it is emitted only when it earns its place:
when several entry methods (extension + static + cast) would otherwise each
need overriding, or when a feature requires it (DI registration, projection
expression). In two cases no mapper class is generated at all and the object
initializer is inlined directly into the entry point:

* **Cast-only pairs** — a lone `ImplicitCast`/`ExplicitCast` operator cannot
  be overridden or hooked by consumers, so the class would be dead weight.
  Same for pairs whose source declares no mapping entry point at all
  (flag-less reverse pairs).
* **`Simple = true` on the source target** — the mapping is declared
  non-overridable, so each entry point (extension method, static method, cast
  operator) carries its own copy of the initializer. No `_customMapFunc`, no
  `ConfigureMapping` hook, no `MappingHelpers` instance hop. Exception: DI or
  projection still force the mapper class, since they reference it by type.

When a mapper class *is* emitted, `Simple` sources emit `Map`/`DefaultMap` as
a thin delegate to an `internal static Dst MapCore(Src source)` holding the
initializer. All entry points (`MapCore`, extension methods, static methods,
cast operators) carry `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
so RyuJIT inlines them into call sites (measured: ~1 ns per call on flat
shapes). `Simple` is per direction: flag both targets on an interface to get
simple mappers both ways (see `CSteroid.Benchmarks` for measured numbers vs
the regular path and Mapperly).

### 3. CSteroidValidationGenerator

When `ValidationType.Custom` is set, it generates an internal partial
`<Target>Validator` (sync or async) that seeds default rules from
DataAnnotations, supports user rules via a `static partial void
ConfigureValidation` hook, and auto-wires nested validation
(`ForNested`/`ForEachNested`) for properties whose type has a generated
validator. The rule chain (including all compiled expression selectors) is
built **once** in the validator's static constructor and cached in a static
`s_ruleBuilder` field — rebuilding it per `Validate` call measured ~370 µs/call
vs ~84 ns cached (see `CSteroid.Benchmarks`). `RuleBuilder<T>.Validate` /
`AsyncRuleBuilder<T>.ValidateAsync` return the `ValidationResult.Success`
singleton when no rules fail.

### Consumer usage pattern

```csharp
[CanonicalModelAttribute("User", MappingType: MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute("UserDto")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```

The above causes the generators to emit `User`, `UserDto`, and
`User.MapToUserDto()`.

### Metadata-lib mode (multi-project solutions)

The `[CanonicalModel]` interfaces can live in a shared library (a "metadata
lib") that only references `CSteroid.Core` and has no generators attached. Each
consuming project attaches the generators as analyzers and declares which model
namespaces it owns:

```csharp
[assembly: GeneratorConfigAttribute(
    GenerateOnlyNamespaces = new string[] { "MyApp.Order.Application.Dtos" })]
```

Emission rules in this mode:

* **Models and validators** are generated only when the model's namespace
  (`CustomNamespace`, or the default `CSteroid.Generated.Models`) is listed in
  the project's `GenerateOnlyNamespaces`. With no filter configured, every
  project generates every model (pre-existing behavior; leads to CS0436
  collisions in multi-project setups).
* **Cast operators** are only emitted when the source model is local, because
  C# requires operators to be declared inside the source type, and the
  destination type must be resolvable in the same compilation (generated there
  or referenced).
* **Extension methods, mapper classes, projections and DI registrations** are
  only emitted when the destination model is local and the source type is
  resolvable — either local or generated by a referenced project. This makes
  mappings point "inward": a Domain project never emits mappings toward
  Application types it cannot reference.

Interfaces are discovered both in the current compilation and in referenced
assemblies that carry `[CanonicalModel]` interfaces (matched by attribute
display name, since `GetTypeByMetadataName` returns null for in-memory metadata
references). In-source interfaces win on metadata-name collisions.

Configuration diagnostics (`CSteroid.Generator.Shared/ConfigDiagnostics.cs`):

* `CSG102` (warning) — a claimed `GenerateOnlyNamespaces` entry matches no
  discovered target; catches typos that would otherwise silently emit nothing.
* `CSG103` (info) — decorated interfaces spanning multiple namespaces exist in
  referenced assemblies but no filter is configured. Only fires for layered
  metadata libraries; single-namespace (monolith) setups stay silent.

Layering rules beyond what the compiler enforces (e.g. "Domain must not depend
on Application") are consumer policy: enforce them with architecture tests such
as ArchUnitNET, not in the generator. See `examples/HexagonalDdd/README.md`.

See `examples/HexagonalDdd/` for a full metadata-lib solution with per-layer
projects.

## Testing details

Tests compile in-memory source strings through the relevant generator(s) via
`GeneratorTestHarness` (in `CSteroid.Generator.Testing`), assert on the
generated source text, and load the emitted assembly to exercise runtime
behavior. When adding generator features, add a test to the matching project
covering both the generated source shape and the runtime result.

## Code style details

* File encoding: UTF-8 with BOM in some `.cs` files; the generators emit UTF-8.
* Using directives: placed at the top of the file, outside the namespace.
* Generated files: marked with `// <auto-generated />` at the top.
* Every generated type carries `[global::System.CodeDom.Compiler.GeneratedCode]`
  with the generator assembly's name and version (`GeneratorMetadata` in
  `CSteroid.Generator.Shared`) so analyzers, coverage tools, and IDEs recognize
  machine-written code. Namespace-qualified references in generated code use the
  `global::` prefix so consumer-side namespaces cannot shadow them.

## CodeGraph index

A local CodeGraph SQLite index lives at
`src/CSteroid/.codegraph/codegraph.db`. It indexes symbols and cross-file
relationships into `nodes`, `edges`, and `files` so agents and tools can
inspect the codebase without scanning every source file first.

Example query:

```bash
sqlite3 src/CSteroid/.codegraph/codegraph.db \
  "select name, qualified_name, file_path, start_line from nodes where name = 'CSteroidGenerator';"
```

If `sqlite3` is unavailable, Python's standard `sqlite3` module can query the
same database.

Do not commit, hand-edit, or treat the database as authoritative project
documentation. `src/CSteroid/.codegraph/.gitignore` excludes the database and
other transient CodeGraph files; regenerate or update the index after source
changes with the CodeGraph tooling.

## Security considerations

* The generator runs at **compile time** with the same trust level as the
  compiler itself. It reads only Roslyn syntax/semantic symbols; it does not
  execute arbitrary code from the consuming project.
* No secrets, connection strings, or certificates are present in the
  repository.
* The `.gitignore` already excludes standard Visual Studio user files
  (`*.suo`, `*.user`), build outputs (`bin/`, `obj/`), and NuGet packages
  (`*.nupkg`).

## Packing

Shared NuGet metadata (version, license, repository, readme) lives in the repo-root
`Directory.Build.props`; `Directory.Build.targets` packs each analyzer project with
its own DLL plus the bundled `CSteroid.Generator.Shared.dll` under
`analyzers/dotnet/cs` via evaluation-time `None` items. Analyzer and meta-packages
include an empty `lib/netstandard2.0/_._` placeholder so their framework matches their
dependency groups without exposing generator DLLs as regular references.
`CSteroid.Core` (and `CSteroid.Orm` for ORM generators) remains a transitive dependency;
`CSteroid.Generator.Shared` is bundled rather than listed as a dependency.

```bash
# Pack everything at once (Core, Orm, all 8 generators):
dotnet pack src/CSteroid/CSteroid.slnx -c Release
```

`Version` defaults to `1.0.0` in `Directory.Build.props`; the publish workflow
overrides it from the git tag (`git tag v1.2.0 && git push origin v1.2.0` publishes
`1.2.0` to NuGet.org using the `NUGET_API_KEY` repository secret).

CI lives in `.github/workflows/`: `ci.yml` builds, tests, runs the HexagonalDdd
self-checks, and packs on every push/PR; `publish.yml` publishes on `v*` tags.
