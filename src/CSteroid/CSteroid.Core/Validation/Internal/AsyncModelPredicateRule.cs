using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    internal sealed class AsyncModelPredicateRule<T> : IAsyncRule<T>
    {
        private readonly Func<T, bool> _predicate;
        private readonly string _message;
        private string? _errorCode;
        private ValidationSeverity _severity;

        public AsyncModelPredicateRule(Func<T, bool> predicate, string message, string? errorCode)
        {
            _predicate = predicate;
            _message = message;
            _errorCode = errorCode;
            _severity = ValidationSeverity.Error;
        }

        public string? ErrorCode
        {
            get => _errorCode;
            set => _errorCode = value;
        }

        public ValidationSeverity Severity
        {
            get => _severity;
            set => _severity = value;
        }

        public Func<T, bool>? WhenCondition { get; set; }

        public Func<T, bool>? UnlessCondition { get; set; }

        public Task EvaluateAsync(T model, List<ValidationFailure> failures, CancellationToken cancellationToken)
        {
            if (WhenCondition != null && !WhenCondition(model))
                return Task.CompletedTask;

            if (UnlessCondition != null && UnlessCondition(model))
                return Task.CompletedTask;

            if (!_predicate(model))
            {
                failures.Add(new ValidationFailure
                {
                    PropertyName = string.Empty,
                    ErrorMessage = _message,
                    ErrorCode = _errorCode,
                    AttemptedValue = model,
                    Severity = _severity
                });
            }

            return Task.CompletedTask;
        }
    }
}
