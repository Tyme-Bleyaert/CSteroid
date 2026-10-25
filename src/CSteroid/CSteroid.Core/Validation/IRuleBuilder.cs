using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    public interface IRuleBuilder<T>
    {
        IPropertyRuleBuilder<T, TProperty> For<TProperty>(Expression<Func<T, TProperty>> selector);

        IRuleBuilder<T> Must(Func<T, bool> predicate, string errorMessage, string? errorCode = null);

        IRuleBuilder<T> ForNested<TProperty>(
            Expression<Func<T, TProperty>> selector,
            Action<IRuleBuilder<TProperty>>? configure = null)
            where TProperty : class;

        IPropertyRuleBuilder<T, TItem> ForEach<TItem>(
            Expression<Func<T, IEnumerable<TItem>>> selector);

        IRuleBuilder<T> ForEachNested<TItem>(
            Expression<Func<T, IEnumerable<TItem>>> selector,
            Action<IRuleBuilder<TItem>>? configure = null)
            where TItem : class;
    }

    public interface IPropertyRuleBuilder<T, TProperty>
    {
        IPropertyRuleBuilder<T, TProperty> NotNull(string? message = null);

        IPropertyRuleBuilder<T, TProperty> NotEmpty(string? message = null);

        IPropertyRuleBuilder<T, TProperty> MaxLength(int max, string? message = null);

        IPropertyRuleBuilder<T, TProperty> MinLength(int min, string? message = null);

        IPropertyRuleBuilder<T, TProperty> Length(int exactLength, string? message = null);

        IPropertyRuleBuilder<T, TProperty> Matches(string pattern, string? message = null);

        IPropertyRuleBuilder<T, TProperty> EmailAddress(string? message = null);

        IPropertyRuleBuilder<T, TProperty> Equal(TProperty value, string? message = null);

        IPropertyRuleBuilder<T, TProperty> NotEqual(TProperty value, string? message = null);

        IPropertyRuleBuilder<T, TProperty> GreaterThan(TProperty value, string? message = null);

        IPropertyRuleBuilder<T, TProperty> GreaterThanOrEqualTo(TProperty value, string? message = null);

        IPropertyRuleBuilder<T, TProperty> LessThan(TProperty value, string? message = null);

        IPropertyRuleBuilder<T, TProperty> LessThanOrEqualTo(TProperty value, string? message = null);

        IPropertyRuleBuilder<T, TProperty> InclusiveBetween(TProperty min, TProperty max, string? message = null);

        IPropertyRuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate, string errorMessage, string? errorCode = null);

        IPropertyRuleBuilder<T, TProperty> Must(Func<T, TProperty, bool> predicate, string errorMessage, string? errorCode = null);

        IPropertyRuleBuilder<T, TProperty> MustAsync(Func<TProperty, CancellationToken, Task<bool>> predicate, string errorMessage, string? errorCode = null);

        IPropertyRuleBuilder<T, TProperty> MustAsync(Func<T, TProperty, CancellationToken, Task<bool>> predicate, string message, string? errorCode = null);

        IPropertyRuleBuilder<T, TProperty> WithMessage(string message);

        IPropertyRuleBuilder<T, TProperty> WithMessage(Func<T, TProperty, string> messageFactory);

        IPropertyRuleBuilder<T, TProperty> WithErrorCode(string code);

        IPropertyRuleBuilder<T, TProperty> WithSeverity(ValidationSeverity severity);

        IPropertyRuleBuilder<T, TProperty> When(Func<T, bool> condition);

        IPropertyRuleBuilder<T, TProperty> Unless(Func<T, bool> condition);

        IRuleBuilder<T> And();
    }
}
