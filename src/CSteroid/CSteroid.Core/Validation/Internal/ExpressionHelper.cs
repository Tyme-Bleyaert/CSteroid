using System;
using System.Linq.Expressions;

namespace CSteroid
{
    internal static class ExpressionHelper
    {
        public static string GetMemberName<T, TProperty>(Expression<Func<T, TProperty>> expression)
        {
            if (expression.Body is MemberExpression member)
                return member.Member.Name;

            if (expression.Body is UnaryExpression unary && unary.Operand is MemberExpression operandMember)
                return operandMember.Member.Name;

            return "?";
        }
    }
}
