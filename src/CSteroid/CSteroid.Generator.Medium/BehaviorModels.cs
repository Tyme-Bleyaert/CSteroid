using System;
using System.Collections.Generic;

namespace CSteroid.Generator.Medium
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

    internal sealed record BehaviorContractFact(
        BehaviorContractKind Kind,
        TypeRef MessageType,
        TypeRef? ResponseType);

    internal sealed record BehaviorConstraintFact(
        string TypeParameterName,
        BehaviorSpecialConstraints SpecialConstraints,
        EquatableArray<TypeRef> Types);

    internal sealed record BehaviorDeclarationFact(
        TypeRef? BehaviorType,
        ServiceShapeFact? Shape,
        bool IsOpenGenericDefinition,
        EquatableArray<string> TypeParameterNames,
        EquatableArray<BehaviorConstraintFact> Constraints,
        EquatableArray<BehaviorContractFact> Contracts,
        int Order,
        int Lifetime,
        LocationInfo Location)
    {
        internal string StableKey => BehaviorType?.RouteKey ?? "<null>";
    }

    internal sealed record BehaviorSetPlan(
        EquatableArray<BehaviorDeclarationFact> ValidDeclarations,
        EquatableArray<RuleIssue> Issues);

    internal sealed record BehaviorSelection(
        BehaviorDeclarationFact Declaration,
        TypeRef ClosedBehaviorType,
        RuleIssue? Issue)
    {
        internal bool IsValid => Issue is null;
    }

    internal sealed record BehaviorRoutePlan(
        string RouteKey,
        EquatableArray<BehaviorSelection> Selections)
    {
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
    }
}
