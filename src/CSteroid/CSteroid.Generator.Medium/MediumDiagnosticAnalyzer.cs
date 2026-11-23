using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSteroid.Generator.Medium
{
    /// <summary>Owns every CSM diagnostic; the generator never reports diagnostics.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MediumDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<DiagnosticDescriptor> Rules = ImmutableArray.Create(
            MediumDiagnostics.MissingSendRoute,
            MediumDiagnostics.DuplicateCommandRoute,
            MediumDiagnostics.InaccessibleRoute,
            MediumDiagnostics.InvalidHandler,
            MediumDiagnostics.InvalidExactRoute,
            MediumDiagnostics.InvalidLifetime,
            MediumDiagnostics.InvalidBehavior,
            MediumDiagnostics.DuplicateBehavior,
            MediumDiagnostics.InvalidClosedBehavior,
            MediumDiagnostics.InvalidManifest,
            MediumDiagnostics.MissingPublishRoute,
            MediumDiagnostics.MissingCompositionRoot,
            MediumDiagnostics.UnregisteredBehavior);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules;

        public override void Initialize(AnalysisContext context)
        {
            // Analyze generated trees so Roslyn still invokes source symbol/syntax actions in a generated compilation.
            // FactsReader only collects source declarations, and generated-code diagnostics are not reported.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(static start =>
            {
                var contracts = MediumRuntimeContracts.Resolve(start.Compilation);
                if (contracts is null)
                    return;
                var state = new AnalyzerState(contracts);
                start.RegisterSymbolAction(symbolContext =>
                {
                    if (symbolContext.Symbol is not INamedTypeSymbol symbol
                        || !symbol.Locations.Any(static location => location.IsInSource))
                    {
                        return;
                    }
                    if (FactsReader.ReadHandler(symbol, symbolContext.Compilation, state.Contracts) is { } handler)
                        state.Handlers[handler.HandlerType.RouteKey] = handler;
                    if (FactsReader.ReadSourceBehavior(symbol, symbolContext.Compilation, state.Contracts) is { } behavior)
                        state.Behaviors[BehaviorIdentity(behavior.BehaviorType!)] = behavior;
                }, SymbolKind.NamedType);
                start.RegisterSyntaxNodeAction(syntaxContext =>
                {
                    if (FactsReader.ReadAddMediumUse(
                            (InvocationExpressionSyntax)syntaxContext.Node,
                            syntaxContext.SemanticModel) is { } addMediumUse)
                    {
                        state.AddMediumUses.Add(addMediumUse);
                    }
                    else if (FactsReader.ReadSendUse(
                            (InvocationExpressionSyntax)syntaxContext.Node,
                            syntaxContext.SemanticModel,
                            state.Contracts) is { } sendUse)
                    {
                        state.SendUses.Add(sendUse);
                    }
                    else if (FactsReader.ReadPublishUse(
                            (InvocationExpressionSyntax)syntaxContext.Node,
                            syntaxContext.SemanticModel,
                            state.Contracts) is { } publishUse)
                    {
                        state.PublishUses.Add(publishUse);
                    }
                }, SyntaxKind.InvocationExpression);
                start.RegisterCompilationEndAction(end => AnalyzeCompilation(end, state));
            });
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context, AnalyzerState state)
        {
            var compilation = FactsReader.ReadCompilationFacts(context.Compilation, state.Contracts);
            var facts = new List<HandlerDeclarationModel>(state.Handlers.Values);
            if (compilation.IsCompositionRoot)
                facts.AddRange(compilation.ImportedHandlers);
            var handlerPlan = RoutePolicy.BuildDeclarationPlan(facts);
            var behaviorPlan = BehaviorPolicy.BuildDeclarationPlan(compilation.Behaviors);
            var global = MediatorPlanner.Build(handlerPlan, behaviorPlan, compilation);
            var issues = new List<RuleIssue>(global.Issues);
            foreach (var use in state.SendUses)
            {
                if (RoutePolicy.EvaluateSendUse(
                        use,
                        handlerPlan,
                        context.Compilation.Assembly.Name,
                        compilation.IsCompositionRoot) is { } issue)
                {
                    issues.Add(issue);
                }
            }
            foreach (var use in state.PublishUses)
            {
                if (RoutePolicy.EvaluatePublishUse(
                        use,
                        handlerPlan,
                        context.Compilation.Assembly.Name,
                        compilation.IsCompositionRoot) is { } issue)
                {
                    issues.Add(issue);
                }
            }

            if (!compilation.IsCompositionRoot)
                AddMissingRootIssue(state, issues);
            else
                AddUnregisteredBehaviorIssues(compilation, state, issues);

            issues.Sort(static (left, right) =>
            {
                var result = string.CompareOrdinal(left.RuleId, right.RuleId);
                if (result != 0)
                    return result;
                result = string.CompareOrdinal(left.StableKey, right.StableKey);
                if (result != 0)
                    return result;
                result = string.CompareOrdinal(left.Location.FilePath, right.Location.FilePath);
                return result != 0 ? result : left.Location.TextSpan.Start.CompareTo(right.Location.TextSpan.Start);
            });
            foreach (var issue in issues)
            {
                var descriptor = MediumDiagnostics.ForRule(issue.RuleId);
                var arguments = new object[issue.MessageArguments.Count];
                for (int i = 0; i < arguments.Length; i++)
                    arguments[i] = issue.MessageArguments[i];
                var properties = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
                foreach (var property in issue.Properties)
                    properties[property.Key] = property.Value;
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    ToLocation(context.Compilation, issue.Location),
                    properties.ToImmutable(),
                    arguments));
            }
        }

        private static void AddMissingRootIssue(AnalyzerState state, List<RuleIssue> issues)
        {
            AddMediumUseFact? first = null;
            foreach (var use in state.AddMediumUses)
            {
                if (first is null
                    || string.CompareOrdinal(use.Location.FilePath, first.Location.FilePath) < 0
                    || string.Equals(use.Location.FilePath, first.Location.FilePath, StringComparison.Ordinal)
                        && use.Location.TextSpan.Start < first.Location.TextSpan.Start)
                {
                    first = use;
                }
            }
            if (first is not null)
            {
                issues.Add(new RuleIssue(
                    MediumDiagnostics.MissingCompositionRootId,
                    "CompositionRoot",
                    first.Location,
                    new EquatableArray<string>(ImmutableArray<string>.Empty)));
            }
        }

        private static void AddUnregisteredBehaviorIssues(
            CompilationFacts compilation,
            AnalyzerState state,
            List<RuleIssue> issues)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var behavior in compilation.Behaviors)
            {
                if (behavior.BehaviorType is not null)
                    declared.Add(BehaviorIdentity(behavior.BehaviorType));
            }

            foreach (var behavior in state.Behaviors.Values)
            {
                var type = behavior.BehaviorType!;
                var identity = BehaviorIdentity(type);
                if (declared.Contains(identity))
                    continue;
                issues.Add(new RuleIssue(
                    MediumDiagnostics.UnregisteredBehaviorId,
                    identity,
                    behavior.Location,
                    new EquatableArray<string>(ImmutableArray.Create(type.GlobalName)),
                    new EquatableArray<DiagnosticProperty>(ImmutableArray.Create(
                        new DiagnosticProperty(MediumDiagnosticProperties.BehaviorTypeOfOperand, BehaviorTypeOfOperand(behavior)),
                        new DiagnosticProperty(MediumDiagnosticProperties.BehaviorIdentity, identity),
                        new DiagnosticProperty(MediumDiagnosticProperties.BehaviorLifetime, LifetimeModel.Scoped.ToString())))));
            }
        }

        private static string BehaviorIdentity(TypeRef type) =>
            type.MetadataIdentity + "|" + type.AssemblyName;

        private static string BehaviorTypeOfOperand(BehaviorDeclarationFact behavior)
        {
            var name = behavior.BehaviorType!.GlobalName;
            if (!behavior.IsOpenGenericDefinition)
                return name;
            var genericStart = name.IndexOf('<');
            if (genericStart >= 0)
                name = name.Substring(0, genericStart);
            return name + "<" + new string(',', behavior.TypeParameterNames.Count - 1) + ">";
        }

        private static Location ToLocation(Compilation compilation, LocationInfo location)
        {
            if (location.FilePath.Length == 0)
                return Location.None;
            var tree = compilation.SyntaxTrees.FirstOrDefault(
                candidate => string.Equals(candidate.FilePath, location.FilePath, StringComparison.Ordinal));
            return tree is null
                ? Location.Create(location.FilePath, location.TextSpan, location.LineSpan)
                : Location.Create(tree, location.TextSpan);
        }

        private sealed class AnalyzerState
        {
            internal AnalyzerState(MediumRuntimeContracts contracts) => Contracts = contracts;
            internal MediumRuntimeContracts Contracts { get; }
            internal ConcurrentDictionary<string, HandlerDeclarationModel> Handlers { get; } = new();
            internal ConcurrentDictionary<string, BehaviorDeclarationFact> Behaviors { get; } = new();
            internal ConcurrentBag<SendUseFact> SendUses { get; } = new();
            internal ConcurrentBag<PublishUseFact> PublishUses { get; } = new();
            internal ConcurrentBag<AddMediumUseFact> AddMediumUses { get; } = new();
        }
    }
}
