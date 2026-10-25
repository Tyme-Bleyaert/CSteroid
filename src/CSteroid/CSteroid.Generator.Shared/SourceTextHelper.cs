using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSteroid.Generator.Shared
{
    public static class SourceTextHelper
    {
        public static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        public static string EscapeChar(char c)
        {
            return c switch
            {
                '\'' => "\\'",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString()
            };
        }

        public static string FormatLiteral(object value)
        {
            if (value is null)
                return "null";
            if (value is string s)
                return $"\"{EscapeString(s)}\"";
            if (value is char c)
                return $"'{EscapeChar(c)}'";
            if (value is bool b)
                return b ? "true" : "false";
            if (value is float f)
                return $"{f}f";
            if (value is double d)
                return $"{d}d";
            if (value is decimal m)
                return $"{m}m";
            if (value is long l)
                return $"{l}L";
            if (value is ulong ul)
                return $"{ul}UL";
            if (value is uint ui)
                return $"{ui}u";
            return value.ToString() ?? "null";
        }

        public static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        public static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 &&
                        (!char.IsUpper(value[i - 1]) ||
                         (i < value.Length - 1 && char.IsLower(value[i + 1]))))
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static SourceText ToSourceText(string text)
        {
            return SourceText.From(text.Replace("\r\n", "\n"), Encoding.UTF8);
        }

        public static string GetPropertyDocumentation(IPropertySymbol prop, INamedTypeSymbol interfaceSymbol)
        {
            var xml = prop.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(xml) && !xml.Contains("inheritdoc", StringComparison.OrdinalIgnoreCase))
                return xml.Trim();

            foreach (var baseIface in interfaceSymbol.AllInterfaces)
            {
                var baseProp = baseIface.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => p.Name == prop.Name);
                if (baseProp is null)
                    continue;

                var baseXml = baseProp.GetDocumentationCommentXml();
                if (!string.IsNullOrWhiteSpace(baseXml))
                    return baseXml.Trim();
            }

            return string.IsNullOrWhiteSpace(xml) ? string.Empty : xml.Trim();
        }

        public static void EmitXmlDoc(StringBuilder sb, string xml, string indent)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return;

            foreach (var rawLine in xml.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                sb.AppendLine($"{indent}/// {line}");
            }
        }
    }
}
