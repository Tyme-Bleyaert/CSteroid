using System;
using System.Collections.Generic;

namespace CSteroid
{
    internal static class NestedValidationHelper
    {
        public static IValidator<TProperty> ResolveValidator<TProperty>()
        {
            if (ValidatorRegistry.TryResolve<TProperty>(out var validator) && validator is not null)
                return validator;

            throw new InvalidOperationException(
                $"No validator is registered for type '{typeof(TProperty).Name}'. " +
                $"Register one via ValidatorRegistry.Register<{typeof(TProperty).Name}>(...) or pass a configure action.");
        }

        public static void AddPrefixed(List<ValidationFailure> failures, string prefix, ValidationResult result)
        {
            foreach (var failure in result.Failures)
            {
                failures.Add(new ValidationFailure
                {
                    PropertyName = prefix + failure.PropertyName,
                    ErrorMessage = failure.ErrorMessage,
                    ErrorCode = failure.ErrorCode,
                    AttemptedValue = failure.AttemptedValue,
                    Severity = failure.Severity
                });
            }
        }
    }
}
