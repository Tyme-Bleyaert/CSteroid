using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CSteroid.Generator.Shared.AttributeFormatter;
using static CSteroid.Generator.Shared.GeneratorConstants;
using static CSteroid.Generator.Shared.SourceTextHelper;

namespace CSteroid.Generator.Shared
{
    /// <summary>
    /// Applies the attribute forwarding policy for one generated property. Each family has
    /// one pass, and the generic pass excludes families already handled above it.
    /// </summary>
    internal static class AttributeEmitter
    {
        public static void EmitAttributes(StringBuilder sb, ModelInfo info, PropertySpec prop)
        {
            EmitDataAnnotations(sb, info, prop);
            EmitSchemaAnnotations(sb, info, prop);
            EmitCosmosAttributes(sb, info, prop);
            EmitMongoAttributes(sb, info, prop);
            EmitPostgreAttributes(sb, info, prop);
            EmitForwardedAttributes(sb, info, prop);
        }

        private static void EmitDataAnnotations(StringBuilder sb, ModelInfo info, PropertySpec prop)
        {
            if ((info.ValidationType & ValidationTypeDataAnnotations) == 0)
                return;

            foreach (var attr in Ordered(prop))
            {
                if (IsDataAnnotation(attr))
                    sb.AppendLine($"        {attr.MinimallyQualifiedText}");
            }
        }

        private static void EmitSchemaAnnotations(StringBuilder sb, ModelInfo info, PropertySpec prop)
        {
            if ((info.OrmTarget & (OrmTargetEFCore | OrmTargetDapper)) == 0
                || (info.OrmTarget & OrmTargetPostgreSQL) != 0)
                return;

            foreach (var attr in Ordered(prop))
            {
                if (IsSchemaAnnotation(attr)
                    && ((info.OrmTarget & OrmTargetEFCore) != 0 || IsColumnAnnotation(attr)))
                {
                    sb.AppendLine($"        {attr.MinimallyQualifiedText}");
                }
            }
        }

        private static void EmitCosmosAttributes(StringBuilder sb, ModelInfo info, PropertySpec prop)
        {
            if ((info.OrmTarget & OrmTargetCosmosDB) == 0)
                return;

            string jsonName = IsCosmosIdProperty(prop) ? "id" : ToCamelCase(prop.Name);
            sb.AppendLine($"        [JsonPropertyName(\"{jsonName}\")]");

            if (!string.IsNullOrEmpty(info.PartitionKey) && prop.Name == info.PartitionKey)
                sb.AppendLine("        [global::CSteroid.CosmosPartitionKey]");
        }

        private static void EmitMongoAttributes(StringBuilder sb, ModelInfo info, PropertySpec prop)
        {
            if ((info.OrmTarget & OrmTargetMongoDB) == 0)
                return;

            if (IsMongoIdProperty(prop))
                sb.AppendLine("        [BsonId]");

            string elementName = ToCamelCase(prop.Name);
            sb.AppendLine($"        [BsonElement(\"{elementName}\")]");
        }

        private static void EmitPostgreAttributes(StringBuilder sb, ModelInfo info, PropertySpec prop)
        {
            if ((info.OrmTarget & OrmTargetPostgreSQL) == 0)
                return;

            var columnAttr = prop.Attributes.FirstOrDefault(IsColumnAnnotation);
            bool isKey = IsPostgreKeyProperty(prop);
            bool isJsonb = IsPostgreJsonbProperty(prop);

            if (isKey)
            {
                sb.AppendLine("        [Key]");
                sb.AppendLine("        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
            }

            sb.AppendLine($"        {FormatPostgreColumn(prop, columnAttr, isJsonb)}");
        }

        private static void EmitForwardedAttributes(StringBuilder sb, ModelInfo info, PropertySpec prop)
        {
            if (!info.ForwardPropertyAttributes)
                return;

            foreach (var attr in Ordered(prop))
            {
                if (ShouldForwardAttribute(attr))
                    sb.AppendLine($"        {attr.FullyQualifiedText}");
            }
        }

        private static bool ShouldForwardAttribute(AttributeSpec attr)
        {
            if (IsDataAnnotation(attr) || IsSchemaAnnotation(attr))
                return false;
            if (attr.Namespace == "CSteroid")
                return false;
            if (attr.IsImplicitlyDeclared || attr.Namespace == "System.Runtime.CompilerServices")
                return false;

            return attr.Name.Length > 0;
        }

        private static IOrderedEnumerable<AttributeSpec> Ordered(PropertySpec prop)
        {
            return prop.Attributes.OrderBy(a => a.DisplayName, StringComparer.Ordinal);
        }

        internal static bool IsPostgreKeyProperty(PropertySpec prop)
        {
            return IsCosmosIdProperty(prop);
        }

        private static bool IsCosmosIdProperty(PropertySpec prop)
        {
            if (prop.Name == "Id")
                return true;

            return prop.Attributes.Any(a =>
                a.Namespace == "System.ComponentModel.DataAnnotations.Schema"
                && a.Name == "KeyAttribute");
        }

        private static bool IsMongoIdProperty(PropertySpec prop)
        {
            return IsCosmosIdProperty(prop);
        }

        private static bool IsPostgreJsonbProperty(PropertySpec prop)
        {
            return prop.Attributes.Any(a => a.DisplayName == "CSteroid.PostgreJsonbAttribute");
        }

        private static string FormatPostgreColumn(PropertySpec prop, AttributeSpec columnAttr, bool isJsonb)
        {
            var sb = new StringBuilder();
            sb.Append("[Column(");

            var args = new List<string>();
            string name = columnAttr is null ? null : GetAttributeConstructorArgument(columnAttr, 0)?.StringValue;
            if (name == null)
                name = ToSnakeCase(prop.Name);

            args.Add($"\"{EscapeString(name)}\"");

            if (isJsonb)
                args.Add("TypeName = \"jsonb\"");

            if (columnAttr is not null)
            {
                foreach (var named in columnAttr.NamedArguments)
                {
                    if (named.Name == "TypeName")
                        continue;
                    args.Add($"{named.Name} = {named.Value.Literal}");
                }
            }

            sb.Append(string.Join(", ", args));
            sb.Append(")]");
            return sb.ToString();
        }
    }
}
