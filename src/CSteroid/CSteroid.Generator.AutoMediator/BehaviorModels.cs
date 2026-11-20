using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CSteroid.Generator.AutoMediator
{
    internal enum BehaviorContractKind
    {
        NonResponse,
        Response,
    }

    [Flags]
    internal enum BehaviorSpecialConstraints
    {
        None = 0,
        ReferenceType = 1,
        ValueType = 2,
        Constructor = 4,
        NotNull = 8,
        Unmanaged = 16,
    }

    internal sealed class BehaviorContractFact : IEquatable<BehaviorContractFact>
    {
        internal BehaviorContractFact(BehaviorContractKind kind, TypeRef messageType, TypeRef? responseType)
        {
            Kind = kind;
            MessageType = messageType;
            ResponseType = responseType;
        }

        internal BehaviorContractKind Kind { get; }
        internal TypeRef MessageType { get; }
        internal TypeRef? ResponseType { get; }

        public bool Equals(BehaviorContractFact? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Kind == other.Kind
                && MessageType.Equals(other.MessageType)
                && Equals(ResponseType, other.ResponseType);
        }

        public override bool Equals(object? obj) => Equals(obj as BehaviorContractFact);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (Kind.GetHashCode() * 31) + MessageType.GetHashCode();
                return (hash * 31) + (ResponseType?.GetHashCode() ?? 0);
            }
        }
    }

    internal sealed class BehaviorConstraintFact : IEquatable<BehaviorConstraintFact>
    {
        internal BehaviorConstraintFact(
            string typeParameterName,
            BehaviorSpecialConstraints specialConstraints,
            EquatableArray<TypeRef> types)
        {
            TypeParameterName = typeParameterName;
            SpecialConstraints = specialConstraints;
            Types = types;
        }

        internal string TypeParameterName { get; }
        internal BehaviorSpecialConstraints SpecialConstraints { get; }
        internal EquatableArray<TypeRef> Types { get; }

        public bool Equals(BehaviorConstraintFact? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(TypeParameterName, other.TypeParameterName, StringComparison.Ordinal)
                && SpecialConstraints == other.SpecialConstraints
                && Types.Equals(other.Types);
        }

        public override bool Equals(object? obj) => Equals(obj as BehaviorConstraintFact);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(TypeParameterName);
                hash = (hash * 31) + SpecialConstraints.GetHashCode();
                return (hash * 31) + Types.GetHashCode();
            }
        }
    }

    internal sealed class BehaviorDeclarationFact : IEquatable<BehaviorDeclarationFact>
    {
        internal BehaviorDeclarationFact(
            TypeRef? behaviorType,
            string globalDefinitionName,
            ServiceShapeFact? shape,
            bool isOpenGenericDefinition,
            EquatableArray<string> typeParameterNames,
            EquatableArray<BehaviorConstraintFact> constraints,
            EquatableArray<BehaviorContractFact> contracts,
            int order,
            int lifetime,
            LocationInfo location)
        {
            BehaviorType = behaviorType;
            GlobalDefinitionName = globalDefinitionName;
            Shape = shape;
            IsOpenGenericDefinition = isOpenGenericDefinition;
            TypeParameterNames = typeParameterNames;
            Constraints = constraints;
            Contracts = contracts;
            Order = order;
            Lifetime = lifetime;
            Location = location;
        }

        internal TypeRef? BehaviorType { get; }
        internal string GlobalDefinitionName { get; }
        internal ServiceShapeFact? Shape { get; }
        internal bool IsOpenGenericDefinition { get; }
        internal EquatableArray<string> TypeParameterNames { get; }
        internal EquatableArray<BehaviorConstraintFact> Constraints { get; }
        internal EquatableArray<BehaviorContractFact> Contracts { get; }
        internal int Order { get; }
        internal int Lifetime { get; }
        internal LocationInfo Location { get; }
        internal string StableKey => BehaviorType?.RouteKey ?? "<null>";

        public bool Equals(BehaviorDeclarationFact? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Equals(BehaviorType, other.BehaviorType)
                && string.Equals(GlobalDefinitionName, other.GlobalDefinitionName, StringComparison.Ordinal)
                && Equals(Shape, other.Shape)
                && IsOpenGenericDefinition == other.IsOpenGenericDefinition
                && TypeParameterNames.Equals(other.TypeParameterNames)
                && Constraints.Equals(other.Constraints)
                && Contracts.Equals(other.Contracts)
                && Order == other.Order
                && Lifetime == other.Lifetime
                && Location.Equals(other.Location);
        }

        public override bool Equals(object? obj) => Equals(obj as BehaviorDeclarationFact);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BehaviorType?.GetHashCode() ?? 0;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(GlobalDefinitionName);
                hash = (hash * 31) + (Shape?.GetHashCode() ?? 0);
                hash = (hash * 31) + IsOpenGenericDefinition.GetHashCode();
                hash = (hash * 31) + TypeParameterNames.GetHashCode();
                hash = (hash * 31) + Constraints.GetHashCode();
                hash = (hash * 31) + Contracts.GetHashCode();
                hash = (hash * 31) + Order;
                hash = (hash * 31) + Lifetime;
                return (hash * 31) + Location.GetHashCode();
            }
        }
    }

    internal sealed class BehaviorSet : IEquatable<BehaviorSet>
    {
        internal BehaviorSet(EquatableArray<BehaviorDeclarationFact> declarations)
        {
            Declarations = declarations;
        }

        internal EquatableArray<BehaviorDeclarationFact> Declarations { get; }

        public bool Equals(BehaviorSet? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Declarations.Equals(other.Declarations);
        }

        public override bool Equals(object? obj) => Equals(obj as BehaviorSet);
        public override int GetHashCode() => Declarations.GetHashCode();
    }

    internal sealed class BehaviorSetPlan : IEquatable<BehaviorSetPlan>
    {
        internal BehaviorSetPlan(
            EquatableArray<BehaviorDeclarationFact> validDeclarations,
            EquatableArray<RuleIssue> issues)
        {
            ValidDeclarations = validDeclarations;
            Issues = issues;
        }

        internal EquatableArray<BehaviorDeclarationFact> ValidDeclarations { get; }
        internal EquatableArray<RuleIssue> Issues { get; }

        public bool Equals(BehaviorSetPlan? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return ValidDeclarations.Equals(other.ValidDeclarations) && Issues.Equals(other.Issues);
        }

        public override bool Equals(object? obj) => Equals(obj as BehaviorSetPlan);

        public override int GetHashCode()
        {
            unchecked
            {
                return (ValidDeclarations.GetHashCode() * 31) + Issues.GetHashCode();
            }
        }
    }

    internal sealed class BehaviorSelection : IEquatable<BehaviorSelection>
    {
        internal BehaviorSelection(
            BehaviorDeclarationFact declaration,
            TypeRef closedBehaviorType,
            RuleIssue? issue)
        {
            Declaration = declaration;
            ClosedBehaviorType = closedBehaviorType;
            Issue = issue;
        }

        internal BehaviorDeclarationFact Declaration { get; }
        internal TypeRef ClosedBehaviorType { get; }
        internal RuleIssue? Issue { get; }
        internal bool IsValid => Issue is null;

        public bool Equals(BehaviorSelection? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Declaration.Equals(other.Declaration)
                && ClosedBehaviorType.Equals(other.ClosedBehaviorType)
                && Equals(Issue, other.Issue);
        }

        public override bool Equals(object? obj) => Equals(obj as BehaviorSelection);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (Declaration.GetHashCode() * 31) + ClosedBehaviorType.GetHashCode();
                return (hash * 31) + (Issue?.GetHashCode() ?? 0);
            }
        }
    }

    internal sealed class BehaviorRoutePlan : IEquatable<BehaviorRoutePlan>
    {
        internal BehaviorRoutePlan(string routeKey, EquatableArray<BehaviorSelection> selections)
        {
            RouteKey = routeKey;
            Selections = selections;
        }

        internal string RouteKey { get; }
        internal EquatableArray<BehaviorSelection> Selections { get; }

        internal IEnumerable<BehaviorSelection> ValidSelections
        {
            get
            {
                foreach (var selection in Selections)
                {
                    if (selection.IsValid)
                        yield return selection;
                }
            }
        }

        internal IEnumerable<RuleIssue> Issues
        {
            get
            {
                foreach (var selection in Selections)
                {
                    if (selection.Issue is { } issue)
                        yield return issue;
                }
            }
        }

        public bool Equals(BehaviorRoutePlan? other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(RouteKey, other.RouteKey, StringComparison.Ordinal)
                && Selections.Equals(other.Selections);
        }

        public override bool Equals(object? obj) => Equals(obj as BehaviorRoutePlan);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(RouteKey) * 31) + Selections.GetHashCode();
            }
        }
    }
}
