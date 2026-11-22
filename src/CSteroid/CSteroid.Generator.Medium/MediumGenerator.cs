using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSteroid.Generator.Medium
{
    /// <summary>Incremental provider wiring; all policy decisions live in shared readers and planners.</summary>
    [Generator]
    public sealed class MediumGenerator : IIncrementalGenerator
    {
        internal const string HandlerDiscoveryTrackingName = "Medium.Handler.Discovery";
        internal const string LocalPlanningTrackingName = "Medium.Local.Planning";
        internal const string CompilationProjectionTrackingName = "Medium.Compilation.Projection";
        internal const string MergedPlanningTrackingName = "Medium.Merged.Planning";
        internal const string BehaviorPlanningTrackingName = "Medium.Behavior.Planning";
        internal const string EmissionPlanningTrackingName = "Medium.Emission.Planning";
        internal const string ManifestPlanningTrackingName = "Medium.Manifest.Planning";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var handlerFacts = context.SyntaxProvider.CreateSyntaxProvider(
                    static (node, ct) => node is ClassDeclarationSyntax { BaseList: not null }
                        or RecordDeclarationSyntax { BaseList: not null },
                    static (ctx, ct) => FactsReader.ReadSourceHandler(ctx))
                .Where(static fact => fact is not null)
                .Select(static (fact, ct) => fact!)
                .WithTrackingName(HandlerDiscoveryTrackingName)
                .Collect();

            var localPlan = handlerFacts
                .Select(static (facts, ct) => RoutePolicy.BuildDeclarationPlan(facts))
                .WithTrackingName(LocalPlanningTrackingName);
            var compilationFacts = context.CompilationProvider
                .Select(static (compilation, ct) =>
                {
                    var contracts = MediumRuntimeContracts.Resolve(compilation);
                    return contracts is null
                        ? new CompilationFacts(false, Empty<BehaviorDeclarationFact>(), Empty<HandlerDeclarationModel>(), Empty<RuleIssue>())
                        : FactsReader.ReadCompilationFacts(compilation, contracts);
                })
                .WithTrackingName(CompilationProjectionTrackingName);

            var mergedHandlers = handlerFacts.Combine(compilationFacts)
                .Select(static (pair, ct) =>
                {
                    var facts = ImmutableArray.CreateBuilder<HandlerDeclarationModel>();
                    facts.AddRange(pair.Left);
                    if (pair.Right.IsCompositionRoot)
                        facts.AddRange(pair.Right.ImportedHandlers);
                    return RoutePolicy.BuildDeclarationPlan(facts);
                })
                .WithTrackingName(MergedPlanningTrackingName);
            var behaviorPlan = compilationFacts
                .Select(static (facts, ct) => BehaviorPolicy.BuildDeclarationPlan(new BehaviorSet(facts.Behaviors)))
                .WithTrackingName(BehaviorPlanningTrackingName);
            var globalPlan = mergedHandlers.Combine(behaviorPlan).Combine(compilationFacts)
                .Select(static (input, ct) => MediatorPlanner.Build(input.Left.Left, input.Left.Right, input.Right))
                .WithTrackingName(EmissionPlanningTrackingName);
            var emissionPlan = globalPlan.Select(static (plan, ct) => plan.ForEmission());
            var manifestPlan = localPlan
                .Select(static (plan, ct) => MediatorPlanner.BuildManifest(plan))
                .WithTrackingName(ManifestPlanningTrackingName);

            context.RegisterSourceOutput(manifestPlan, static (ctx, plan) => ManifestEmitter.Emit(ctx, plan));
            context.RegisterSourceOutput(emissionPlan, static (ctx, plan) =>
            {
                MediatorEmitter.Emit(ctx, plan);
                RegistrationEmitter.Emit(ctx, plan);
            });
        }

        private static EquatableArray<T> Empty<T>() where T : System.IEquatable<T> =>
            new(ImmutableArray<T>.Empty);
    }
}
