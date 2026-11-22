using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace CSteroid.Generator.Medium
{
    /// <summary>Order-sensitive immutable value sequence used at incremental provider boundaries.</summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _items;

        public EquatableArray(ImmutableArray<T> items)
        {
            _items = items;
        }

        public int Count => _items.IsDefault ? 0 : _items.Length;
        public T this[int index] => _items[index];
        public ImmutableArray<T>.Enumerator GetEnumerator() =>
            (_items.IsDefault ? ImmutableArray<T>.Empty : _items).GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return _items[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

        public bool Equals(EquatableArray<T> other)
        {
            var left = _items;
            var right = other._items;
            if (left.IsDefaultOrEmpty)
                return right.IsDefaultOrEmpty;
            if (right.IsDefaultOrEmpty || right.Length != left.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (!left[i].Equals(right[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);
        public override int GetHashCode()
        {
            if (_items.IsDefaultOrEmpty)
                return 0;
            unchecked
            {
                int hash = 17;
                foreach (var item in _items)
                    hash = (hash * 31) + item.GetHashCode();
                return hash;
            }
        }
        public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
        public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
    }

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

    internal sealed record RuleIssue(
        string RuleId,
        string StableKey,
        LocationInfo Location,
        EquatableArray<string> MessageArguments);

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
            EquatableArray<HandlerRouteModel> routes,
            bool isAccessible,
            LocationInfo location)
        {
            HandlerType = handlerType;
            Shape = shape;
            Lifetime = lifetime;
            LifetimeLocation = lifetimeLocation;
            Routes = routes;
            IsAccessible = isAccessible;
            Location = location;
        }

        internal TypeRef HandlerType { get; }
        internal ServiceShapeFact Shape { get; }
        internal int Lifetime { get; }
        internal LocationInfo? LifetimeLocation { get; }
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

    internal sealed record SendUseFact(TypeRef CommandType, RouteKind CommandKind, LocationInfo Location)
    {
        internal string StableKey => CommandType.RouteKey + "\0" + CommandKind + "\0" + Location.FilePath + "\0" + Location.TextSpan.Start;
    }

    internal sealed record PublishUseFact(TypeRef MessageType, LocationInfo Location)
    {
        internal string StableKey => MessageType.RouteKey + "\0Publish\0" + Location.FilePath + "\0" + Location.TextSpan.Start;
    }

    internal sealed record BehaviorStepPlan(TypeRef BehaviorType, int Lifetime);

    internal sealed record RoutePlan(
        RouteKind Kind,
        TypeRef Message,
        TypeRef? Response,
        TypeRef HandlerType,
        int HandlerLifetime,
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
