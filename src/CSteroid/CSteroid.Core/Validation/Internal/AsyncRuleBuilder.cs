using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    public sealed class AsyncRuleBuilder<T> : IRuleBuilder<T>
    {
        private readonly List<IAsyncRule<T>> _rules = new List<IAsyncRule<T>>();

        public IPropertyRuleBuilder<T, TProperty> For<TProperty>(Expression<Func<T, TProperty>> selector)
        {
            return new AsyncPropertyRuleChain<T, TProperty>(this, selector, _rules);
        }

        public IRuleBuilder<T> Must(Func<T, bool> predicate, string errorMessage, string? errorCode = null)
        {
            _rules.Add(new AsyncModelPredicateRule<T>(predicate, errorMessage, errorCode));
            return this;
        }

        public IRuleBuilder<T> ForNested<TProperty>(
            Expression<Func<T, TProperty>> selector,
            Action<IRuleBuilder<TProperty>>? configure = null)
            where TProperty : class
        {
            _rules.Add(new AsyncNestedObjectRule<T, TProperty>(selector, configure));
            return this;
        }

        public IPropertyRuleBuilder<T, TItem> ForEach<TItem>(Expression<Func<T, IEnumerable<TItem>>> selector)
        {
            return new AsyncCollectionPropertyRuleChain<T, TItem>(this, selector, _rules);
        }

        public IRuleBuilder<T> ForEachNested<TItem>(
            Expression<Func<T, IEnumerable<TItem>>> selector,
            Action<IRuleBuilder<TItem>>? configure = null)
            where TItem : class
        {
            _rules.Add(new AsyncNestedCollectionRule<T, TItem>(selector, configure));
            return this;
        }

        public async Task<ValidationResult> ValidateAsync(T model, CancellationToken cancellationToken)
        {
            var failures = new List<ValidationFailure>();
            foreach (var rule in _rules)
            {
                await rule.EvaluateAsync(model, failures, cancellationToken).ConfigureAwait(false);
            }

            return failures.Count == 0 ? ValidationResult.Success : new ValidationResult(failures);
        }
    }
}
