namespace CSteroid
{
    public sealed class ValidationFailure
    {
        public string PropertyName { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public string? ErrorCode { get; init; }

        public object? AttemptedValue { get; init; }

        public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;
    }
}
