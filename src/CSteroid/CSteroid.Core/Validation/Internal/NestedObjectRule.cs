using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CSteroid
{
    internal sealed class NestedObjectRule<T, TProperty> : IRule<T> where TProperty : class
    {
        private readonly Func<T, TProperty> _selector;
        private readonly string _propertyName;
        private readonly Action<IRuleBuilder<TProperty>>? _configure;

        public NestedObjectRule(Expression<Func<T, TProperty>> selector, Action<IRuleBuilder<TProperty>>? configure)
        {
            _selector = selector.Compile();
            _propertyName = ExpressionHelper.GetMemberName(selector);
            _configure = configure;
        }

        public void Evaluate(T model, List<ValidationFailure> failures)
        {
            var value = _selector(model);
            if (value is null)
                return;

            ValidationResult result;
            if (_configure != null)
            {
                var builder = new RuleBuilder<TProperty>();
                _configure(builder);
                result = builder.Validate(value);
            }
            else
            {
                result = NestedValidationHelper.ResolveValidator<TProperty>().Validate(value);
            }

            NestedValidationHelper.AddPrefixed(failures, _propertyName + ".", result);
        }
    }
}
