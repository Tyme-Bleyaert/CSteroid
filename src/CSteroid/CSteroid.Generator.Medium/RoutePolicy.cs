using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CSteroid.Generator.Medium
{
    /// <summary>Owns typed route validity, accessibility, DI shape, and command cardinality.</summary>
    internal static class RoutePolicy
    {
        internal static HandlerDeclarationPlan BuildDeclarationPlan(IEnumerable<HandlerDeclarationModel> facts)
        {
            var distinct = new Dictionary<string, HandlerDeclarationModel>(StringComparer.Ordinal);
            foreach (var fact in facts)
            {
                if (!distinct.TryGetValue(fact.HandlerType.RouteKey, out var existing)
                    || CompareLocations(fact.Location, existing.Location) < 0)
                {
                    distinct[fact.HandlerType.RouteKey] = fact;
                }
            }

            var handlers = new List<HandlerDeclarationModel>(distinct.Values);
            handlers.Sort(static (left, right) => string.CompareOrdinal(left.HandlerType.RouteKey, right.HandlerType.RouteKey));
            var evaluations = new List<RouteEvaluation>();
            var issues = new List<RuleIssue>();
            foreach (var handler in handlers)
            {
                var declarationIssues = EvaluateDeclaration(handler);
                issues.AddRange(declarationIssues);
                foreach (var route in handler.Routes)
                {
                    var evaluation = declarationIssues.Count == 0
                        ? EvaluateRoute(handler, route)
                        : RouteEvaluation.Invalid(handler, route, null);
                    evaluations.Add(evaluation);
                    if (evaluation.Issue is { } issue)
                        issues.Add(issue);
                }
            }

            ExcludeDuplicateCommands(evaluations, issues);
            evaluations.Sort(static (left, right) =>
            {
                var result = string.CompareOrdinal(left.Route.Message.RouteKey, right.Route.Message.RouteKey);
                if (result != 0)
                    return result;
                result = left.Route.Kind.CompareTo(right.Route.Kind);
                return result != 0
                    ? result
                    : string.CompareOrdinal(left.Handler.HandlerType.RouteKey, right.Handler.HandlerType.RouteKey);
            });
            issues.Sort(CompareIssues);

            var validHandlers = new List<HandlerDeclarationModel>();
            foreach (var handler in handlers)
            {
                foreach (var evaluation in evaluations)
                {
                    if (evaluation.IsValid
                        && string.Equals(
                            evaluation.Handler.HandlerType.RouteKey,
                            handler.HandlerType.RouteKey,
                            StringComparison.Ordinal))
                    {
                        validHandlers.Add(handler);
                        break;
                    }
                }
            }

            return new HandlerDeclarationPlan(
                ToArray(validHandlers),
                ToArray(evaluations),
                ToArray(issues));
        }

        internal static RuleIssue? EvaluateSendUse(SendUseFact use, HandlerDeclarationPlan plan)
        {
            foreach (var route in plan.ValidRoutes)
            {
                if (route.Route.Kind == use.CommandKind
                    && string.Equals(route.Route.Message.RouteKey, use.CommandType.RouteKey, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            foreach (var route in plan.RouteEvaluations)
            {
                if (!route.IsValid
                    && string.Equals(route.Route.Message.RouteKey, use.CommandType.RouteKey, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return Issue(
                MediumDiagnostics.MissingSendRouteId,
                use.StableKey,
                use.Location,
                use.CommandType.GlobalName,
                use.CommandKind == RouteKind.CommandWithResponse ? "response" : "non-response");
        }

        internal static RuleIssue? EvaluatePublishUse(PublishUseFact use, HandlerDeclarationPlan plan)
        {
            foreach (var route in plan.ValidRoutes)
            {
                if (route.Route.Kind == RouteKind.Message
                    && string.Equals(route.Route.Message.RouteKey, use.MessageType.RouteKey, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            foreach (var route in plan.RouteEvaluations)
            {
                if (!route.IsValid
                    && string.Equals(route.Route.Message.RouteKey, use.MessageType.RouteKey, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return Issue(
                MediumDiagnostics.MissingPublishRouteId,
                use.StableKey,
                use.Location,
                use.MessageType.GlobalName);
        }

        private static List<RuleIssue> EvaluateDeclaration(HandlerDeclarationModel handler)
        {
            var issues = new List<RuleIssue>();
            if (!handler.IsAccessible)
            {
                issues.Add(Issue(
                    MediumDiagnostics.InaccessibleRouteId,
                    handler.HandlerType.RouteKey + "\0Accessibility",
                    handler.Location,
                    handler.HandlerType.GlobalName,
                    "the handler is inaccessible to generated namespace-level code"));
            }

            var shapeError = GetHandlerShapeError(handler, out var shapeLocation);
            if (shapeError is not null)
            {
                issues.Add(Issue(
                    MediumDiagnostics.InvalidHandlerId,
                    handler.HandlerType.RouteKey + "\0Shape",
                    shapeLocation,
                    handler.HandlerType.GlobalName,
                    shapeError));
            }

            if (handler.Lifetime < (int)LifetimeModel.Scoped || handler.Lifetime > (int)LifetimeModel.Transient)
            {
                issues.Add(Issue(
                    MediumDiagnostics.InvalidLifetimeId,
                    handler.HandlerType.RouteKey + "\0Lifetime",
                    handler.LifetimeLocation ?? handler.Location,
                    handler.HandlerType.GlobalName,
                    "the handler lifetime value is invalid"));
            }
            return issues;
        }

        private static string? GetHandlerShapeError(HandlerDeclarationModel handler, out LocationInfo location)
        {
            foreach (var type in handler.Shape.TypeChain)
            {
                if (type.IsGeneric)
                {
                    location = type.Location;
                    return "generic handlers and generic containing types are not supported";
                }
            }

            var declaration = handler.Shape.TypeChain[handler.Shape.TypeChain.Count - 1];
            location = declaration.Location;
            if (declaration.IsAbstract || declaration.IsStatic)
                return "abstract and static handlers cannot be constructed";

            foreach (var constructor in handler.Shape.Constructors)
            {
                if (constructor.Accessibility != DeclaredAccessibilityModel.Public)
                    continue;
                var usable = true;
                foreach (var parameter in constructor.Parameters)
                {
                    if (parameter.RefKind != ParameterRefKind.None
                        || parameter.IsByRefLike
                        || !parameter.IsAccessible
                        || parameter.Type.ContainsTypeParameters
                        || parameter.Type.ContainsPointer
                        || parameter.Type.Kind == TypeRefKind.FunctionPointer)
                    {
                        usable = false;
                        break;
                    }
                }
                if (usable)
                    return null;
            }
            return "no public DI-constructible instance constructor is available";
        }

        private static RouteEvaluation EvaluateRoute(HandlerDeclarationModel handler, HandlerRouteModel route)
        {
            var message = route.Message;
            if (!route.MessageIsAccessible || !route.ResponseIsAccessible)
            {
                return Reject(
                    handler,
                    route,
                    MediumDiagnostics.InaccessibleRouteId,
                    message.GlobalName,
                    "a message, response, or containing type is inaccessible to the composition root");
            }

            if (message.Kind != TypeRefKind.Named
                || message.IsAbstract
                || message.ContainsTypeParameters
                || message.TypeArguments.Count != 0
                || !HasContract(message, MediumRuntimeContracts.MessageMetadataName)
                || route.Response is { ContainsTypeParameters: true }
                || route.Response is { ContainsPointer: true })
            {
                return RejectInvalidRoute(handler, route, "the exact route must use a non-generic concrete message and closed response");
            }

            var responses = GetResponseContracts(message);
            if (responses.Count > 1)
                return RejectInvalidRoute(handler, route, "the message declares multiple response contracts");

            switch (route.Kind)
            {
                case RouteKind.Message:
                    if (HasContract(message, MediumRuntimeContracts.CommandMetadataName))
                        return RejectInvalidRoute(handler, route, "a command cannot be declared as an event handler");
                    break;
                case RouteKind.Command:
                    if (responses.Count != 0)
                        return RejectInvalidRoute(handler, route, "a response command cannot be handled as non-response");
                    if (!HasContract(message, MediumRuntimeContracts.CommandMetadataName))
                        return RejectInvalidRoute(handler, route, "the message is not a command");
                    break;
                case RouteKind.CommandWithResponse:
                    if (responses.Count != 1
                        || route.Response is null
                        || responses[0].TypeArguments.Count != 1
                        || !string.Equals(responses[0].TypeArguments[0].RouteKey, route.Response.RouteKey, StringComparison.Ordinal))
                    {
                        return RejectInvalidRoute(handler, route, "the handler response does not match the command's only response contract");
                    }
                    break;
            }
            return RouteEvaluation.Valid(handler, route);
        }

        private static List<TypeRef> GetResponseContracts(TypeRef message)
        {
            var result = new List<TypeRef>();
            if (string.Equals(message.MetadataIdentity, MediumRuntimeContracts.CommandOfTMetadataName, StringComparison.Ordinal))
                result.Add(message);
            foreach (var contract in message.AllInterfaces)
            {
                if (!string.Equals(contract.MetadataIdentity, MediumRuntimeContracts.CommandOfTMetadataName, StringComparison.Ordinal))
                    continue;
                var exists = false;
                foreach (var item in result)
                {
                    if (string.Equals(item.RouteKey, contract.RouteKey, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    result.Add(contract);
            }
            result.Sort(static (left, right) => string.CompareOrdinal(left.RouteKey, right.RouteKey));
            return result;
        }

        private static bool HasContract(TypeRef type, string metadataIdentity)
        {
            if (string.Equals(type.MetadataIdentity, metadataIdentity, StringComparison.Ordinal))
                return true;
            foreach (var contract in type.AllInterfaces)
            {
                if (string.Equals(contract.MetadataIdentity, metadataIdentity, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ExcludeDuplicateCommands(List<RouteEvaluation> routes, List<RuleIssue> issues)
        {
            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                if (!route.IsValid || route.Route.Kind == RouteKind.Message)
                    continue;
                if (!groups.TryGetValue(route.Route.Message.RouteKey, out var indices))
                {
                    indices = new List<int>();
                    groups.Add(route.Route.Message.RouteKey, indices);
                }
                indices.Add(i);
            }

            var keys = new List<string>(groups.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                var indices = groups[key];
                if (indices.Count < 2)
                    continue;
                indices.Sort((left, right) =>
                {
                    var leftRoute = routes[left];
                    var rightRoute = routes[right];
                    var result = string.CompareOrdinal(
                        leftRoute.Handler.HandlerType.RouteKey,
                        rightRoute.Handler.HandlerType.RouteKey);
                    return result != 0 ? result : left.CompareTo(right);
                });
                for (int i = 0; i < indices.Count; i++)
                {
                    var route = routes[indices[i]];
                    routes[indices[i]] = RouteEvaluation.Invalid(route.Handler, route.Route, null);
                    if (i > 0)
                    {
                        issues.Add(Issue(
                            MediumDiagnostics.DuplicateCommandRouteId,
                            key + "\0" + route.Handler.HandlerType.RouteKey,
                            route.Handler.Location,
                            route.Handler.HandlerType.GlobalName,
                            route.Route.Message.GlobalName));
                    }
                }
            }
        }

        private static RouteEvaluation RejectInvalidRoute(
            HandlerDeclarationModel handler,
            HandlerRouteModel route,
            string reason) =>
            Reject(handler, route, MediumDiagnostics.InvalidExactRouteId, route.Message.GlobalName, reason);

        private static RouteEvaluation Reject(
            HandlerDeclarationModel handler,
            HandlerRouteModel route,
            string ruleId,
            params string[] arguments) =>
            RouteEvaluation.Invalid(
                handler,
                route,
                Issue(ruleId, handler.HandlerType.RouteKey + "\0" + route.Message.RouteKey, route.Location, arguments));

        private static RuleIssue Issue(
            string ruleId,
            string stableKey,
            LocationInfo location,
            params string[] arguments) =>
            new(ruleId, stableKey, location, new EquatableArray<string>(ImmutableArray.Create(arguments)));

        private static EquatableArray<T> ToArray<T>(IEnumerable<T> values) where T : IEquatable<T> =>
            new(ImmutableArray.CreateRange(values));

        private static int CompareLocations(LocationInfo left, LocationInfo right)
        {
            var result = string.CompareOrdinal(left.FilePath, right.FilePath);
            return result != 0 ? result : left.TextSpan.Start.CompareTo(right.TextSpan.Start);
        }

        private static int CompareIssues(RuleIssue left, RuleIssue right)
        {
            var result = string.CompareOrdinal(left.RuleId, right.RuleId);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.StableKey, right.StableKey);
            return result != 0 ? result : CompareLocations(left.Location, right.Location);
        }
    }
}
