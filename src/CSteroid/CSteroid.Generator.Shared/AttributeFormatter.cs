using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Shared.SourceTextHelper;

namespace CSteroid.Generator.Shared
{
    public static class AttributeFormatter
    {
        public static bool IsDataAnnotation(AttributeData attr)
        {
            return attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "System.ComponentModel.DataAnnotations";
        }

        public static bool IsSchemaAnnotation(AttributeData attr)
        {
            return attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "System.ComponentModel.DataAnnotations.Schema";
        }

        public static bool IsColumnAnnotation(AttributeData attr)
        {
            return attr.AttributeClass?.Name == "ColumnAttribute";
        }

        public static string FormatAttribute(AttributeData attr)
        {
            return FormatAttribute(attr, SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        /// <summary>
        /// Emits the attribute with a global:: qualified class name. Used by generic
        /// attribute forwarding, where the generated file has no using for the attribute's
        /// namespace (unlike the whitelisted DataAnnotations/schema passes, which emit a
        /// matching using directive).
        /// </summary>
        public static string FormatAttributeFullyQualified(AttributeData attr)
        {
            return FormatAttribute(attr, SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string FormatAttribute(AttributeData attr, SymbolDisplayFormat format)
        {
            var sb = new StringBuilder();
            sb.Append('[');

            var attrName = attr.AttributeClass?.ToDisplayString(format);
            if (attrName != null && attrName.EndsWith("Attribute", StringComparison.Ordinal))
                attrName = attrName.Substring(0, attrName.Length - "Attribute".Length);

            sb.Append(attrName ?? "Unknown");

            var args = new List<string>();

            foreach (var arg in attr.ConstructorArguments)
            {
                args.Add(FormatTypedConstant(arg));
            }

            foreach (var named in attr.NamedArguments.OrderBy(n => n.Key, StringComparer.Ordinal))
            {
                args.Add($"{named.Key} = {FormatTypedConstant(named.Value)}");
            }

            if (args.Count > 0)
            {
                sb.Append('(');
                sb.Append(string.Join(", ", args));
                sb.Append(')');
            }

            sb.Append(']');
            return sb.ToString();
        }

        public static string FormatTypedConstant(TypedConstant constant)
        {
            if (constant.IsNull)
                return "null";

            switch (constant.Kind)
            {
                case TypedConstantKind.Primitive:
                    return constant.Value switch
                    {
                        string s => $"\"{EscapeString(s)}\"",
                        char c => $"'{EscapeChar(c)}'",
                        bool b => b ? "true" : "false",
                        float f => $"{f}f",
                        double d => $"{d}d",
                        decimal m => $"{m}m",
                        long l => $"{l}L",
                        ulong ul => $"{ul}UL",
                        uint ui => $"{ui}u",
                        _ => constant.Value?.ToString() ?? "null"
                    };
                case TypedConstantKind.Array:
                    if (constant.IsNull)
                        return "null";
                    var values = constant.Values.Select(FormatTypedConstant);
                    return $"new[] {{ {string.Join(", ", values)} }}";
                case TypedConstantKind.Enum:
                    // Fully qualified: emitted attribute text must resolve in files that have
                    // no using for the attribute's (or enum's) namespace. Roslyn gives the
                    // underlying integral value in .Value, so resolve the field name first.
                    var enumType = constant.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var enumValue = constant.Value;
                    var enumMember = constant.Type?.GetMembers()
                        .OfType<IFieldSymbol>()
                        .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, enumValue));
                    var enumName = enumMember?.Name ?? enumValue?.ToString() ?? "0";
                    return $"{enumType}.{enumName}";
                case TypedConstantKind.Type:
                    return $"typeof({constant.Value})";
                default:
                    return "null";
            }
        }

        public static object GetAttributeConstructorArgument(AttributeData attr, int index)
        {
            if (attr.ConstructorArguments.Length > index)
                return attr.ConstructorArguments[index].Value;
            return null;
        }

        public static object GetAttributeNamedArgument(AttributeData attr, string name)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == name);
            if (named.Key == name)
                return named.Value.Value;
            return null;
        }
    }
}
