using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Shared.OutputConventions;

namespace CSteroid.Generator.Shared
{
    /// <summary>
    /// Validates the consuming project's generator configuration against the discovered
    /// models. These diagnostics check CSteroid's own configuration (mechanism), never the
    /// consumer's architecture (policy): layered-namespace enforcement belongs to tools
    /// like ArchUnitNET, not to the generator.
    /// </summary>
    public static class ConfigDiagnostics
    {
        private static readonly DiagnosticDescriptor UnclaimedNamespace = new DiagnosticDescriptor(
            "CSG102",
            "GenerateOnlyNamespaces claims an unused namespace",
            "GenerateOnlyNamespaces lists '{0}', but no discovered [CanonicalModel] target generates into that namespace — likely a typo or stale filter",
            "CSteroid",
            DiagnosticSeverity.Warning,
            true);

        private static readonly DiagnosticDescriptor LayeredMetadataWithoutFilter = new DiagnosticDescriptor(
            "CSG103",
            "Layered metadata library without a namespace filter",
            "{0} [CanonicalModel] target(s) across {1} namespaces were discovered in referenced assemblies. No GenerateOnlyNamespaces filter is configured, so this project generates every model and layered solutions can hit CS0436 collisions.",
            "CSteroid",
            DiagnosticSeverity.Info,
            true,
            description: "Without GenerateOnlyNamespaces, every project referencing the metadata library generates every model, which collides with CS0436 in layered solutions. Single-namespace metadata libraries are exempt.");

        public static void Report(SourceProductionContext spc, Compilation compilation, List<ModelInfo> modelInfos, bool foundReferencedInterfaces)
        {
            var filter = GetGenerateOnlyNamespaces(compilation.Assembly);
            var emittedNamespaces = new HashSet<string>(
                modelInfos.Select(GetModelNamespace),
                StringComparer.Ordinal);

            if (filter is { Length: > 0 })
            {
                foreach (var ns in filter.OrderBy(n => n, StringComparer.Ordinal))
                {
                    if (!emittedNamespaces.Contains(ns))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(UnclaimedNamespace, Location.None, ns));
                    }
                }

                return;
            }

            // No filter configured. Only nudge when the metadata library was clearly designed
            // for layered generation (targets span multiple namespaces): that is exactly the
            // setup where generating everything here collides with other projects. A single-
            // namespace metadata library (e.g. one business domain) is a deliberate monolith
            // pattern and stays silent.
            if (!foundReferencedInterfaces)
                return;

            if (emittedNamespaces.Count > 1)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    LayeredMetadataWithoutFilter,
                    Location.None,
                    modelInfos.Count,
                    emittedNamespaces.Count));
            }
        }
    }
}
