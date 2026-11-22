using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CSteroid.Generator.Medium
{
    /// <summary>Builds the single dispatch and registration plan owned by a composition root.</summary>
    internal static class MediatorPlanner
    {
        internal static GlobalPlan Build(
            HandlerDeclarationPlan handlers,
            BehaviorSetPlan behaviors,
            CompilationFacts compilation)
        {
            var selections = BehaviorPolicy.SelectRoutes(behaviors, handlers);
            var selectionByRoute = new Dictionary<string, BehaviorRoutePlan>(StringComparer.Ordinal);
            foreach (var selection in selections)
                selectionByRoute[selection.RouteKey] = selection;

            var routes = new List<RoutePlan>();
            var registrations = new Dictionary<string, RegistrationPlan>(StringComparer.Ordinal);
            var issues = new List<RuleIssue>();
            issues.AddRange(compilation.ImportIssues);
            issues.AddRange(handlers.Issues);
            issues.AddRange(behaviors.Issues);
            foreach (var selection in selectionByRoute.Values)
                issues.AddRange(selection.Issues);

            foreach (var evaluation in handlers.ValidRoutes)
            {
                var key = RouteKey(evaluation);
                if (!selectionByRoute.TryGetValue(evaluation.Route.Message.RouteKey, out var selection))
                    continue;

                var steps = ImmutableArray.CreateBuilder<BehaviorStepPlan>();
                foreach (var behavior in selection.ValidSelections)
                {
                    steps.Add(new BehaviorStepPlan(behavior.ClosedBehaviorType, behavior.Declaration.Lifetime));
                    registrations[behavior.ClosedBehaviorType.RouteKey] = new RegistrationPlan(
                        behavior.ClosedBehaviorType,
                        behavior.Declaration.Lifetime);
                }

                var route = evaluation.Route;
                var handler = evaluation.Handler;
                routes.Add(new RoutePlan(
                    route.Kind,
                    route.Message,
                    route.Response,
                    handler.HandlerType,
                    handler.Lifetime,
                    new EquatableArray<BehaviorStepPlan>(steps.ToImmutable()),
                    StableName.Hash(key)));
                registrations[handler.HandlerType.RouteKey] = new RegistrationPlan(handler.HandlerType, handler.Lifetime);
            }

            routes.Sort(static (left, right) =>
            {
                var result = left.Kind.CompareTo(right.Kind);
                if (result != 0)
                    return result;
                result = string.CompareOrdinal(left.Message.RouteKey, right.Message.RouteKey);
                return result != 0
                    ? result
                    : string.CompareOrdinal(left.HandlerType.RouteKey, right.HandlerType.RouteKey);
            });
            var registrationList = new List<RegistrationPlan>(registrations.Values);
            registrationList.Sort(static (left, right) => string.CompareOrdinal(left.ServiceType.RouteKey, right.ServiceType.RouteKey));
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

            return new GlobalPlan(
                compilation.IsCompositionRoot,
                new EquatableArray<RoutePlan>(ImmutableArray.CreateRange(routes)),
                new EquatableArray<RegistrationPlan>(ImmutableArray.CreateRange(registrationList)),
                new EquatableArray<RuleIssue>(ImmutableArray.CreateRange(issues)));
        }

        internal static LocalManifestPlan BuildManifest(HandlerDeclarationPlan localHandlers)
        {
            var handlers = ImmutableArray.CreateBuilder<TypeRef>();
            foreach (var handler in localHandlers.ValidHandlers)
                handlers.Add(handler.HandlerType);
            return new LocalManifestPlan(new EquatableArray<TypeRef>(handlers.ToImmutable()));
        }

        private static string RouteKey(RouteEvaluation route) =>
            route.Handler.HandlerType.RouteKey + "\0" + (int)route.Route.Kind + "\0" + route.Route.Message.RouteKey;
    }
}
