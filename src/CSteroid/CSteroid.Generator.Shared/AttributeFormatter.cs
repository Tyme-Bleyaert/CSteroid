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
        public static bool IsDataAnnotation(AttributeSpec attr)
        {
            return attr.Namespace == "System.ComponentModel.DataAnnotations";
        }

        public static bool IsSchemaAnnotation(AttributeSpec attr)
        {
            return attr.Namespace == "System.ComponentModel.DataAnnotations.Schema";
        }

        public static bool IsColumnAnnotation(AttributeSpec attr)
        {
            return attr.Name == "ColumnAttribute";
        }

        public static string FormatAttribute(AttributeData attr)
        {
            return FormatAttribute(attr, SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        /// <summary>
        /// Emits the attribute with a global:: qualified class name for generic forwarding.
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
                args.Add(FormatTypedConstant(arg));

            foreach (var named in attr.NamedArguments.OrderBy(n => n.Key, StringComparer.Ordinal))
                args.Add($"{named.Key} = {FormatTypedConstant(named.Value)}");

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
                    return FormatLiteral(constant.Value);
                case TypedConstantKind.Array:
                    var values = constant.Values.Select(FormatTypedConstant);
                    return $"new[] {{ {string.Join(", ", values)} }}";
                case TypedConstantKind.Enum:
                    // Roslyn stores the underlying integral value. Resolve the field name so
                    // emitted attributes remain readable and fully qualified.
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

        public static AttributeArgumentSpec GetAttributeConstructorArgument(AttributeSpec attr, int index)
        {
            return index < attr.ConstructorArguments.Count ? attr.ConstructorArguments[index] : null;
        }

        public static AttributeArgumentSpec GetAttributeNamedArgument(AttributeSpec attr, string name)
        {
            return attr.NamedArguments.FirstOrDefault(n => n.Name == name)?.Value;
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
