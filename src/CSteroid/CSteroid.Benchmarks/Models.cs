using System.ComponentModel.DataAnnotations;
using CSteroid;
using CSteroid.Generated.Models;

namespace CSteroid.Benchmarks;

// ---- CSteroid models (generated from these interfaces) ----

[CanonicalModel("BenchAddress")]
public interface IBenchAddress
{
    [Required]
    string Street { get; set; }

    [Required]
    string City { get; set; }
}

[CanonicalModel("BenchUser",
    MappingTypes = MappingTypes.ExtensionMethod,
    ValidationType = ValidationType.DataAnnotations | ValidationType.Custom)]
[CanonicalModel("BenchUserDto")]
public interface IBenchUser
{
    int Id { get; set; }

    [Required]
    [MaxLength(100)]
    string Name { get; set; }

    [Required]
    [EmailAddress]
    string Email { get; set; }

    int Age { get; set; }

    BenchAddress Address { get; set; }

    List<BenchAddress> Addresses { get; set; }

    List<int> Scores { get; set; }
}

// Flat shape: pure per-property codegen comparison, no collections or nesting.
[CanonicalModel("BenchFlat", MappingTypes = MappingTypes.ExtensionMethod)]
[CanonicalModel("BenchFlatDto")]
public interface IBenchFlat
{
    int Id { get; set; }
    string Name { get; set; }
    string Email { get; set; }
    int Age { get; set; }
    bool IsActive { get; set; }
}

// Simple mapping (branch-free, non-overridable) — flat and full shapes.
[CanonicalModel("BenchSimpleFlat", MappingTypes = MappingTypes.ExtensionMethod, Simple = true)]
[CanonicalModel("BenchSimpleFlatDto")]
public interface IBenchSimpleFlat
{
    int Id { get; set; }
    string Name { get; set; }
    string Email { get; set; }
    int Age { get; set; }
    bool IsActive { get; set; }
}

[CanonicalModel("BenchSimpleUser", MappingTypes = MappingTypes.ExtensionMethod, Simple = true)]
[CanonicalModel("BenchSimpleUserDto")]
public interface IBenchSimpleUser
{
    int Id { get; set; }
    string Name { get; set; }
    string Email { get; set; }
    int Age { get; set; }
    BenchAddress Address { get; set; }
    List<BenchAddress> Addresses { get; set; }
    List<int> Scores { get; set; }
}

// Cast operator benchmarks (simple + cast flags only, no extension methods).
[CanonicalModel("BenchCastImplicit", MappingTypes = MappingTypes.ImplicitCast, Simple = true)]
[CanonicalModel("BenchCastImplicitDto")]
public interface IBenchCastImplicit
{
    int Id { get; set; }
    string Name { get; set; }
    string Email { get; set; }
    int Age { get; set; }
    bool IsActive { get; set; }
}

[CanonicalModel("BenchCastExplicit", MappingTypes = MappingTypes.ExplicitCast, Simple = true)]
[CanonicalModel("BenchCastExplicitDto")]
public interface IBenchCastExplicit
{
    int Id { get; set; }
    string Name { get; set; }
    string Email { get; set; }
    int Age { get; set; }
    bool IsActive { get; set; }
}

// ---- Shape-identical plain classes for the Mapperly / hand-written baselines ----
// (Mapperly cannot consume other generators' output, so it maps plain types.)

public class PlainAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class PlainUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public PlainAddress Address { get; set; } = new();
    public List<PlainAddress> Addresses { get; set; } = [];
    public List<int> Scores { get; set; } = [];
}

public class PlainUserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public PlainAddress Address { get; set; } = new();
    public List<PlainAddress> Addresses { get; set; } = [];
    public List<int> Scores { get; set; } = [];
}

public class PlainFlat
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public class PlainFlatDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

// Hand-written cast-operator baselines (operators live on the source type).
public class PlainCastImplicit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }

    public static implicit operator PlainCastImplicitDto(PlainCastImplicit s) =>
        new() { Id = s.Id, Name = s.Name, Email = s.Email, Age = s.Age, IsActive = s.IsActive };
}

public class PlainCastImplicitDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public class PlainCastExplicit
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }

    public static explicit operator PlainCastExplicitDto(PlainCastExplicit s) =>
        new() { Id = s.Id, Name = s.Name, Email = s.Email, Age = s.Age, IsActive = s.IsActive };
}

public class PlainCastExplicitDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}
