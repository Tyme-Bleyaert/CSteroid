using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Shared.AttributeFormatter;
using static CSteroid.Generator.Shared.GeneratorConstants;
using static CSteroid.Generator.Shared.SourceTextHelper;

namespace CSteroid.Generator.Shared
{
    /// <summary>
    /// Decides which attributes a generated property carries for a given target and emits
    /// them. The passes run in a fixed order; each pass owns one attribute family, so a
    /// family handled by an earlier pass is excluded from the generic forwarding pass to
    /// avoid duplicates.
    /// </summary>
    internal static class AttributeEmitter
    {
        public static void EmitAttributes(StringBuilder sb, ModelInfo info, IPropertySymbol prop)
        {
            EmitDataAnnotations(sb, info, prop);
            EmitSchemaAnnotations(sb, info, prop);
            EmitCosmosAttributes(sb, info, prop);
            EmitMongoAttributes(sb, info, prop);
            EmitPostgreAttributes(sb, info, prop);
            EmitForwardedAttributes(sb, info, prop);
        }

        private static void EmitDataAnnotations(StringBuilder sb, ModelInfo info, IPropertySymbol prop)
        {
            if ((info.ValidationType & ValidationTypeDataAnnotations) == 0)
                return;

            foreach (var attr in Ordered(prop))
            {
                if (IsDataAnnotation(attr))
                    sb.AppendLine($"        {FormatAttribute(attr)}");
            }
        }

        private static void EmitSchemaAnnotations(StringBuilder sb, ModelInfo info, IPropertySymbol prop)
        {
            if ((info.OrmTarget & (OrmTargetEFCore | OrmTargetDapper)) == 0
                || (info.OrmTarget & OrmTargetPostgreSQL) != 0)
                return;

            foreach (var attr in Ordered(prop))
            {
                if (IsSchemaAnnotation(attr)
                    && ((info.OrmTarget & OrmTargetEFCore) != 0 || IsColumnAnnotation(attr)))
                {
                    sb.AppendLine($"        {FormatAttribute(attr)}");
                }
            }
        }

        private static void EmitCosmosAttributes(StringBuilder sb, ModelInfo info, IPropertySymbol prop)
        {
            if ((info.OrmTarget & OrmTargetCosmosDB) == 0)
                return;

            string jsonName = IsCosmosIdProperty(prop) ? "id" : ToCamelCase(prop.Name);
            sb.AppendLine($"        [JsonPropertyName(\"{jsonName}\")]");

            if (!string.IsNullOrEmpty(info.PartitionKey) && prop.Name == info.PartitionKey)
            {
                sb.AppendLine($"        [global::CSteroid.CosmosPartitionKey]");
            }
        }

        private static void EmitMongoAttributes(StringBuilder sb, ModelInfo info, IPropertySymbol prop)
        {
            if ((info.OrmTarget & OrmTargetMongoDB) == 0)
                return;

            if (IsMongoIdProperty(prop))
            {
                sb.AppendLine("        [BsonId]");
            }

            string elementName = ToCamelCase(prop.Name);
            sb.AppendLine($"        [BsonElement(\"{elementName}\")]");
        }

        private static void EmitPostgreAttributes(StringBuilder sb, ModelInfo info, IPropertySymbol prop)
        {
            if ((info.OrmTarget & OrmTargetPostgreSQL) == 0)
                return;

            var columnAttr = prop.GetAttributes().FirstOrDefault(IsColumnAnnotation);
            bool isKey = IsPostgreKeyProperty(prop);
            bool isJsonb = IsPostgreJsonbProperty(prop);

            if (isKey)
            {
                sb.AppendLine("        [Key]");
                sb.AppendLine("        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
            }

            sb.AppendLine($"        {FormatPostgreColumn(prop, columnAttr, isJsonb)}");
        }

        // Opt-in generic forwarding: everything not already handled above and not
        // CSteroid-internal / compiler metadata lands on this target's property.
        // Only enabled per target (ForwardPropertyAttributes) so shared interfaces
        // can forward EF-specific attributes to the entity without breaking DTO
        // targets whose projects do not reference those assemblies.
        private static void EmitForwardedAttributes(StringBuilder sb, ModelInfo info, IPropertySymbol prop)
        {
            if (!info.ForwardPropertyAttributes)
                return;

            foreach (var attr in Ordered(prop))
            {
                if (ShouldForwardAttribute(attr))
                {
                    sb.AppendLine($"        {FormatAttributeFullyQualified(attr)}");
                }
            }
        }

        private static bool ShouldForwardAttribute(AttributeData attr)
        {
            var cls = attr.AttributeClass;
            if (cls is null)
                return false;

            // DataAnnotations/schema are owned by the dedicated passes above (when enabled);
            // forwarding them here as well would duplicate the attribute on the property.
            if (IsDataAnnotation(attr) || IsSchemaAnnotation(attr))
                return false;

            // CSteroid's own attributes are generator input, never model output.
            if (cls.ContainingNamespace?.ToDisplayString() == "CSteroid")
                return false;

            // Compiler-emitted metadata (NullableContext, IsExternalInit, RefSafetyRules, ...)
            // must not be re-emitted as source.
            if (cls.IsImplicitlyDeclared || cls.ContainingNamespace?.ToDisplayString() == "System.Runtime.CompilerServices")
                return false;

            return true;
        }

        private static IOrderedEnumerable<AttributeData> Ordered(IPropertySymbol prop)
        {
            return prop.GetAttributes().OrderBy(a => a.AttributeClass?.ToDisplayString() ?? string.Empty, StringComparer.Ordinal);
        }

        // --- shared property predicates (also used by ModelSourceBuilder's using-directive logic) ---

        internal static bool IsPostgreKeyProperty(IPropertySymbol prop)
        {
            return IsCosmosIdProperty(prop);
        }

        private static bool IsCosmosIdProperty(IPropertySymbol prop)
        {
            if (prop.Name == "Id")
                return true;

            return prop.GetAttributes().Any(a =>
                a.AttributeClass?.ContainingNamespace?.ToDisplayString() == "System.ComponentModel.DataAnnotations.Schema" &&
                a.AttributeClass?.Name == "KeyAttribute");
        }

        private static bool IsMongoIdProperty(IPropertySymbol prop)
        {
            return IsCosmosIdProperty(prop);
        }

        private static bool IsPostgreJsonbProperty(IPropertySymbol prop)
        {
            return prop.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "CSteroid.PostgreJsonbAttribute");
        }

        private static string FormatPostgreColumn(IPropertySymbol prop, AttributeData columnAttr, bool isJsonb)
        {
            var sb = new StringBuilder();
            sb.Append("[Column(");

            var args = new List<string>();
            string name = columnAttr != null ? GetAttributeConstructorArgument(columnAttr, 0) as string : null;
            if (name == null)
            {
                name = ToSnakeCase(prop.Name);
            }

            args.Add($"\"{EscapeString(name)}\"");

            if (isJsonb)
            {
                args.Add("TypeName = \"jsonb\"");
            }

            if (columnAttr != null)
            {
                foreach (var named in columnAttr.NamedArguments)
                {
                    if (named.Key == "TypeName")
                        continue;
                    args.Add($"{named.Key} = {FormatTypedConstant(named.Value)}");
                }
            }

            sb.Append(string.Join(", ", args));
            sb.Append(")]");
            return sb.ToString();
        }
    }
}
