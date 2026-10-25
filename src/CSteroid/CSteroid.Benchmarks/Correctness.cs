using CSteroid.Generated.Models;
using CSteroid.Generated.Mappings;
using CSteroid.Generated.Validation;

namespace CSteroid.Benchmarks;

/// <summary>
/// Sanity checks executed before benchmarking so the compared implementations are known
/// to produce equivalent results. Run with: dotnet run -c Release -- --check
/// </summary>
public static class Correctness
{
    public static void RunAll()
    {
        CheckMapping();
        CheckValidation();
        Console.WriteLine("All correctness checks passed.");
    }

    private static void CheckMapping()
    {
        var benchSource = MappingBenchmarks.CreateCSteroidSource();
        var plainSource = MappingBenchmarks.CreatePlainSource();

        var csteroid = benchSource.MapToBenchUserDto();
        var mapperly = new BenchMapper().Map(plainSource);
        var handwritten = MappingBenchmarks.HandwrittenMap(plainSource);

        Assert(mapperly.Id == handwritten.Id && mapperly.Name == handwritten.Name &&
               mapperly.Email == handwritten.Email && mapperly.Age == handwritten.Age, "Mapperly vs hand-written: scalar mismatch");
        Assert(csteroid.Id == plainSource.Id && csteroid.Name == plainSource.Name &&
               csteroid.Email == plainSource.Email && csteroid.Age == plainSource.Age, "CSteroid: scalar mismatch");

        // Collection semantics differ by design: CSteroid shares same-type collections of
        // non-target elements (Scores); Mapperly copies collections. Target-element
        // collections (Addresses) are copied by both.
        Assert(csteroid.Scores!.SequenceEqual(plainSource.Scores), "CSteroid: Scores content mismatch");
        Assert(ReferenceEquals(csteroid.Scores, benchSource.Scores), "CSteroid: Scores should be shared by reference");
        Assert(mapperly.Scores!.SequenceEqual(plainSource.Scores), "Mapperly: Scores content mismatch");

        Assert(csteroid.Addresses!.Select(a => a.Street).SequenceEqual(plainSource.Addresses.Select(a => a.Street)),
            "CSteroid: Addresses content mismatch");
        Assert(!ReferenceEquals(csteroid.Addresses, benchSource.Addresses), "CSteroid: Addresses should be a new list");
        Assert(ReferenceEquals(csteroid.Addresses[0], benchSource.Addresses[0]), "CSteroid: Address elements should be shared");

        // Flat mapping: identical results across all three.
        var flatCsteroid = MappingBenchmarks.CreateCSteroidFlat().MapToBenchFlatDto();
        var flatMapperly = new BenchMapper().MapFlat(MappingBenchmarks.CreatePlainFlat());
        var flatHand = MappingBenchmarks.HandwrittenFlatMap(MappingBenchmarks.CreatePlainFlat());
        Assert(flatCsteroid.Id == flatMapperly.Id && flatCsteroid.Name == flatMapperly.Name &&
               flatCsteroid.Email == flatMapperly.Email && flatCsteroid.Age == flatMapperly.Age &&
               flatCsteroid.IsActive == flatMapperly.IsActive &&
               flatMapperly.Id == flatHand.Id && flatMapperly.Name == flatHand.Name,
            "Flat mapping: scalar mismatch across implementations");

        // Nested semantics: identical nested type -> reference assignment in all three.
        Assert(ReferenceEquals(csteroid.Address, benchSource.Address), "CSteroid: Address should be reference-copied");
        Assert(ReferenceEquals(mapperly.Address, plainSource.Address) || mapperly.Address.Street == plainSource.Address.Street,
            "Mapperly: Address mismatch");

        // Simple mapping: same values and semantics as the regular path.
        var simpleFlat = MappingBenchmarks.CreateSimpleFlat().MapToBenchSimpleFlatDto();
        var regularFlat = MappingBenchmarks.CreateCSteroidFlat().MapToBenchFlatDto();
        Assert(simpleFlat.Id == regularFlat.Id && simpleFlat.Name == regularFlat.Name &&
               simpleFlat.Email == regularFlat.Email && simpleFlat.Age == regularFlat.Age &&
               simpleFlat.IsActive == regularFlat.IsActive, "Simple flat: mismatch vs regular path");

        var simpleSource = MappingBenchmarks.CreateSimpleUser();
        var simpleFull = simpleSource.MapToBenchSimpleUserDto();
        var regularFull = MappingBenchmarks.CreateCSteroidSource().MapToBenchUserDto();
        Assert(simpleFull.Name == regularFull.Name && simpleFull.Email == regularFull.Email,
            "Simple full: scalar mismatch vs regular path");
        Assert(ReferenceEquals(simpleFull.Address, simpleSource.Address),
            "Simple full: Address should be reference-copied");
        Assert(!ReferenceEquals(simpleFull.Addresses, simpleSource.Addresses),
            "Simple full: Addresses should be a new list");

        // Cast operators: same values as hand-written operators.
        BenchCastImplicitDto implicitDto = MappingBenchmarks.CreateCastImplicit();
        PlainCastImplicitDto implicitHand = MappingBenchmarks.CreatePlainCastImplicit();
        Assert(implicitDto.Id == implicitHand.Id && implicitDto.Name == implicitHand.Name &&
               implicitDto.Email == implicitHand.Email && implicitDto.Age == implicitHand.Age &&
               implicitDto.IsActive == implicitHand.IsActive, "Implicit cast: mismatch vs hand-written");

        var explicitDto = (BenchCastExplicitDto)MappingBenchmarks.CreateCastExplicit();
        var explicitHand = (PlainCastExplicitDto)MappingBenchmarks.CreatePlainCastExplicit();
        Assert(explicitDto.Id == explicitHand.Id && explicitDto.Name == explicitHand.Name &&
               explicitDto.Email == explicitHand.Email && explicitDto.Age == explicitHand.Age &&
               explicitDto.IsActive == explicitHand.IsActive, "Explicit cast: mismatch vs hand-written");
    }

    private static void CheckValidation()
    {
        var validator = new BenchUserValidator();
        var valid = ValidationBenchmarks.CreateValid();
        var invalid = ValidationBenchmarks.CreateInvalid();

        var validResult = validator.Validate(valid);
        Assert(validResult.IsValid, "CSteroid validator: valid input should pass");

        var invalidResult = validator.Validate(invalid);
        Assert(!invalidResult.IsValid, "CSteroid validator: invalid input should fail");
        Assert(invalidResult.Failures.All(f => f.PropertyName is nameof(BenchUser.Name) or nameof(BenchUser.Email)),
            "CSteroid validator: unexpected failure properties");

        var handInvalid = ValidationBenchmarks.HandwrittenValidate(invalid);
        Assert(handInvalid.Failures.Select(f => f.PropertyName).OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(invalidResult.Failures.Select(f => f.PropertyName).OrderBy(x => x, StringComparer.Ordinal)),
            "Hand-written vs CSteroid validator: failing properties differ");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Correctness check failed: " + message);
    }
}
