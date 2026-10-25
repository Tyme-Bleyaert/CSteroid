using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CSteroid
{
    public sealed class RuleBuilder<T> : IRuleBuilder<T>
    {
        private readonly List<IRule<T>> _rules = new List<IRule<T>>();

        public IPropertyRuleBuilder<T, TProperty> For<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            return new PropertyRuleChain<T, TProperty>(this, selector, _rules);
        }

        public IRuleBuilder<T> Must(Func<T, bool> predicate, string errorMessage, string? errorCode = null)
        {
            _rules.Add(new ModelPredicateRule<T>(predicate, errorMessage, errorCode));
            return this;
        }

        public IRuleBuilder<T> ForNested<TProperty>(
            Expression<Func<T, TProperty>> selector,
            Action<IRuleBuilder<TProperty>>? configure = null)
            where TProperty : class
        {
            _rules.Add(new NestedObjectRule<T, TProperty>(selector, configure));
            return this;
        }

        public IPropertyRuleBuilder<T, TItem> ForEach<TItem>(Expression<Func<T, IEnumerable<TItem>>> selector)
        {
            return new CollectionPropertyRuleChain<T, TItem>(this, selector, _rules);
        }

        public IRuleBuilder<T> ForEachNested<TItem>(
            Expression<Func<T, IEnumerable<TItem>>> selector,
            Action<IRuleBuilder<TItem>>? configure = null)
            where TItem : class
        {
            _rules.Add(new NestedCollectionRule<T, TItem>(selector, configure));
            return this;
        }

        public ValidationResult Validate(T model)
        {
            var failures = new List<ValidationFailure>();
            foreach (var rule in _rules)
            {
                rule.Evaluate(model, failures);
            }

            return failures.Count == 0 ? ValidationResult.Success : new ValidationResult(failures);
        }
    }
}
