using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Medium
{
    internal static class MediumDiagnostics
    {
        internal const string MissingSendRouteId = "CSM001";
        internal const string DuplicateCommandRouteId = "CSM002";
        internal const string InaccessibleRouteId = "CSM006";
        internal const string InvalidHandlerId = "CSM011";
        internal const string InvalidExactRouteId = "CSM012";
        internal const string InvalidLifetimeId = "CSM013";
        internal const string InvalidBehaviorId = "CSM014";
        internal const string DuplicateBehaviorId = "CSM015";
        internal const string InvalidClosedBehaviorId = "CSM016";
        internal const string InvalidManifestId = "CSM017";
        internal const string MissingPublishRouteId = "CSM018";

        private const string Category = "CSteroid.Medium";

        internal static readonly DiagnosticDescriptor MissingSendRoute = new DiagnosticDescriptor(
            id: MissingSendRouteId,
            title: "Medium Send use has no exact route",
            messageFormat: "Command '{0}' has no valid exact {1} Send route",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor DuplicateCommandRoute = new DiagnosticDescriptor(
            id: DuplicateCommandRouteId,
            title: "Medium command has multiple handler routes",
            messageFormat: "Handler '{0}' duplicates the command route for '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InaccessibleRoute = new DiagnosticDescriptor(
            id: InaccessibleRouteId,
            title: "Medium route declaration is inaccessible",
            messageFormat: "Medium cannot use '{0}' because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidHandler = new DiagnosticDescriptor(
            id: InvalidHandlerId,
            title: "Medium handler is invalid",
            messageFormat: "Handler '{0}' is invalid because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidExactRoute = new DiagnosticDescriptor(
            id: InvalidExactRouteId,
            title: "Medium message cannot form an exact route",
            messageFormat: "Message '{0}' cannot form an exact Medium route because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidLifetime = new DiagnosticDescriptor(
            id: InvalidLifetimeId,
            title: "Medium lifetime value is invalid",
            messageFormat: "Medium declaration '{0}' is invalid because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidBehavior = new DiagnosticDescriptor(
            id: InvalidBehaviorId,
            title: "Medium behavior declaration is invalid",
            messageFormat: "Behavior '{0}' is invalid because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor DuplicateBehavior = new DiagnosticDescriptor(
            id: DuplicateBehaviorId,
            title: "Medium behavior is declared more than once",
            messageFormat: "Behavior '{0}' has duplicate declarations",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidClosedBehavior = new DiagnosticDescriptor(
            id: InvalidClosedBehaviorId,
            title: "Medium selected behavior closure is invalid",
            messageFormat: "Behavior '{0}' cannot be used for route '{1}' because {2}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidManifest = new DiagnosticDescriptor(
            id: InvalidManifestId,
            title: "Medium manifest is invalid",
            messageFormat: "Manifest from referenced assembly '{0}' is invalid because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor MissingPublishRoute = new DiagnosticDescriptor(
            id: MissingPublishRouteId,
            title: "Medium Publish use has no exact route",
            messageFormat: "Message '{0}' has no valid exact Publish route; at least one handler is required",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static DiagnosticDescriptor ForRule(string ruleId)
        {
            switch (ruleId)
            {
                case MissingSendRouteId:
                    return MissingSendRoute;
                case DuplicateCommandRouteId:
                    return DuplicateCommandRoute;
                case InaccessibleRouteId:
                    return InaccessibleRoute;
                case InvalidHandlerId:
                    return InvalidHandler;
                case InvalidExactRouteId:
                    return InvalidExactRoute;
                case InvalidLifetimeId:
                    return InvalidLifetime;
                case InvalidBehaviorId:
                    return InvalidBehavior;
                case DuplicateBehaviorId:
                    return DuplicateBehavior;
                case InvalidClosedBehaviorId:
                    return InvalidClosedBehavior;
                case InvalidManifestId:
                    return InvalidManifest;
                case MissingPublishRouteId:
                    return MissingPublishRoute;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(ruleId), ruleId, "Unknown Medium rule");
            }
        }
    }
}
