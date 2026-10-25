using System;
using System.Collections.Generic;
using System.Linq;

namespace CSteroid
{
    public sealed class ValidationResult
    {
        public static readonly ValidationResult Success = new ValidationResult(Array.Empty<ValidationFailure>());

        // Only Error-severity failures invalidate the model; Warning and Info are
        // informational (FluentValidation semantics).
        public bool IsValid => !Failures.Any(f => f.Severity == ValidationSeverity.Error);

        public IReadOnlyList<ValidationFailure> Failures { get; }

        public ValidationResult(IReadOnlyList<ValidationFailure> failures)
        {
            Failures = failures ?? Array.Empty<ValidationFailure>();
        }
    }
}
