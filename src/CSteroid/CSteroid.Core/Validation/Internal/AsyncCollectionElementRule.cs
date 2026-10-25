using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    internal sealed class AsyncCollectionElementRule<T, TItem> : IAsyncRule<T>
    {
        private readonly Func<T, IEnumerable<TItem>> _selector;
        private readonly string _propertyName;
        private readonly AsyncPropertyRule<TItem, TItem>? _elementRule;
        private readonly Func<T, TItem, CancellationToken, Task<bool>>? _crossPredicate;
        private string _message;
        private Func<T, TItem, string>? _messageFactory;
        private string? _errorCode;
        private ValidationSeverity _severity;

        public AsyncCollectionElementRule(
            Expression<Func<T, IEnumerable<TItem>>> selector,
            string propertyName,
            AsyncPropertyRule<TItem, TItem> elementRule)
        {
            _selector = selector.Compile();
            _propertyName = propertyName;
            _elementRule = elementRule;
            _crossPredicate = null;
            _message = elementRule.Message;
            _messageFactory = null;
            _errorCode = elementRule.ErrorCode;
            _severity = elementRule.Severity;
        }

        public AsyncCollectionElementRule(
            Expression<Func<T, IEnumerable<TItem>>> selector,
            string propertyName,
            Func<T, TItem, CancellationToken, Task<bool>> crossPredicate,
            string message,
            string? errorCode)
        {
            _selector = selector.Compile();
            _propertyName = propertyName;
            _elementRule = null;
            _crossPredicate = crossPredicate;
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

        public Func<T, TItem, string>? MessageFactory
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

        public async Task EvaluateAsync(T model, List<ValidationFailure> failures, CancellationToken cancellationToken)
        {
            if (WhenCondition != null && !WhenCondition(model))
                return;

            if (UnlessCondition != null && UnlessCondition(model))
                return;

            var items = _selector(model);
            if (items is null)
                return;

            var index = 0;
            foreach (var item in items)
            {
                var valid = _crossPredicate != null
                    ? await _crossPredicate(model, item, cancellationToken).ConfigureAwait(false)
                    : await _elementRule!.EvaluateValueAsync(item, item, cancellationToken).ConfigureAwait(false);

                if (!valid)
                {
                    failures.Add(new ValidationFailure
                    {
                        PropertyName = $"{_propertyName}[{index}]",
                        ErrorMessage = _messageFactory != null ? _messageFactory(model, item) : _message,
                        ErrorCode = _errorCode,
                        AttemptedValue = item,
                        Severity = _severity
                    });
                }

                index++;
            }
        }
    }
}
