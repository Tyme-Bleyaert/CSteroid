using BenchmarkDotNet.Attributes;
using CSteroid.Generated.Models;
using CSteroid.Generated.Validation;

namespace CSteroid.Benchmarks;

[MemoryDiagnoser]
public class ValidationBenchmarks
{
    private static readonly BenchUserValidator s_validator = new();

    internal static BenchUser CreateValid() => new()
    {
        Id = 42,
        Name = "Jane Doe",
        Email = "jane@example.com",
        Age = 33,
        Address = new BenchAddress { Street = "Main Street 1", City = "Springfield" },
        Scores = Enumerable.Range(0, 100).ToList(),
    };

    internal static BenchUser CreateInvalid() => new()
    {
        Id = 42,
        Name = "",
        Email = "not-an-email",
        Age = 33,
        Address = new BenchAddress { Street = "Main Street 1", City = "Springfield" },
        Scores = Enumerable.Range(0, 100).ToList(),
    };

    private static readonly BenchUser s_valid = CreateValid();

    private static readonly BenchUser s_invalid = CreateInvalid();

    [Benchmark(Description = "CSteroid (generated, valid)")]
    public ValidationResult CSteroid_Valid() => s_validator.Validate(s_valid);

    [Benchmark(Description = "CSteroid (generated, invalid)")]
    public ValidationResult CSteroid_Invalid() => s_validator.Validate(s_invalid);

    [Benchmark(Description = "Hand-written (valid)")]
    public ValidationResult Handwritten_Valid() => HandwrittenValidate(s_valid);

    [Benchmark(Description = "Hand-written (invalid)")]
    public ValidationResult Handwritten_Invalid() => HandwrittenValidate(s_invalid);

    private static readonly System.Text.RegularExpressions.Regex s_emailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static ValidationResult HandwrittenValidate(BenchUser model)
    {
        var failures = new List<ValidationFailure>();

        if (model.Name is null || model.Name.Length == 0)
            failures.Add(new ValidationFailure { PropertyName = nameof(BenchUser.Name), ErrorMessage = "'Name' must not be empty." });
        else if (model.Name.Length > 100)
            failures.Add(new ValidationFailure { PropertyName = nameof(BenchUser.Name), ErrorMessage = "'Name' must be 100 characters or fewer." });

        if (model.Email is null || model.Email.Length == 0)
            failures.Add(new ValidationFailure { PropertyName = nameof(BenchUser.Email), ErrorMessage = "'Email' must not be empty." });
        else if (!s_emailRegex.IsMatch(model.Email))
            failures.Add(new ValidationFailure { PropertyName = nameof(BenchUser.Email), ErrorMessage = "'Email' must be a valid email address." });

        return new ValidationResult(failures);
    }
}
