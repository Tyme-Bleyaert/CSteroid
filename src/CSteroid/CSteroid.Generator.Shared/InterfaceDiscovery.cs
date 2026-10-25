using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CSteroid.Generator.Shared.ModelInfoFactory;

namespace CSteroid.Generator.Shared
{
    public static class InterfaceDiscovery
    {
        public static IncrementalValueProvider<(ImmutableArray<INamedTypeSymbol> Symbols, Compilation Compilation)> Register(IncrementalGeneratorInitializationContext context)
        {
            var interfaceSymbols = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is InterfaceDeclarationSyntax ids && ids.AttributeLists.Count > 0,
                    transform: static (ctx, ct) => ctx.SemanticModel.GetDeclaredSymbol((InterfaceDeclarationSyntax)ctx.Node, ct) as INamedTypeSymbol)
                .Where(static s => s is not null)
                .Select(static (s, _) => s!)
                .Collect();

            return interfaceSymbols.Combine(context.CompilationProvider);
        }

        public static List<ModelInfo> BuildModelInfos(SourceProductionContext spc, ImmutableArray<INamedTypeSymbol> symbols, Compilation compilation)
        {
            var allSymbols = ReferencedInterfaceDiscovery.MergeWithReferenced(compilation, symbols, out var foundReferencedInterfaces);
            var canonicalAttrSymbol = compilation.GetTypeByMetadataName("CSteroid.CanonicalModelAttribute");
            var excludeAttrSymbol = compilation.GetTypeByMetadataName("CSteroid.ExcludeFromGenerationAttribute");

            var modelInfos = new List<ModelInfo>();

            if (canonicalAttrSymbol is null)
                return modelInfos;

            foreach (var symbol in allSymbols.OrderBy(s => s.ToDisplayString(), StringComparer.Ordinal))
            {
                var allAttrs = symbol.GetAttributes();
                var attrs = allAttrs
                    .Where(a => a.AttributeClass is not null && a.AttributeClass.ToDisplayString() == canonicalAttrSymbol.ToDisplayString())
                    .ToList();

                if (attrs.Count == 0)
                {
                    var attrClassNames = string.Join(", ", allAttrs.Select(a => a.AttributeClass?.ToDisplayString() ?? "null"));
                    var msg = string.Format("Interface {0} has attributes: {1}. Expected: {2}", symbol.Name, attrClassNames, canonicalAttrSymbol.ToDisplayString());
                    var desc = new DiagnosticDescriptor("CSG001", "Debug", msg, "CSteroid", DiagnosticSeverity.Warning, true);
                    spc.ReportDiagnostic(Diagnostic.Create(desc, symbol.Locations.FirstOrDefault()));
                    continue;
                }

                var allProperties = GetAllProperties(symbol).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
                var ormAttrs = allAttrs
                    .Where(a => a.AttributeClass?.ToDisplayString() == "CSteroid.OrmTargetAttribute")
                    .ToList();

                foreach (var attr in attrs.OrderBy(a => GetTarget(a) ?? symbol.Name, StringComparer.Ordinal))
                {
                    var info = BuildModelInfo(symbol, attr, allProperties, excludeAttrSymbol, ormAttrs);
                    if (info is not null)
                        modelInfos.Add(info);
                }
            }

            ConfigDiagnostics.Report(spc, compilation, modelInfos, foundReferencedInterfaces);

            return modelInfos;
        }

        public static void ExecuteWithErrorBoundary(SourceProductionContext spc, Action core)
        {
            try
            {
                core();
            }
            catch (System.Exception ex)
            {
                var desc = new DiagnosticDescriptor("CSG999", "Generator Error", ex.ToString(), "CSteroid", DiagnosticSeverity.Error, true);
                spc.ReportDiagnostic(Diagnostic.Create(desc, Location.None));
            }
        }
    }
}
