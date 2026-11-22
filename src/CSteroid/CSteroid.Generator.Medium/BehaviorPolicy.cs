using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CSteroid.Generator.Medium
{
    internal static class BehaviorPolicy
    {
        internal static BehaviorSetPlan BuildDeclarationPlan(
            EquatableArray<BehaviorDeclarationFact> declarations)
        {
            var duplicateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var declaration in declarations)
            {
                if (declaration.BehaviorType is null)
                    continue;
                duplicateCounts.TryGetValue(declaration.StableKey, out var count);
                duplicateCounts[declaration.StableKey] = count + 1;
            }

            var valid = new List<BehaviorDeclarationFact>();
            var issues = new List<RuleIssue>();
            foreach (var declaration in declarations)
            {
                var declarationIssues = ValidateDeclaration(declaration);
                if (declaration.BehaviorType is not null
                    && duplicateCounts.TryGetValue(declaration.StableKey, out var count)
                    && count > 1)
                {
                    declarationIssues.Add(Issue(
                        MediumDiagnostics.DuplicateBehaviorId,
                        declaration.StableKey,
                        declaration.Location,
                        declaration.BehaviorType.GlobalName));
                }

                if (declarationIssues.Count == 0)
                    valid.Add(declaration);
                else
                    issues.AddRange(declarationIssues);
            }

            valid.Sort(CompareDeclarations);
            issues.Sort(CompareIssues);
            return new BehaviorSetPlan(
                new EquatableArray<BehaviorDeclarationFact>(ImmutableArray.CreateRange(valid)),
                new EquatableArray<RuleIssue>(ImmutableArray.CreateRange(issues)));
        }

        internal static EquatableArray<BehaviorRoutePlan> SelectRoutes(
            BehaviorSetPlan plan,
            HandlerDeclarationPlan handlerPlan)
        {
            var routes = ImmutableArray.CreateBuilder<BehaviorRoutePlan>();
            foreach (var route in handlerPlan.ValidRoutes)
                routes.Add(Select(plan, route));
            return new EquatableArray<BehaviorRoutePlan>(routes.ToImmutable());
        }

        private static BehaviorRoutePlan Select(BehaviorSetPlan plan, RouteEvaluation route)
        {
            var handlerRoute = route.Route;
            var selections = new List<BehaviorSelection>();
            foreach (var declaration in plan.ValidDeclarations)
            {
                var arity = declaration.TypeParameterNames.Count;
                if (handlerRoute.Kind == RouteKind.CommandWithResponse)
                {
                    if (declaration.IsOpenGenericDefinition && arity != 2)
                        continue;
                }
                else if (declaration.IsOpenGenericDefinition && arity != 1)
                {
                    continue;
                }

                var arguments = GetRouteArguments(handlerRoute, declaration.IsOpenGenericDefinition);
                if (!SatisfiesConstraints(declaration, arguments)
                    || !HasExactContract(declaration, handlerRoute, arguments))
                {
                    continue;
                }

                var closedType = declaration.BehaviorType!.Substitute(declaration.TypeParameterNames, arguments);
                selections.Add(new BehaviorSelection(
                    declaration,
                    closedType,
                    ValidateClosedBehavior(declaration, handlerRoute.Message)));
            }

            selections.Sort(static (left, right) =>
            {
                var result = left.Declaration.Order.CompareTo(right.Declaration.Order);
                return result != 0
                    ? result
                    : string.CompareOrdinal(left.ClosedBehaviorType.RouteKey, right.ClosedBehaviorType.RouteKey);
            });
            return new BehaviorRoutePlan(
                handlerRoute.Message.RouteKey,
                new EquatableArray<BehaviorSelection>(ImmutableArray.CreateRange(selections)));
        }

        private static List<RuleIssue> ValidateDeclaration(BehaviorDeclarationFact declaration)
        {
            var issues = new List<RuleIssue>();
            if (declaration.BehaviorType is null || declaration.Shape is null)
            {
                issues.Add(Issue(
                    MediumDiagnostics.InvalidBehaviorId,
                    declaration.StableKey,
                    declaration.Location,
                    "<null>",
                    "the attribute does not name a behavior type"));
                return issues;
            }

            var type = declaration.BehaviorType;
            var shape = declaration.Shape;
            var current = shape.TypeChain[shape.TypeChain.Count - 1];
            string? error = null;
            if (current.ContainerKind != TypeContainerKind.Class && current.ContainerKind != TypeContainerKind.RecordClass)
                error = "the behavior must be a class or record class";
            else if (current.IsAbstract || current.IsStatic)
                error = "abstract and static behaviors are not supported";
            else if (declaration.TypeParameterNames.Count > 2)
                error = "generic behaviors must have arity one or two";
            else if (type.ContainsTypeParameters && !declaration.IsOpenGenericDefinition)
                error = "the behavior contains open constructed type leftovers";
            else if (!declaration.IsOpenGenericDefinition && declaration.TypeParameterNames.Count != 0)
                error = "closed constructed generic declarations are not supported";
            else if (declaration.Contracts.Count == 0)
                error = "the behavior implements no supported pipeline contract";
            else if (declaration.IsOpenGenericDefinition && !HasValidOpenContractShape(declaration))
                error = "the open generic contract does not use the behavior type parameters in route order";

            if (error is null)
            {
                for (int i = 0; i < shape.TypeChain.Count - 1; i++)
                {
                    if (!shape.TypeChain[i].IsGeneric)
                        continue;
                    error = "generic containing types are not supported";
                    break;
                }
            }

            if (error is not null)
            {
                issues.Add(Issue(
                    MediumDiagnostics.InvalidBehaviorId,
                    declaration.StableKey,
                    declaration.Location,
                    type.GlobalName,
                    error));
            }

            if (declaration.Lifetime < (int)LifetimeModel.Scoped
                || declaration.Lifetime > (int)LifetimeModel.Transient)
            {
                issues.Add(Issue(
                    MediumDiagnostics.InvalidLifetimeId,
                    declaration.StableKey + "\0Lifetime",
                    declaration.Location,
                    type.GlobalName,
                    "the behavior lifetime value is invalid"));
            }

            return issues;
        }

        private static bool HasValidOpenContractShape(BehaviorDeclarationFact declaration)
        {
            var arity = declaration.TypeParameterNames.Count;
            foreach (var contract in declaration.Contracts)
            {
                if (arity == 1)
                {
                    if (contract.Kind != BehaviorContractKind.NonResponse
                        || !contract.MessageType.IsTypeParameter(declaration.TypeParameterNames[0]))
                    {
                        return false;
                    }
                }
                else if (arity == 2)
                {
                    if (contract.Kind != BehaviorContractKind.Response
                        || !contract.MessageType.IsTypeParameter(declaration.TypeParameterNames[0])
                        || contract.ResponseType is null
                        || !contract.ResponseType.IsTypeParameter(declaration.TypeParameterNames[1]))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return declaration.Contracts.Count != 0;
        }

        private static EquatableArray<TypeRef> GetRouteArguments(
            HandlerRouteModel route,
            bool openGeneric)
        {
            if (!openGeneric)
                return new EquatableArray<TypeRef>(ImmutableArray<TypeRef>.Empty);
            if (route.Kind == RouteKind.CommandWithResponse)
                return new EquatableArray<TypeRef>(ImmutableArray.Create(route.Message, route.Response!));
            return new EquatableArray<TypeRef>(ImmutableArray.Create(route.Message));
        }

        private static bool SatisfiesConstraints(
            BehaviorDeclarationFact declaration,
            EquatableArray<TypeRef> arguments)
        {
            foreach (var constraint in declaration.Constraints)
            {
                var index = IndexOfTypeParameter(declaration.TypeParameterNames, constraint.TypeParameterName);
                if (index < 0 || index >= arguments.Count)
                    return false;
                var argument = arguments[index];
                var special = constraint.SpecialConstraints;
                if (special.HasFlag(BehaviorSpecialConstraints.ReferenceType) && !argument.IsReferenceType)
                    return false;
                if (special.HasFlag(BehaviorSpecialConstraints.ValueType)
                    && (!argument.IsValueType || IsNullableValueType(argument)))
                {
                    return false;
                }
                if (special.HasFlag(BehaviorSpecialConstraints.Unmanaged) && !argument.IsUnmanagedType)
                    return false;
                if (special.HasFlag(BehaviorSpecialConstraints.Constructor)
                    && !argument.HasPublicParameterlessConstructor)
                {
                    return false;
                }
                if (special.HasFlag(BehaviorSpecialConstraints.NotNull) && IsNullable(argument))
                    return false;

                foreach (var constraintType in constraint.Types)
                {
                    var closedConstraint = constraintType.Substitute(declaration.TypeParameterNames, arguments);
                    if (!SatisfiesTypeConstraint(argument, closedConstraint))
                        return false;
                }
            }
            return true;
        }

        private static bool SatisfiesTypeConstraint(TypeRef argument, TypeRef constraint)
        {
            if (string.Equals(argument.RouteKey, constraint.RouteKey, StringComparison.Ordinal))
                return true;
            foreach (var contract in argument.AllInterfaces)
            {
                if (string.Equals(contract.RouteKey, constraint.RouteKey, StringComparison.Ordinal))
                    return true;
            }
            for (var baseType = argument.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                if (string.Equals(baseType.RouteKey, constraint.RouteKey, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool HasExactContract(
            BehaviorDeclarationFact declaration,
            HandlerRouteModel route,
            EquatableArray<TypeRef> arguments)
        {
            var expectedKind = route.Kind == RouteKind.CommandWithResponse
                ? BehaviorContractKind.Response
                : BehaviorContractKind.NonResponse;
            foreach (var contract in declaration.Contracts)
            {
                if (contract.Kind != expectedKind
                    || !string.Equals(
                        contract.MessageType.Substitute(declaration.TypeParameterNames, arguments).RouteKey,
                        route.Message.RouteKey,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (expectedKind == BehaviorContractKind.NonResponse)
                    return true;
                if (contract.ResponseType is not null
                    && string.Equals(
                        contract.ResponseType.Substitute(declaration.TypeParameterNames, arguments).RouteKey,
                        route.Response!.RouteKey,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static RuleIssue? ValidateClosedBehavior(
            BehaviorDeclarationFact declaration,
            TypeRef message)
        {
            var shape = declaration.Shape!;
            foreach (var type in shape.TypeChain)
            {
                if (!type.IsFileLocal
                    && (type.Accessibility == DeclaredAccessibilityModel.Public
                        || type.Accessibility == DeclaredAccessibilityModel.Internal))
                {
                    continue;
                }
                return ClosedBehaviorIssue(declaration, message, "the closed behavior is inaccessible");
            }

            foreach (var constructor in shape.Constructors)
            {
                if (constructor.Accessibility != DeclaredAccessibilityModel.Public)
                    continue;
                var usable = true;
                foreach (var parameter in constructor.Parameters)
                {
                    if (parameter.RefKind != ParameterRefKind.None
                        || parameter.IsByRefLike
                        || parameter.Type.ContainsPointer
                        || parameter.Type.Kind == TypeRefKind.FunctionPointer
                        || (!parameter.IsAccessible && !parameter.Type.ContainsTypeParameters))
                    {
                        usable = false;
                        break;
                    }
                }
                if (usable)
                    return null;
            }

            return ClosedBehaviorIssue(declaration, message, "no public DI-constructible instance constructor is available");
        }

        private static RuleIssue ClosedBehaviorIssue(
            BehaviorDeclarationFact declaration,
            TypeRef message,
            string reason)
        {
            return new RuleIssue(
                MediumDiagnostics.InvalidClosedBehaviorId,
                declaration.StableKey + "\0" + message.RouteKey,
                declaration.Location,
                new EquatableArray<string>(ImmutableArray.Create(
                    declaration.BehaviorType!.GlobalName,
                    message.GlobalName,
                    reason)));
        }

        private static int IndexOfTypeParameter(EquatableArray<string> names, string name)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static bool IsNullableValueType(TypeRef type)
        {
            return string.Equals(type.MetadataIdentity, "System.Nullable`1", StringComparison.Ordinal);
        }

        private static bool IsNullable(TypeRef type)
        {
            return IsNullableValueType(type)
                || (type.IsReferenceType && type.GlobalName.EndsWith("?", StringComparison.Ordinal));
        }

        private static int CompareDeclarations(BehaviorDeclarationFact left, BehaviorDeclarationFact right)
        {
            var result = left.Order.CompareTo(right.Order);
            return result != 0 ? result : string.CompareOrdinal(left.StableKey, right.StableKey);
        }

        private static int CompareIssues(RuleIssue left, RuleIssue right)
        {
            var result = string.CompareOrdinal(left.RuleId, right.RuleId);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.StableKey, right.StableKey);
            return result != 0 ? result : left.Location.TextSpan.Start.CompareTo(right.Location.TextSpan.Start);
        }

        private static RuleIssue Issue(
            string ruleId,
            string stableKey,
            LocationInfo location,
            params string[] arguments)
        {
            return new RuleIssue(
                ruleId,
                stableKey,
                location,
                new EquatableArray<string>(ImmutableArray.Create(arguments)));
        }
    }
}
