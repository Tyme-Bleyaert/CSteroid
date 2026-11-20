using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSteroid.Generator.AutoMediator
{
    /// <summary>Owns every CSAM diagnostic; the generator never reports diagnostics.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AutoMediatorDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<DiagnosticDescriptor> Rules = ImmutableArray.Create(
            AutoMediatorDiagnostics.MissingSendRoute,
            AutoMediatorDiagnostics.DuplicateCommandRoute,
            AutoMediatorDiagnostics.InaccessibleRoute,
            AutoMediatorDiagnostics.InvalidHandler,
            AutoMediatorDiagnostics.InvalidExactRoute,
            AutoMediatorDiagnostics.InvalidLifetime,
            AutoMediatorDiagnostics.InvalidBehavior,
            AutoMediatorDiagnostics.DuplicateBehavior,
            AutoMediatorDiagnostics.InvalidClosedBehavior,
            AutoMediatorDiagnostics.InvalidManifest);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules;

        public override void Initialize(AnalysisContext context)
        {
            // Analyze generated trees so Roslyn still invokes source symbol/syntax actions in a generated compilation.
            // FactsReader only collects source declarations, and generated-code diagnostics are not reported.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(static start =>
            {
                var contracts = AutoMediatorRuntimeContracts.Resolve(start.Compilation);
                if (contracts is null)
                    return;
                var state = new AnalyzerState(contracts);
                start.RegisterSymbolAction(symbolContext =>
                {
                    if (symbolContext.Symbol is INamedTypeSymbol symbol
                        && symbol.Locations.Any(static location => location.IsInSource)
                        && FactsReader.ReadHandler(symbol, symbolContext.Compilation, state.Contracts) is { } handler)
                    {
                        state.Handlers[handler.HandlerType.RouteKey] = handler;
                    }
                }, SymbolKind.NamedType);
                start.RegisterSyntaxNodeAction(syntaxContext =>
                {
                    if (FactsReader.ReadSendUse(
                            (InvocationExpressionSyntax)syntaxContext.Node,
                            syntaxContext.SemanticModel,
                            state.Contracts) is { } use)
                    {
                        state.SendUses.Add(use);
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
            var behaviorPlan = BehaviorPolicy.BuildDeclarationPlan(new BehaviorSet(compilation.Behaviors));
            var global = MediatorPlanner.Build(handlerPlan, behaviorPlan, compilation);
            var issues = new List<RuleIssue>(global.Issues);
            foreach (var use in state.SendUses)
            {
                if (RoutePolicy.EvaluateSendUse(use, handlerPlan) is { } issue)
                    issues.Add(issue);
            }
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
                var descriptor = AutoMediatorDiagnostics.ForRule(issue.RuleId);
                var arguments = new object[issue.MessageArguments.Count];
                for (int i = 0; i < arguments.Length; i++)
                    arguments[i] = issue.MessageArguments[i];
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    ToLocation(issue.Location),
                    arguments));
            }
        }

        private static Location ToLocation(LocationInfo location) =>
            location.FilePath.Length == 0
                ? Location.None
                : Location.Create(location.FilePath, location.TextSpan, location.LineSpan);

        private sealed class AnalyzerState
        {
            internal AnalyzerState(AutoMediatorRuntimeContracts contracts) => Contracts = contracts;
            internal AutoMediatorRuntimeContracts Contracts { get; }
            internal ConcurrentDictionary<string, HandlerDeclarationModel> Handlers { get; } = new();
            internal ConcurrentBag<SendUseFact> SendUses { get; } = new();
        }
    }
}
