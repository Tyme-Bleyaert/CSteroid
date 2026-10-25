# HexagonalDdd example

A hexagonal-architecture / DDD example built on CSteroid's **metadata-lib pattern**: the
`[CanonicalModel]` interfaces live in small metadata libraries, and each layer project
generates only the models it owns.

## Project layout

```
HexagonalDdd.Order.Metadata/       Interfaces only (references CSteroid.Core, no generators)
HexagonalDdd.Order.Domain/         OrderEntity + OrderBo + explicit casts + domain behavior
HexagonalDdd.Order.Application/    OrderDto + BO->DTO extension, ports, OrderService
HexagonalDdd.Customer.*            Same shape for the Customer bounded context
HexagonalDdd.Ui/                   ViewModels + DTO->VM extensions, composition root, console
```

Each layer project:

1. references the metadata lib (and the previous layer where the architecture demands it),
2. attaches the generators as analyzers,
3. declares which namespaces it owns:

```csharp
// HexagonalDdd.Order.Application/GeneratorConfig.cs
using CSteroid;

[assembly: GeneratorConfigAttribute(
    GenerateOnlyNamespaces = new string[] { "HexagonalDdd.Order.Application.Dtos" })]
```

Without the filter, every referencing project would generate every model and the builds
would collide (CS0436).

## How the mapping flows

All targets for an aggregate sit on one interface in the metadata lib, because CSteroid
forms mapping pairs only between targets on the same interface:

```csharp
[CanonicalModelAttribute("OrderEntity", CustomNamespace = "...Order.Domain.Models", MappingTypes = MappingTypes.ExplicitCast)]
[CanonicalModelAttribute("OrderBo",     CustomNamespace = "...Order.Domain.Models", MappingTypes = MappingTypes.ExplicitCast | MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute("OrderDto",    CustomNamespace = "...Order.Application.Dtos", MappingTypes = MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute("OrderViewModel", CustomNamespace = "HexagonalDdd.Ui.ViewModels", MappingTypes = MappingTypes.ExtensionMethod)]
public interface IOrderModelDefinition : IOrderBase { ... }
```

* **Domain** claims `...Domain.Models`: generates `OrderEntity` and `OrderBo` plus the
  explicit cast operators between them. Cast operators must live inside the source type,
  so they only appear in the project that generates the source.
* **Application** claims `...Application.Dtos` and references Domain: generates `OrderDto`
  and the `OrderBo.MapToOrderDto()` extension. The mapping is emitted here because the DTO
  (the destination) is local and `OrderBo` is resolvable through the Domain reference.
* **Ui** claims `...Ui.ViewModels` and references Application: generates `OrderViewModel`
  and the `OrderDto.MapToOrderViewModel()` extension.

Domain behavior lives in a hand-written partial next to the generated model
(`OrderBo.Behavior.cs` adds `Total` and `ApplyPriorityDiscount()`).

## Enforcing the layering

CSteroid enforces what is technically necessary — a project cannot emit mappings toward
types it cannot reference, and `GenerateOnlyNamespaces` keeps each project to its own
models. Broader architecture rules ("Domain must not depend on Application", "one
namespace per project") are *your* policy and belong in architecture tests. With
[ArchUnitNET](https://github.com/TNG/ArchUnitNET) that looks like:

```csharp
// In your own test project — your architecture, your rules:
var domainOnly = ArchRuleDefinition.Types()
    .That().ResideInNamespace(".*\\.Domain\..*", true)
    .Should().NotDependOnAny(".*\\.Application\..*")
    .AndShould().NotDependOnAny(".*\\.Ui\..*");

domainOnly.Check(architecture);
```

CSteroid also validates its own configuration with two diagnostics:

* **CSG102 (warning)** — `GenerateOnlyNamespaces` lists a namespace no discovered
  target generates into (typo or stale filter; otherwise nothing generates silently).
* **CSG103 (info)** — decorated interfaces spanning multiple namespaces were found in
  referenced assemblies, but no filter is configured (everything will generate here,
  risking CS0436 collisions). Single-namespace metadata libraries (monolith style)
  stay silent.

## Run it

```bash
dotnet run --project examples/HexagonalDdd/HexagonalDdd.Ui
```

The console app wires in-memory persistence adapters into the application services and
runs self-checks across the Entity -> BO -> DTO -> ViewModel chain.
