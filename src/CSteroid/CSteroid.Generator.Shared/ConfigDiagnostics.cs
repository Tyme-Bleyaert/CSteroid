using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Shared.OutputConventions;

namespace CSteroid.Generator.Shared
{
    /// <summary>Validates generator configuration against the discovered models.</summary>
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

        public static void Report(SourceProductionContext spc, GenerationSpec generation)
        {
            var filter = generation.Config.GenerateOnlyNamespaces;
            var modelInfos = generation.Models;
            var emittedNamespaces = new HashSet<string>(
                modelInfos.Select(GetModelNamespace),
                StringComparer.Ordinal);

            if (filter.Count > 0)
            {
                foreach (var ns in filter)
                {
                    if (!emittedNamespaces.Contains(ns))
                        spc.ReportDiagnostic(Diagnostic.Create(UnclaimedNamespace, Location.None, ns));
                }

                return;
            }

            if (generation.FoundReferencedInterfaces && emittedNamespaces.Count > 1)
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
