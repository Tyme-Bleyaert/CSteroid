using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CSteroid
{
    internal sealed class PropertyRule<T, TProperty> : IRule<T>
    {
        private readonly Func<T, TProperty> _selector;
        private readonly string _propertyName;
        private readonly Func<TProperty, bool>? _propertyPredicate;
        private readonly Func<T, TProperty, bool>? _crossPropertyPredicate;
        private string _message;
        private Func<T, TProperty, string>? _messageFactory;
        private string? _errorCode;
        private ValidationSeverity _severity;

        public PropertyRule(
            Expression<Func<T, TProperty>> selector,
            Func<TProperty, bool> predicate,
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

        public PropertyRule(
            Expression<Func<T, TProperty>> selector,
            Func<T, TProperty, bool> predicate,
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

        internal bool EvaluateValue(T model, TProperty value)
        {
            if (_crossPropertyPredicate != null)
                return _crossPropertyPredicate(model, value);

            if (_propertyPredicate != null)
                return _propertyPredicate(value);

            return true;
        }

        public void Evaluate(T model, List<ValidationFailure> failures)
        {
            if (WhenCondition != null && !WhenCondition(model))
                return;

            if (UnlessCondition != null && UnlessCondition(model))
                return;

            var value = _selector(model);
            bool valid;
            if (_crossPropertyPredicate != null)
                valid = _crossPropertyPredicate(model, value);
            else if (_propertyPredicate != null)
                valid = _propertyPredicate(value);
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
