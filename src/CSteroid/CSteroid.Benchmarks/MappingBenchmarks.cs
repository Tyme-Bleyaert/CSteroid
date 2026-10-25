using BenchmarkDotNet.Attributes;
using CSteroid.Generated.Models;
using CSteroid.Generated.Mappings;

namespace CSteroid.Benchmarks;

[MemoryDiagnoser]
public class MappingBenchmarks
{
    private static readonly BenchMapper s_mapperly = new();

    private static List<BenchAddress> CreateBenchAddresses() =>
        Enumerable.Range(0, 10).Select(i => new BenchAddress { Street = $"Street {i}", City = "Springfield" }).ToList();

    private static List<PlainAddress> CreatePlainAddresses() =>
        Enumerable.Range(0, 10).Select(i => new PlainAddress { Street = $"Street {i}", City = "Springfield" }).ToList();

    internal static BenchUser CreateCSteroidSource() => new()
    {
        Id = 42,
        Name = "Jane Doe",
        Email = "jane@example.com",
        Age = 33,
        Address = new BenchAddress { Street = "Main Street 1", City = "Springfield" },
        Addresses = CreateBenchAddresses(),
        Scores = Enumerable.Range(0, 100).ToList(),
    };

    internal static PlainUser CreatePlainSource() => new()
    {
        Id = 42,
        Name = "Jane Doe",
        Email = "jane@example.com",
        Age = 33,
        Address = new PlainAddress { Street = "Main Street 1", City = "Springfield" },
        Addresses = CreatePlainAddresses(),
        Scores = Enumerable.Range(0, 100).ToList(),
    };

    internal static BenchFlat CreateCSteroidFlat() => new()
    {
        Id = 42, Name = "Jane Doe", Email = "jane@example.com", Age = 33, IsActive = true,
    };

    internal static PlainFlat CreatePlainFlat() => new()
    {
        Id = 42, Name = "Jane Doe", Email = "jane@example.com", Age = 33, IsActive = true,
    };

    internal static BenchSimpleFlat CreateSimpleFlat() => new()
    {
        Id = 42, Name = "Jane Doe", Email = "jane@example.com", Age = 33, IsActive = true,
    };

    internal static BenchSimpleUser CreateSimpleUser() => new()
    {
        Id = 42,
        Name = "Jane Doe",
        Email = "jane@example.com",
        Age = 33,
        Address = new BenchAddress { Street = "Main Street 1", City = "Springfield" },
        Addresses = CreateBenchAddresses(),
        Scores = Enumerable.Range(0, 100).ToList(),
    };

    internal static BenchCastImplicit CreateCastImplicit() => new()
    {
        Id = 42, Name = "Jane Doe", Email = "jane@example.com", Age = 33, IsActive = true,
    };

    internal static BenchCastExplicit CreateCastExplicit() => new()
    {
        Id = 42, Name = "Jane Doe", Email = "jane@example.com", Age = 33, IsActive = true,
    };

    internal static PlainCastImplicit CreatePlainCastImplicit() => new()
    {
        Id = 42, Name = "Jane Doe", Email = "jane@example.com", Age = 33, IsActive = true,
    };

    internal static PlainCastExplicit CreatePlainCastExplicit() => new()
    {
        Id = 42, Name = "Jane Doe", Email = "jane@example.com", Age = 33, IsActive = true,
    };

    private static readonly BenchUser s_csteroidSource = CreateCSteroidSource();
    private static readonly PlainUser s_plainSource = CreatePlainSource();
    private static readonly BenchFlat s_csteroidFlat = CreateCSteroidFlat();
    private static readonly PlainFlat s_plainFlat = CreatePlainFlat();
    private static readonly BenchSimpleFlat s_simpleFlat = CreateSimpleFlat();
    private static readonly BenchSimpleUser s_simpleUser = CreateSimpleUser();
    private static readonly BenchCastImplicit s_castImplicit = CreateCastImplicit();
    private static readonly BenchCastExplicit s_castExplicit = CreateCastExplicit();
    private static readonly PlainCastImplicit s_plainCastImplicit = CreatePlainCastImplicit();
    private static readonly PlainCastExplicit s_plainCastExplicit = CreatePlainCastExplicit();

    // Flat shape: identical work on all three (scalar properties only).
    [Benchmark(Description = "CSteroid (flat)")]
    public BenchFlatDto CSteroidFlat() => s_csteroidFlat.MapToBenchFlatDto();

    [Benchmark(Description = "Mapperly (flat)")]
    public PlainFlatDto MapperlyFlat() => s_mapperly.MapFlat(s_plainFlat);

    [Benchmark(Description = "Hand-written (flat)")]
    public PlainFlatDto HandwrittenFlat() => HandwrittenFlatMap(s_plainFlat);

    // Full shape. Semantics differ deliberately: CSteroid shares same-type collections
    // of non-target elements (Scores), Mapperly copies them. Addresses (target element
    // type) is copied by both.
    [Benchmark(Description = "CSteroid (full)")]
    public BenchUserDto CSteroid() => s_csteroidSource.MapToBenchUserDto();

    [Benchmark(Description = "Mapperly (full)")]
    public PlainUserDto Mapperly() => s_mapperly.Map(s_plainSource);

    [Benchmark(Description = "Hand-written (full, CSteroid semantics)")]
    public PlainUserDto Handwritten() => HandwrittenMap(s_plainSource);

    // Simple mapping: branch-free, non-overridable path.
    [Benchmark(Description = "CSteroid simple (flat)")]
    public BenchSimpleFlatDto SimpleFlat() => s_simpleFlat.MapToBenchSimpleFlatDto();

    [Benchmark(Description = "CSteroid simple (full)")]
    public BenchSimpleUserDto SimpleFull() => s_simpleUser.MapToBenchSimpleUserDto();

    // Cast operators.
    [Benchmark(Description = "Implicit cast (CSteroid)")]
    public BenchCastImplicitDto CastImplicitCSteroid() => s_castImplicit;

    [Benchmark(Description = "Implicit cast (hand-written)")]
    public PlainCastImplicitDto CastImplicitHandwritten() => s_plainCastImplicit;

    [Benchmark(Description = "Explicit cast (CSteroid)")]
    public BenchCastExplicitDto CastExplicitCSteroid() => (BenchCastExplicitDto)s_castExplicit;

    [Benchmark(Description = "Explicit cast (hand-written)")]
    public PlainCastExplicitDto CastExplicitHandwritten() => (PlainCastExplicitDto)s_plainCastExplicit;

    internal static PlainUserDto HandwrittenMap(PlainUser source) =>
        new()
        {
            Address = source.Address,
            Addresses = source.Addresses?.ToList()!,
            Age = source.Age,
            Email = source.Email,
            Id = source.Id,
            Name = source.Name,
            Scores = source.Scores,
        };

    internal static PlainFlatDto HandwrittenFlatMap(PlainFlat source) =>
        new()
        {
            Age = source.Age,
            Email = source.Email,
            Id = source.Id,
            IsActive = source.IsActive,
            Name = source.Name,
        };
}
