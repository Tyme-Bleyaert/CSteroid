using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Shared;
using static CSteroid.Generator.Shared.GeneratorConstants;
using static CSteroid.Generator.Shared.NestedTargets;
using static CSteroid.Generator.Shared.OutputConventions;

namespace CSteroid.Generator.Mapping
{
    /// <summary>
    /// Cast-operator emission: implicit/explicit operators live inside the generated
    /// (partial) source model, one file per model namespace.
    /// </summary>
    public partial class CSteroidMappingGenerator
    {
        private static Dictionary<string, string> GenerateCastOperatorsByNamespace(List<IGrouping<ISymbol, ModelInfo>> byInterface, string mappingNamespace, HashSet<string> targetNames, Dictionary<string, HashSet<string>> nestedGraph, Compilation compilation, EmissionPolicy policy)
        {
            var fileBuilders = new Dictionary<string, (StringBuilder Builder, HashSet<string> RequiredNamespaces)>(StringComparer.Ordinal);

            foreach (var group in byInterface)
            {
                var models = group.OrderBy(m => m.Target, StringComparer.Ordinal).ToList();
                if (models.Count < 2)
                    continue;

                foreach (var src in models)
                {
                    // Cast operators must be declared inside the source type, so only
                    // locally-generated sources can carry them.
                    if (!policy.IsLocal(src))
                        continue;

                    var srcNamespace = GetModelNamespace(src);
                    var destinations = new List<ModelInfo>();

                    foreach (var dst in models)
                    {
                        if (src.Target == dst.Target)
                            continue;

                        var commonProps = GetCommonProperties(src, dst);
                        if (commonProps.Count == 0)
                            continue;

                        bool hasCast = (src.MappingTypes & MappingTypesImplicitCast) != 0 || (src.MappingTypes & MappingTypesExplicitCast) != 0;

                        // The destination type must be nameable here: generated locally or by
                        // a referenced project (metadata-lib mode).
                        if (hasCast && policy.IsResolvable(dst, compilation))
                        {
                            destinations.Add(dst);
                        }
                    }

                    if (destinations.Count == 0)
                        continue;

                    if (!fileBuilders.TryGetValue(srcNamespace, out var file))
                    {
                        file = (new StringBuilder(), new HashSet<string>(StringComparer.Ordinal));
                        fileBuilders[srcNamespace] = file;
                    }

                    string declarationKeyword = src.DeclarationType switch
                    {
                        DeclarationTypeRecord => "record",
                        DeclarationTypeStruct => "struct",
                        _ => "class"
                    };

                    file.Builder.AppendLine($"    public partial {declarationKeyword} {src.Target}");
                    file.Builder.AppendLine("    {");

                    foreach (var dst in destinations.OrderBy(d => d.Target, StringComparer.Ordinal))
                    {
                        var dstNamespace = GetModelNamespace(dst);
                        if (dstNamespace != srcNamespace)
                        {
                            file.RequiredNamespaces.Add(dstNamespace);
                        }

                        bool isImplicit = (src.MappingTypes & MappingTypesImplicitCast) != 0;
                        string opKeyword = isImplicit ? "implicit" : "explicit";

                        var commonProps = GetCommonProperties(src, dst);

                        string invocation;
                        if (!NeedsMapperClass(src, dst))
                        {
                            // Cast-only pair: the operator is the only entry point and cannot be
                            // overridden, so there is no mapper class — inline the initializer.
                            invocation = BuildMappingInitializer(src, dst, commonProps, targetNames, nestedGraph, "item", "            ");
                        }
                        else
                        {
                            // Mapper-based invocation lives in the mappings namespace.
                            file.RequiredNamespaces.Add(mappingNamespace);
                            if (src.IsSimpleMapping)
                            {
                                invocation = $"{src.Target}To{dst.Target}Mapper.MapCore(item)";
                            }
                            else if ((src.MappingTypes & MappingTypesExtensionMethod) != 0)
                            {
                                invocation = $"MappingExtensions.MapTo{dst.Target}(item)";
                            }
                            else if ((src.MappingTypes & MappingTypesStatic) != 0)
                            {
                                invocation = $"MappingMethods.MapTo{dst.Target}(item)";
                            }
                            else
                            {
                                invocation = $"new {src.Target}To{dst.Target}Mapper().Map(item)";
                            }
                        }

                        file.Builder.AppendLine($"        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                        file.Builder.AppendLine($"        public static {opKeyword} operator {dst.Target}({src.Target} item)");
                        file.Builder.AppendLine("        {");
                        file.Builder.AppendLine($"            return {invocation};");
                        file.Builder.AppendLine("        }");
                        file.Builder.AppendLine();
                    }

                    file.Builder.AppendLine("    }");
                    file.Builder.AppendLine();
                }
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in fileBuilders)
            {
                var ns = kvp.Key;
                var (builder, requiredNamespaces) = kvp.Value;
                var sb = new StringBuilder();
                sb.AppendLine("// <auto-generated />");
                sb.AppendLine("#pragma warning disable CS8618, CS8669 // generated models: reference properties are intentionally uninitialized");
                sb.AppendLine("using System;");
                // Collection mappings emit ?.ToList() / ?.ToArray() / new HashSet<>
                // (same unconditional usings as CSteroidMappings.g.cs).
                sb.AppendLine("using System.Collections.Generic;");
                sb.AppendLine("using System.Linq;");
                foreach (var requiredNs in requiredNamespaces.OrderBy(n => n, StringComparer.Ordinal))
                {
                    if (requiredNs != ns)
                    {
                        sb.AppendLine($"using {requiredNs};");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
                sb.Append(builder);
                sb.AppendLine("}");
                result[ns] = sb.ToString();
            }

            return result;
        }
    }
}
