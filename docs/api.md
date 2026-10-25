# CSteroid — API Reference

Canonical reference for the public attribute and enum API in `CSteroid.Core` and
`CSteroid.Orm`. For features and a consumer example see `README.md`; for
generator internals see `docs/architecture.md`.

## Goals

1. Generate domain models automatically from interface definitions.
2. Apply validation attributes and/or fluent validation rules based on interface annotations.
3. Generate mapping code between models and DTOs.
4. Keep a clean separation of Core Models and Generator logic.

## Enums

### `MappingTypes`

```csharp
[Flags]
public enum MappingTypes
{
    None                 = 0,
    ExtensionMethod      = 1,
    Static               = 2,
    ImplicitCast         = 4,
    ExplicitCast         = 8,
    DependencyInjection  = 16,
    ProjectionExpression = 32,
}
```

### `ValidationType`

```csharp
[Flags]
public enum ValidationType
{
    None            = 0,
    DataAnnotations = 1,
    Custom          = 2,
    Async           = 4,
}
```

Combinations:
- `DataAnnotations | Custom` copies attributes to the generated model and seeds a fluent validator with default rules.
- `Custom | Async` generates an asynchronous fluent validator.

### `DeclarationType`

```csharp
public enum DeclarationType
{
    Class  = 0,
    Record = 1,
    Struct = 2,
}
```

### `OrmTarget`

Lives in the **`CSteroid.Orm`** package (namespace `CSteroid`), not in `CSteroid.Core`.

```csharp
[Flags]
public enum OrmTarget
{
    None       = 0,
    EFCore     = 1,
    Dapper     = 2,
    CosmosDB   = 4,
    MongoDB    = 8,
    PostgreSQL = 16,
}
```

### `FileNamingConvention`

```csharp
public enum FileNamingConvention
{
    Default     = 0,
    ModelSuffix = 1,
}
```

## Attributes

### `CanonicalModelAttribute` (AllowMultiple = true)

```csharp
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
public sealed class CanonicalModelAttribute : Attribute
{
    public string Target { get; set; }
    public ValidationType ValidationType { get; set; }
    public MappingTypes MappingTypes { get; set; }
    public DeclarationType DeclarationType { get; set; }
    public string Version { get; set; }
    public string[] ExcludedProperties { get; set; }
    public string CustomNamespace { get; set; }
    public string FolderPath { get; set; }
    public string PartitionKey { get; set; }

    /// <summary>
    /// When true, generated mappers using this target as a mapping *source* skip the
    /// overridable plumbing (`_customMapFunc`, `ConfigureMapping` hook, `MappingHelpers`
    /// registration hop). No mapper class is emitted unless DI registration or a
    /// projection expression needs it; instead each entry point (extension method,
    /// static method, cast operator) inlines its own copy of the object initializer.
    /// Use when the mapping will never be customised — it is measurably faster
    /// (see CSteroid.Benchmarks). Applies per direction: flag both targets for a
    /// simple mapper in both directions.
    /// The generated model class is also sealed; it is emitted as a closed
    /// `public sealed class` (no `partial`) whenever nothing else needs to merge into it —
    /// i.e. the target is declared exactly once and carries no cast-operator flags — and as
    /// `public sealed partial class` otherwise (duplicate declarations or cast operators,
    /// where `partial` is the merge mechanism, not an extensibility hook). Structs stay
    /// unsealed (`sealed` is invalid on them; they are implicitly sealed).
    /// </summary>
    public bool Simple { get; set; }

    public CanonicalModelAttribute(string target) { Target = target; }

    public CanonicalModelAttribute(string target,
                                     ValidationType validationType = default,
                                     MappingTypes mappingTypes = default,
                                     DeclarationType declarationType = default,
                                     string version = null,
                                     string[] excludedProperties = null,
                                     string partitionKey = null)
    {
        Target = target;
        ValidationType = validationType;
        MappingTypes = mappingTypes;
        DeclarationType = declarationType;
        Version = version;
        ExcludedProperties = excludedProperties;
        PartitionKey = partitionKey;
    }
}
```

### `OrmTargetAttribute` (in `CSteroid.Orm`, AllowMultiple = true)

Applied to the same interface as `CanonicalModelAttribute` to select which
ORM-optimized generator emits the model.

```csharp
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
public sealed class OrmTargetAttribute : Attribute
{
    public OrmTarget Target { get; }
    public string Model { get; set; }

    public OrmTargetAttribute(OrmTarget target)
    {
        Target = target;
    }
}
```

- `Target` – the ORM flag for the model. A model may carry only one flag (`CSG101` otherwise).
- `Model` – optional; scopes the target to one model name. Omitted = applies to every model from the interface.

### `ExcludeFromGenerationAttribute`

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExcludeFromGenerationAttribute : Attribute
{
    public string[] ExcludedModels { get; set; }

    public ExcludeFromGenerationAttribute() { }

    public ExcludeFromGenerationAttribute(params string[] namesOfModelsForExclusion)
    {
        ExcludedModels = namesOfModelsForExclusion;
    }
}
```

### `GeneratorConfigAttribute` (assembly-level)

```csharp
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class GeneratorConfigAttribute : Attribute
{
    public FileNamingConvention FileNamingConvention { get; set; }
    public string ValidationProject { get; set; }
    public string MappingProject { get; set; }
}
```
