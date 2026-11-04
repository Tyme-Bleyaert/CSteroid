using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components.Forms;

namespace CSteroid.Blazor.Internal
{
    internal static class ValidationPathResolver
    {
        internal static FieldIdentifier Resolve(object rootModel, string? propertyName)
        {
            if (rootModel is null)
                throw new ArgumentNullException(nameof(rootModel));

            if (string.IsNullOrEmpty(propertyName))
                return new FieldIdentifier(rootModel, string.Empty);

            try
            {
                return TryResolve(rootModel, propertyName, out var field)
                    ? field
                    : new FieldIdentifier(rootModel, string.Empty);
            }
            catch (Exception exception) when (
                exception is AmbiguousMatchException
                || exception is ArgumentException
                || exception is IndexOutOfRangeException
                || exception is InvalidOperationException
                || exception is MemberAccessException
                || exception is NotSupportedException
                || exception is TargetInvocationException)
            {
                return new FieldIdentifier(rootModel, string.Empty);
            }
        }

        private static bool TryResolve(object rootModel, string path, out FieldIdentifier field)
        {
            object? current = rootModel;
            var position = 0;

            while (position < path.Length)
            {
                if (current is null)
                    break;

                var memberStart = position;
                while (position < path.Length && path[position] != '.' && path[position] != '[')
                {
                    if (path[position] == ']')
                        break;
                    position++;
                }

                var memberName = path.Substring(memberStart, position - memberStart);
                if (memberName.Length == 0)
                    break;

                var member = GetMember(current.GetType(), memberName);
                if (member is null || member is PropertyInfo property && property.GetIndexParameters().Length != 0)
                    break;

                if (position == path.Length)
                    return TryCreateField(current, memberName, out field);

                var memberValue = GetValue(member, current);
                if (path[position] == '.')
                {
                    current = memberValue;
                    position++;
                    if (position == path.Length)
                        return TryCreateField(current, string.Empty, out field);
                    continue;
                }

                var indexedValue = memberValue;
                object? indexOwner = null;
                var indexName = string.Empty;
                while (position < path.Length && path[position] == '[')
                {
                    var closingBracket = path.IndexOf(']', position + 1);
                    if (closingBracket < 0)
                        break;

                    var indexText = path.Substring(position + 1, closingBracket - position - 1);
                    indexOwner = indexedValue;
                    if (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                        || !TryGetIndexedValue(indexedValue, index, out indexedValue))
                    {
                        break;
                    }

                    indexName = index.ToString(CultureInfo.InvariantCulture);
                    position = closingBracket + 1;
                }

                if (position == path.Length)
                    return TryCreateField(indexOwner, indexName, out field);

                if (path[position] != '.')
                    break;

                current = indexedValue;
                position++;
                if (position == path.Length)
                    return TryCreateField(current, string.Empty, out field);
            }

            field = default;
            return false;
        }

        private static MemberInfo? GetMember(Type type, string name)
        {
            return (MemberInfo?)type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
                ?? type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
        }

        private static object? GetValue(MemberInfo member, object instance)
        {
            return member is PropertyInfo property
                ? property.GetValue(instance)
                : ((FieldInfo)member).GetValue(instance);
        }

        private static bool TryGetIndexedValue(object? source, int index, out object? value)
        {
            value = null;
            if (source is null)
                return false;

            if (source is IList list)
            {
                if (index >= list.Count)
                    return false;
                value = list[index];
                return true;
            }

            if (source is not IEnumerable enumerable)
                return false;

            var enumerator = enumerable.GetEnumerator();
            try
            {
                for (var current = 0; current <= index; current++)
                {
                    if (!enumerator.MoveNext())
                        return false;
                }
                value = enumerator.Current;
                return true;
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        private static bool TryCreateField(object? owner, string fieldName, out FieldIdentifier field)
        {
            if (owner is null || owner.GetType().IsValueType)
            {
                field = default;
                return false;
            }

            field = new FieldIdentifier(owner, fieldName);
            return true;
        }
    }
}
