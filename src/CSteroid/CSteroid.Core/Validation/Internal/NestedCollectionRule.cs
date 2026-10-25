using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CSteroid
{
    internal sealed class NestedCollectionRule<T, TItem> : IRule<T> where TItem : class
    {
        private readonly Func<T, IEnumerable<TItem>> _selector;
        private readonly string _propertyName;
        private readonly Action<IRuleBuilder<TItem>>? _configure;

        public NestedCollectionRule(Expression<Func<T, IEnumerable<TItem>>> selector, Action<IRuleBuilder<TItem>>? configure)
        {
            _selector = selector.Compile();
            _propertyName = ExpressionHelper.GetMemberName(selector);
            _configure = configure;
        }

        public void Evaluate(T model, List<ValidationFailure> failures)
        {
            var items = _selector(model);
            if (items is null)
                return;

            RuleBuilder<TItem>? builder = null;
            IValidator<TItem>? validator = null;
            if (_configure != null)
            {
                builder = new RuleBuilder<TItem>();
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
                    var result = builder != null ? builder.Validate(item) : validator!.Validate(item);
                    NestedValidationHelper.AddPrefixed(failures, $"{_propertyName}[{index}].", result);
                }

                index++;
            }
        }
    }
}
