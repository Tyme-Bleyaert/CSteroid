using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CSteroid.Generator.Shared.OutputConventions;

namespace CSteroid.Generator.Shared
{
    public static class InterfaceDiscovery
    {
        public const string InterfaceDiscoveryTrackingName = "CSteroid.Interface.Discovery";
        public const string CompilationProjectionTrackingName = "CSteroid.Compilation.Projection";
        public const string GenerationPlanningTrackingName = "CSteroid.Generation.Planning";

        public static IncrementalValueProvider<GenerationSpec> Register(IncrementalGeneratorInitializationContext context)
        {
            var interfaces = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "CSteroid.CanonicalModelAttribute",
                    predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                    transform: static (ctx, ct) => ModelInfoFactory.BuildInterfaceInfo(
                        (INamedTypeSymbol)ctx.TargetSymbol,
                        ctx.Attributes,
                        ct))
                .WithTrackingName(InterfaceDiscoveryTrackingName)
                .Collect();

            var compilationSpec = interfaces
                .Combine(context.CompilationProvider)
                .Select(static (input, ct) => BuildCompilationSpec(input.Left, input.Right, ct))
                .WithTrackingName(CompilationProjectionTrackingName);

            return interfaces
                .Combine(compilationSpec)
                .Select(static (input, ct) => BuildGenerationSpec(input.Left, input.Right, ct))
                .WithTrackingName(GenerationPlanningTrackingName);
        }

        private static CompilationSpec BuildCompilationSpec(
            IReadOnlyList<InterfaceSpec> sourceInterfaces,
            Compilation compilation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var referencedInterfaces = ReferencedInterfaceDiscovery.ReadReferenced(compilation, cancellationToken);
            var modelTypeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var interfaceSpec in sourceInterfaces.Concat(referencedInterfaces))
            {
                foreach (var model in interfaceSpec.Models)
                    modelTypeNames.Add($"{GetModelNamespace(model)}.{model.Target}");
            }

            var resolvableModelTypes = modelTypeNames
                .Where(name => compilation.GetTypeByMetadataName(name) is not null)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var (validationNamespace, mappingNamespace) = GetOutputNamespaces(compilation.Assembly);
            var generateOnlyNamespaces = GetGenerateOnlyNamespaces(compilation.Assembly) ?? Array.Empty<string>();
            var config = new GeneratorConfigSpec(
                GetFileNamingConvention(compilation.Assembly),
                validationNamespace,
                mappingNamespace,
                new EquatableArray<string>(generateOnlyNamespaces.OrderBy(ns => ns, StringComparer.Ordinal)));
            return new CompilationSpec(
                new EquatableArray<InterfaceSpec>(referencedInterfaces),
                new EquatableArray<string>(resolvableModelTypes),
                config);
        }

        private static GenerationSpec BuildGenerationSpec(
            IReadOnlyList<InterfaceSpec> sourceInterfaces,
            CompilationSpec compilation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var merged = new Dictionary<string, InterfaceSpec>(StringComparer.Ordinal);
            foreach (var interfaceSpec in sourceInterfaces)
                merged[interfaceSpec.Key] = interfaceSpec;

            var foundReferencedInterfaces = false;
            foreach (var referenced in compilation.ReferencedInterfaces)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!merged.ContainsKey(referenced.Key))
                {
                    merged.Add(referenced.Key, referenced);
                    foundReferencedInterfaces = true;
                }
            }

            var models = new List<ModelInfo>();
            foreach (var interfaceSpec in merged.Values.OrderBy(i => i.Key, StringComparer.Ordinal))
            {
                foreach (var model in interfaceSpec.Models)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var metadataName = $"{GetModelNamespace(model)}.{model.Target}";
                    models.Add(model.WithResolution(compilation.ResolvableModelTypes.Contains(metadataName, StringComparer.Ordinal)));
                }
            }

            return new GenerationSpec(
                new EquatableArray<ModelInfo>(models),
                new EquatableArray<ModelInfo>(ModelInfoFactory.MergeModels(models)),
                compilation.Config,
                foundReferencedInterfaces);
        }

        public static void ExecuteWithErrorBoundary(SourceProductionContext spc, Action core)
        {
            try
            {
                core();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var desc = new DiagnosticDescriptor("CSG999", "Generator Error", ex.ToString(), "CSteroid", DiagnosticSeverity.Error, true);
                spc.ReportDiagnostic(Diagnostic.Create(desc, Location.None));
            }
        }
    }
}
