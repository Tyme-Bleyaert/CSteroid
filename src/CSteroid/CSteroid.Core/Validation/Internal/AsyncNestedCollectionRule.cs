using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    internal sealed class AsyncNestedCollectionRule<T, TItem> : IAsyncRule<T> where TItem : class
    {
        private readonly Func<T, IEnumerable<TItem>> _selector;
        private readonly string _propertyName;
        private readonly Action<IRuleBuilder<TItem>>? _configure;

        public AsyncNestedCollectionRule(Expression<Func<T, IEnumerable<TItem>>> selector, Action<IRuleBuilder<TItem>>? configure)
        {
            _selector = selector.Compile();
            _propertyName = ExpressionHelper.GetMemberName(selector);
            _configure = configure;
        }

        public async Task EvaluateAsync(T model, List<ValidationFailure> failures, CancellationToken cancellationToken)
        {
            var items = _selector(model);
            if (items is null)
                return;

            AsyncRuleBuilder<TItem>? builder = null;
            IValidator<TItem>? validator = null;
            if (_configure != null)
            {
                builder = new AsyncRuleBuilder<TItem>();
                _configure(builder);
            }
            else
            {
                validator = NestedValidationHelper.ResolveValidator<TItem>();
            }

            var index = 0;
            foreach (var item in items)
            {
                if (item is not null)
                {
                    ValidationResult result;
                    if (builder != null)
                    {
                        result = await builder.ValidateAsync(item, cancellationToken).ConfigureAwait(false);
                    }
                    else if (validator is IAsyncValidator<TItem> asyncValidator)
                    {
                        result = await asyncValidator.ValidateAsync(item, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        result = validator!.Validate(item);
                    }

                    NestedValidationHelper.AddPrefixed(failures, $"{_propertyName}[{index}].", result);
                }

                index++;
            }
        }
    }
}
