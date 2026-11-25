using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace CSteroid.Generator.Medium
{
    internal enum TypeRefKind
    {
        Named,
        Interface,
        Array,
        Pointer,
        TypeParameter,
        FunctionPointer,
        Other,
    }

    internal enum RouteKind
    {
        Message,
        Command,
        CommandWithResponse,
        Stream,
    }

    internal sealed record TypeRef
    {
        internal TypeRef(
            string routeKey,
            string globalName,
            string assemblyName,
            bool isValueType,
            string metadataIdentity,
            TypeRefKind kind,
            EquatableArray<TypeRef> typeArguments,
            bool isAbstract,
            bool containsTypeParameters,
            bool containsPointer,
            TypeRef? baseType,
            EquatableArray<TypeRef> allInterfaces,
            bool isReferenceType,
            bool isUnmanagedType,
            bool hasPublicParameterlessConstructor)
        {
            RouteKey = routeKey;
            GlobalName = globalName;
            AssemblyName = assemblyName;
            IsValueType = isValueType;
            MetadataIdentity = metadataIdentity;
            Kind = kind;
            TypeArguments = typeArguments;
            IsAbstract = isAbstract;
            ContainsTypeParameters = containsTypeParameters;
            ContainsPointer = containsPointer;
            BaseType = baseType;
            AllInterfaces = allInterfaces;
            IsReferenceType = isReferenceType;
            IsUnmanagedType = isUnmanagedType;
            HasPublicParameterlessConstructor = hasPublicParameterlessConstructor;
        }

        internal string RouteKey { get; }
        internal string GlobalName { get; }
        internal string AssemblyName { get; }
        internal bool IsValueType { get; }
        internal string MetadataIdentity { get; }
        internal TypeRefKind Kind { get; }
        internal EquatableArray<TypeRef> TypeArguments { get; }
        internal bool IsAbstract { get; }
        internal bool ContainsTypeParameters { get; }
        internal bool ContainsPointer { get; }
        internal TypeRef? BaseType { get; }
        internal EquatableArray<TypeRef> AllInterfaces { get; }
        internal bool IsReferenceType { get; }
        internal bool IsUnmanagedType { get; }
        internal bool HasPublicParameterlessConstructor { get; }

        internal bool IsTypeParameter(string name) =>
            Kind == TypeRefKind.TypeParameter
            && string.Equals(RouteKey, "!" + name + "|" + AssemblyName, StringComparison.Ordinal);

        internal TypeRef Substitute(
            EquatableArray<string> typeParameterNames,
            EquatableArray<TypeRef> arguments)
        {
            if (Kind == TypeRefKind.TypeParameter)
            {
                for (int i = 0; i < typeParameterNames.Count; i++)
                {
                    if (IsTypeParameter(typeParameterNames[i]))
                        return arguments[i];
                }
            }

            if (TypeArguments.Count == 0)
                return this;

            var closedArguments = ImmutableArray.CreateBuilder<TypeRef>(TypeArguments.Count);
            var routeArguments = new string[TypeArguments.Count];
            var globalArguments = new string[TypeArguments.Count];
            foreach (var argument in TypeArguments)
            {
                var closed = argument.Substitute(typeParameterNames, arguments);
                routeArguments[closedArguments.Count] = closed.NestedRouteKey;
                globalArguments[closedArguments.Count] = closed.GlobalName;
                closedArguments.Add(closed);
            }

            return new TypeRef(
                MetadataIdentity + "<" + string.Join(",", routeArguments) + ">|" + AssemblyName,
                DefinitionGlobalName + "<" + string.Join(", ", globalArguments) + ">",
                AssemblyName,
                false,
                MetadataIdentity,
                TypeRefKind.Named,
                new EquatableArray<TypeRef>(closedArguments.ToImmutable()),
                false,
                false,
                false,
                null,
                new EquatableArray<TypeRef>(ImmutableArray<TypeRef>.Empty),
                true,
                false,
                true);
        }

        private string DefinitionGlobalName
        {
            get
            {
                var genericStart = GlobalName.IndexOf('<');
                return genericStart < 0 ? GlobalName : GlobalName.Substring(0, genericStart);
            }
        }

        private string NestedRouteKey
        {
            get
            {
                var separator = RouteKey.LastIndexOf('|');
                return separator < 0 ? RouteKey : RouteKey.Substring(0, separator);
            }
        }
    }

    internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan);

    internal sealed record DiagnosticProperty(string Key, string Value);

    internal sealed record RuleIssue
    {
        internal RuleIssue(
            string ruleId,
            string stableKey,
            LocationInfo location,
            EquatableArray<string> messageArguments,
            EquatableArray<DiagnosticProperty> properties = default)
        {
            RuleId = ruleId;
            StableKey = stableKey;
            Location = location;
            MessageArguments = messageArguments;

            var sorted = ImmutableArray.CreateBuilder<DiagnosticProperty>(properties.Count);
            foreach (var property in properties)
                sorted.Add(property);
            sorted.Sort(static (left, right) =>
            {
                var result = string.CompareOrdinal(left.Key, right.Key);
                return result != 0 ? result : string.CompareOrdinal(left.Value, right.Value);
            });
            Properties = new EquatableArray<DiagnosticProperty>(sorted.ToImmutable());
        }

        internal string RuleId { get; }
        internal string StableKey { get; }
        internal LocationInfo Location { get; }
        internal EquatableArray<string> MessageArguments { get; }
        internal EquatableArray<DiagnosticProperty> Properties { get; }
    }

    internal sealed record HandlerRouteModel
    {
        internal HandlerRouteModel(
            RouteKind kind,
            TypeRef message,
            TypeRef? response,
            bool messageIsAccessible,
            bool responseIsAccessible,
            LocationInfo location)
        {
            Kind = kind;
            Message = message;
            Response = response;
            MessageIsAccessible = messageIsAccessible;
            ResponseIsAccessible = responseIsAccessible;
            Location = location;
        }

        internal RouteKind Kind { get; }
        internal TypeRef Message { get; }
        internal TypeRef? Response { get; }
        internal bool MessageIsAccessible { get; }
        internal bool ResponseIsAccessible { get; }
        internal LocationInfo Location { get; }
    }

    internal sealed record HandlerDeclarationModel
    {
        internal HandlerDeclarationModel(
            TypeRef handlerType,
            ServiceShapeFact shape,
            int lifetime,
            LocationInfo? lifetimeLocation,
            int order,
            EquatableArray<HandlerRouteModel> routes,
            bool isAccessible,
            LocationInfo location)
        {
            HandlerType = handlerType;
            Shape = shape;
            Lifetime = lifetime;
            LifetimeLocation = lifetimeLocation;
            Order = order;
            Routes = routes;
            IsAccessible = isAccessible;
            Location = location;
        }

        internal TypeRef HandlerType { get; }
        internal ServiceShapeFact Shape { get; }
        internal int Lifetime { get; }
        internal LocationInfo? LifetimeLocation { get; }
        internal int Order { get; }
        internal EquatableArray<HandlerRouteModel> Routes { get; }
        internal bool IsAccessible { get; }
        internal LocationInfo Location { get; }
    }

    internal sealed record RouteEvaluation
    {
        private RouteEvaluation(HandlerDeclarationModel handler, HandlerRouteModel route, RuleIssue? issue, bool isValid)
        {
            Handler = handler;
            Route = route;
            Issue = issue;
            IsValid = isValid;
        }

        internal HandlerDeclarationModel Handler { get; }
        internal HandlerRouteModel Route { get; }
        internal RuleIssue? Issue { get; }
        internal bool IsValid { get; }

        internal static RouteEvaluation Valid(HandlerDeclarationModel handler, HandlerRouteModel route) =>
            new(handler, route, null, true);
        internal static RouteEvaluation Invalid(HandlerDeclarationModel handler, HandlerRouteModel route, RuleIssue? issue) =>
            new(handler, route, issue, false);
    }

    internal sealed record HandlerDeclarationPlan(
        EquatableArray<HandlerDeclarationModel> ValidHandlers,
        EquatableArray<RouteEvaluation> RouteEvaluations,
        EquatableArray<RuleIssue> Issues)
    {
        internal IEnumerable<RouteEvaluation> ValidRoutes
        {
            get
            {
                foreach (var route in RouteEvaluations)
                {
                    if (route.IsValid)
                        yield return route;
                }
            }
        }
    }

    internal sealed record CompilationFacts(
        bool IsCompositionRoot,
        EquatableArray<BehaviorDeclarationFact> Behaviors,
        EquatableArray<HandlerDeclarationModel> ImportedHandlers,
        EquatableArray<RuleIssue> ImportIssues);

    internal sealed record SendUseFact(
        TypeRef CommandType,
        RouteKind CommandKind,
        TypeRef? ResponseType,
        bool MessageIsAccessible,
        bool ResponseIsAccessible,
        LocationInfo Location)
    {
        internal string StableKey => CommandType.RouteKey + " " + CommandKind + " " + Location.FilePath + " " + Location.TextSpan.Start;
    }

    internal sealed record PublishUseFact(
        TypeRef MessageType,
        bool MessageIsAccessible,
        LocationInfo Location)
    {
        internal string StableKey => MessageType.RouteKey + " Publish " + Location.FilePath + " " + Location.TextSpan.Start;
    }

    internal sealed record AddMediumUseFact(LocationInfo Location);

    internal sealed record BehaviorStepPlan(TypeRef BehaviorType, int Lifetime);

    internal sealed record RoutePlan(
        RouteKind Kind,
        TypeRef Message,
        TypeRef? Response,
        TypeRef HandlerType,
        int HandlerLifetime,
        int HandlerOrder,
        EquatableArray<BehaviorStepPlan> Behaviors,
        string MemberId);

    internal sealed record RegistrationPlan(TypeRef ServiceType, int Lifetime);

    internal sealed record EmissionPlan(
        bool IsCompositionRoot,
        EquatableArray<RoutePlan> Routes,
        EquatableArray<RegistrationPlan> Registrations);

    internal sealed record GlobalPlan(
        bool IsCompositionRoot,
        EquatableArray<RoutePlan> Routes,
        EquatableArray<RegistrationPlan> Registrations,
        EquatableArray<RuleIssue> Issues)
    {
        internal EmissionPlan ForEmission() => new(IsCompositionRoot, Routes, Registrations);
    }

    internal sealed record LocalManifestPlan(EquatableArray<TypeRef> HandlerTypes);
}
