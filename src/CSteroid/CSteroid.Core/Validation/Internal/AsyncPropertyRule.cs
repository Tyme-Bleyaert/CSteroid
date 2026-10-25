using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    internal sealed class AsyncPropertyRule<T, TProperty> : IAsyncRule<T>
    {
        private readonly Func<T, TProperty> _selector;
        private readonly string _propertyName;
        private readonly Func<TProperty, CancellationToken, Task<bool>>? _propertyPredicate;
        private readonly Func<T, TProperty, CancellationToken, Task<bool>>? _crossPropertyPredicate;
        private string _message;
        private Func<T, TProperty, string>? _messageFactory;
        private string? _errorCode;
        private ValidationSeverity _severity;

        public AsyncPropertyRule(
            Expression<Func<T, TProperty>> selector,
            Func<TProperty, CancellationToken, Task<bool>> predicate,
            string message,
            string? errorCode)
        {
            _selector = selector.Compile();
            _propertyName = ExpressionHelper.GetMemberName(selector);
            _propertyPredicate = predicate;
            _crossPropertyPredicate = null;
            _message = message;
            _messageFactory = null;
            _errorCode = errorCode;
            _severity = ValidationSeverity.Error;
        }

        public AsyncPropertyRule(
            Expression<Func<T, TProperty>> selector,
            Func<T, TProperty, CancellationToken, Task<bool>> predicate,
            string message,
            string? errorCode)
        {
            _selector = selector.Compile();
            _propertyName = ExpressionHelper.GetMemberName(selector);
            _propertyPredicate = null;
            _crossPropertyPredicate = predicate;
            _message = message;
            _messageFactory = null;
            _errorCode = errorCode;
            _severity = ValidationSeverity.Error;
        }

        public string Message
        {
            get => _message;
            set => _message = value;
        }

        public Func<T, TProperty, string>? MessageFactory
        {
            get => _messageFactory;
            set => _messageFactory = value;
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

        internal Task<bool> EvaluateValueAsync(T model, TProperty value, CancellationToken cancellationToken)
        {
            if (_crossPropertyPredicate != null)
                return _crossPropertyPredicate(model, value, cancellationToken);

            if (_propertyPredicate != null)
                return _propertyPredicate(value, cancellationToken);

            return Task.FromResult(true);
        }

        public async Task EvaluateAsync(T model, List<ValidationFailure> failures, CancellationToken cancellationToken)
        {
            if (WhenCondition != null && !WhenCondition(model))
                return;

            if (UnlessCondition != null && UnlessCondition(model))
                return;

            var value = _selector(model);
            bool valid;
            if (_crossPropertyPredicate != null)
                valid = await _crossPropertyPredicate(model, value, cancellationToken).ConfigureAwait(false);
            else if (_propertyPredicate != null)
                valid = await _propertyPredicate(value, cancellationToken).ConfigureAwait(false);
            else
                valid = true;

            if (!valid)
            {
                failures.Add(new ValidationFailure
                {
                    PropertyName = _propertyName,
                    ErrorMessage = _messageFactory != null ? _messageFactory(model, value) : _message,
                    ErrorCode = _errorCode,
                    AttemptedValue = value,
                    Severity = _severity
                });
            }
        }
    }
}
