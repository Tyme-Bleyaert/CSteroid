using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    internal sealed class CollectionPropertyRuleChain<T, TItem> : IPropertyRuleBuilder<T, TItem>
    {
        private static readonly Expression<Func<TItem, TItem>> IdentitySelector = x => x;

        private readonly RuleBuilder<T> _parent;
        private readonly Expression<Func<T, IEnumerable<TItem>>> _selector;
        private readonly List<IRule<T>> _rules;
        private readonly string _propertyName;
        private readonly PropertyRuleChain<TItem, TItem> _elementChain;
        private readonly List<IRule<TItem>> _elementRules;
        private CollectionElementRule<T, TItem>? _lastRule;

        public CollectionPropertyRuleChain(RuleBuilder<T> parent, Expression<Func<T, IEnumerable<TItem>>> selector, List<IRule<T>> rules)
        {
            _parent = parent;
            _selector = selector;
            _rules = rules;
            _propertyName = ExpressionHelper.GetMemberName(selector);
            _elementRules = new List<IRule<TItem>>();
            _elementChain = new PropertyRuleChain<TItem, TItem>(new RuleBuilder<TItem>(), IdentitySelector, _elementRules, _propertyName);
        }

        public IRuleBuilder<T> And()
        {
            return _parent;
        }

        public IPropertyRuleBuilder<T, TItem> EmailAddress(string? message = null)
        {
            _elementChain.EmailAddress(message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> Equal(TItem value, string? message = null)
        {
            _elementChain.Equal(value, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> GreaterThan(TItem value, string? message = null)
        {
            _elementChain.GreaterThan(value, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> GreaterThanOrEqualTo(TItem value, string? message = null)
        {
            _elementChain.GreaterThanOrEqualTo(value, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> InclusiveBetween(TItem min, TItem max, string? message = null)
        {
            _elementChain.InclusiveBetween(min, max, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> Length(int exactLength, string? message = null)
        {
            _elementChain.Length(exactLength, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> LessThan(TItem value, string? message = null)
        {
            _elementChain.LessThan(value, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> LessThanOrEqualTo(TItem value, string? message = null)
        {
            _elementChain.LessThanOrEqualTo(value, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> Matches(string pattern, string? message = null)
        {
            _elementChain.Matches(pattern, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> MaxLength(int max, string? message = null)
        {
            _elementChain.MaxLength(max, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> MinLength(int min, string? message = null)
        {
            _elementChain.MinLength(min, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> Must(Func<TItem, bool> predicate, string errorMessage, string? errorCode = null)
        {
            _elementChain.Must(predicate, errorMessage, errorCode);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> Must(Func<T, TItem, bool> predicate, string errorMessage, string? errorCode = null)
        {
            var rule = new CollectionElementRule<T, TItem>(_selector, _propertyName, predicate, errorMessage, errorCode);
            _rules.Add(rule);
            _lastRule = rule;
            return this;
        }

        public IPropertyRuleBuilder<T, TItem> MustAsync(Func<TItem, CancellationToken, Task<bool>> predicate, string errorMessage, string? errorCode = null)
        {
            throw new InvalidOperationException("MustAsync can only be used with asynchronous validators. Use ValidationType.Async to generate an async validator.");
        }

        public IPropertyRuleBuilder<T, TItem> MustAsync(Func<T, TItem, CancellationToken, Task<bool>> predicate, string message, string? errorCode = null)
        {
            throw new InvalidOperationException("MustAsync can only be used with asynchronous validators. Use ValidationType.Async to generate an async validator.");
        }

        public IPropertyRuleBuilder<T, TItem> NotEmpty(string? message = null)
        {
            _elementChain.NotEmpty(message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> NotEqual(TItem value, string? message = null)
        {
            _elementChain.NotEqual(value, message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> NotNull(string? message = null)
        {
            _elementChain.NotNull(message);
            return WrapLastElementRule();
        }

        public IPropertyRuleBuilder<T, TItem> Unless(Func<T, bool> condition)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("Unless must be called after a rule.");

            _lastRule.UnlessCondition = condition;
            return this;
        }

        public IPropertyRuleBuilder<T, TItem> When(Func<T, bool> condition)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("When must be called after a rule.");

            _lastRule.WhenCondition = condition;
            return this;
        }

        public IPropertyRuleBuilder<T, TItem> WithErrorCode(string code)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("WithErrorCode must be called after a rule.");

            _lastRule.ErrorCode = code;
            return this;
        }

        public IPropertyRuleBuilder<T, TItem> WithMessage(string message)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("WithMessage must be called after a rule.");

            _lastRule.Message = message;
            _lastRule.MessageFactory = null;
            return this;
        }

        public IPropertyRuleBuilder<T, TItem> WithMessage(Func<T, TItem, string> messageFactory)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("WithMessage must be called after a rule.");

            _lastRule.MessageFactory = messageFactory;
            return this;
        }

        public IPropertyRuleBuilder<T, TItem> WithSeverity(ValidationSeverity severity)
        {
            if (_lastRule == null)
                throw new InvalidOperationException("WithSeverity must be called after a rule.");

            _lastRule.Severity = severity;
            return this;
        }

        private CollectionPropertyRuleChain<T, TItem> WrapLastElementRule()
        {
            var index = _elementRules.Count - 1;
            var elementRule = (PropertyRule<TItem, TItem>)_elementRules[index];
            _elementRules.RemoveAt(index);
            var rule = new CollectionElementRule<T, TItem>(_selector, _propertyName, elementRule);
            _rules.Add(rule);
            _lastRule = rule;
            return this;
        }
    }
}
