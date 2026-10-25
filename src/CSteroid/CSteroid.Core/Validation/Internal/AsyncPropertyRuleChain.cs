using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    internal sealed class AsyncPropertyRuleChain<T, TProperty> : IPropertyRuleBuilder<T, TProperty>
    {
        private readonly AsyncRuleBuilder<T> _parent;
        private readonly Expression<Func<T, TProperty>> _selector;
        private readonly List<IAsyncRule<T>> _rules;
        private readonly string _propertyName;
        private AsyncPropertyRule<T, TProperty>? _lastRule;

        public AsyncPropertyRuleChain(AsyncRuleBuilder<T> parent, Expression<Func<T, TProperty>> selector, List<IAsyncRule<T>> rules, string? propertyName = null)
        {
            _parent = parent;
            _selector = selector;
            _rules = rules;
            _propertyName = propertyName ?? ExpressionHelper.GetMemberName(selector);
        }

        public IRuleBuilder<T> And()
        {
            return _parent;
        }

        public IPropertyRuleBuilder<T, TProperty> EmailAddress(string? message = null)
        {
            EnsureStringType(nameof(EmailAddress));
            AddRule(async (value, ct) =>
            {
                var s = (string)(object)value!;
                return await Task.FromResult(!string.IsNullOrEmpty(s) && EmailAddressRegex.IsMatch(s)).ConfigureAwait(false);
            }, message ?? $"'{_propertyName}' must be a valid email address.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> Equal(TProperty value, string? message = null)
        {
            var comparer = EqualityComparer<TProperty>.Default;
            AddRule((actual, ct) => Task.FromResult(comparer.Equals(actual, value)), message ?? $"'{_propertyName}' must equal '{value}'.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> GreaterThan(TProperty value, string? message = null)
        {
            AddRule((actual, ct) => Task.FromResult(actual != null && Comparer<TProperty>.Default.Compare(actual, value) > 0), message ?? $"'{_propertyName}' must be greater than '{value}'.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> GreaterThanOrEqualTo(TProperty value, string? message = null)
        {
            AddRule((actual, ct) => Task.FromResult(actual != null && Comparer<TProperty>.Default.Compare(actual, value) >= 0), message ?? $"'{_propertyName}' must be greater than or equal to '{value}'.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> InclusiveBetween(TProperty min, TProperty max, string? message = null)
        {
            AddRule((actual, ct) => Task.FromResult(actual != null && Comparer<TProperty>.Default.Compare(actual, min) >= 0 && Comparer<TProperty>.Default.Compare(actual, max) <= 0), message ?? $"'{_propertyName}' must be between '{min}' and '{max}'.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> Length(int exactLength, string? message = null)
        {
            EnsureStringType(nameof(Length));
            AddRule((value, ct) =>
            {
                var s = (string)(object)value!;
                return Task.FromResult(s != null && s.Length == exactLength);
            }, message ?? $"'{_propertyName}' must be exactly {exactLength} characters in length.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> LessThan(TProperty value, string? message = null)
        {
            AddRule((actual, ct) => Task.FromResult(actual != null && Comparer<TProperty>.Default.Compare(actual, value) < 0), message ?? $"'{_propertyName}' must be less than '{value}'.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> LessThanOrEqualTo(TProperty value, string? message = null)
        {
            AddRule((actual, ct) => Task.FromResult(actual != null && Comparer<TProperty>.Default.Compare(actual, value) <= 0), message ?? $"'{_propertyName}' must be less than or equal to '{value}'.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> Matches(string pattern, string? message = null)
        {
            EnsureStringType(nameof(Matches));
            AddRule((value, ct) =>
            {
                var s = (string)(object)value!;
                return Task.FromResult(s != null && Regex.IsMatch(s, pattern));
            }, message ?? $"'{_propertyName}' is not in the correct format.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> MaxLength(int max, string? message = null)
        {
            EnsureStringType(nameof(MaxLength));
            AddRule((value, ct) =>
            {
                var s = (string)(object)value!;
                return Task.FromResult(s != null && s.Length <= max);
            }, message ?? $"'{_propertyName}' must be {max} characters or fewer.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> MinLength(int min, string? message = null)
        {
            EnsureStringType(nameof(MinLength));
            AddRule((value, ct) =>
            {
                var s = (string)(object)value!;
                return Task.FromResult(s != null && s.Length >= min);
            }, message ?? $"'{_propertyName}' must be at least {min} characters long.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate, string errorMessage, string? errorCode = null)
        {
            AddRule((value, ct) => Task.FromResult(predicate(value)), errorMessage, errorCode);
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> Must(Func<T, TProperty, bool> predicate, string errorMessage, string? errorCode = null)
        {
            AddRule((model, value, ct) => Task.FromResult(predicate(model, value)), errorMessage, errorCode);
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> MustAsync(Func<TProperty, CancellationToken, Task<bool>> predicate, string errorMessage, string? errorCode = null)
        {
            AddRule(predicate, errorMessage, errorCode);
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> MustAsync(Func<T, TProperty, CancellationToken, Task<bool>> predicate, string errorMessage, string? errorCode = null)
        {
            AddRule(predicate, errorMessage, errorCode);
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> NotEmpty(string? message = null)
        {
            AddRule((value, ct) =>
            {
                if (value == null)
                    return Task.FromResult(false);

                if (value is string s)
                    return Task.FromResult(!string.IsNullOrWhiteSpace(s));

                if (value is IEnumerable enumerable)
                    return Task.FromResult(enumerable.GetEnumerator().MoveNext());

                return Task.FromResult(true);
            }, message ?? $"'{_propertyName}' must not be empty.");

            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> NotEqual(TProperty value, string? message = null)
        {
            var comparer = EqualityComparer<TProperty>.Default;
            AddRule((actual, ct) => Task.FromResult(!comparer.Equals(actual, value)), message ?? $"'{_propertyName}' must not equal '{value}'.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> NotNull(string? message = null)
        {
            AddRule((value, ct) => Task.FromResult(value != null), message ?? $"'{_propertyName}' must not be null.");
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> Unless(Func<T, bool> condition)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("Unless must be called after a rule.");

            _lastRule.UnlessCondition = condition;
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> When(Func<T, bool> condition)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("When must be called after a rule.");

            _lastRule.WhenCondition = condition;
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> WithErrorCode(string code)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("WithErrorCode must be called after a rule.");

            _lastRule.ErrorCode = code;
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> WithMessage(string message)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("WithMessage must be called after a rule.");

            _lastRule.Message = message;
            _lastRule.MessageFactory = null;
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> WithMessage(Func<T, TProperty, string> messageFactory)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("WithMessage must be called after a rule.");

            _lastRule.MessageFactory = messageFactory;
            return this;
        }

        public IPropertyRuleBuilder<T, TProperty> WithSeverity(ValidationSeverity severity)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("WithSeverity must be called after a rule.");

            _lastRule.Severity = severity;
            return this;
        }

        private void AddRule(Func<TProperty, CancellationToken, Task<bool>> predicate, string message, string? errorCode = null)
        {
            var rule = new AsyncPropertyRule<T, TProperty>(_selector, predicate, message, errorCode);
            _rules.Add(rule);
            _lastRule = rule;
        }

        private void AddRule(Func<T, TProperty, CancellationToken, Task<bool>> predicate, string message, string? errorCode = null)
        {
            var rule = new AsyncPropertyRule<T, TProperty>(_selector, predicate, message, errorCode);
            _rules.Add(rule);
            _lastRule = rule;
        }

        private void EnsureStringType(string ruleName)
        {
            if (typeof(TProperty) != typeof(string))
                throw new InvalidOperationException($"{ruleName} can only be applied to string properties.");
        }
    }
}
