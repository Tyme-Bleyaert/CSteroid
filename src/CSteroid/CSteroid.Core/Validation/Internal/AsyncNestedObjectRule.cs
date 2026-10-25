using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    internal sealed class AsyncNestedObjectRule<T, TProperty> : IAsyncRule<T> where TProperty : class
    {
        private readonly Func<T, TProperty> _selector;
        private readonly string _propertyName;
        private readonly Action<IRuleBuilder<TProperty>>? _configure;

        public AsyncNestedObjectRule(Expression<Func<T, TProperty>> selector, Action<IRuleBuilder<TProperty>>? configure)
        {
            _selector = selector.Compile();
            _propertyName = ExpressionHelper.GetMemberName(selector);
            _configure = configure;
        }

        public async Task EvaluateAsync(T model, List<ValidationFailure> failures, CancellationToken cancellationToken)
        {
            var value = _selector(model);
            if (value is null)
                return;

            ValidationResult result;
            if (_configure != null)
            {
                var builder = new AsyncRuleBuilder<TProperty>();
                _configure(builder);
                result = await builder.ValidateAsync(value, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var validator = NestedValidationHelper.ResolveValidator<TProperty>();
                result = validator is IAsyncValidator<TProperty> asyncValidator
                    ? await asyncValidator.ValidateAsync(value, cancellationToken).ConfigureAwait(false)
                    : validator.Validate(value);
            }

            NestedValidationHelper.AddPrefixed(failures, _propertyName + ".", result);
        }
    }
}
