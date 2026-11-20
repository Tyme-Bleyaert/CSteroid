using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.AutoMediator
{
    internal static class AutoMediatorDiagnostics
    {
        internal const string MissingSendRouteId = "CSAM001";
        internal const string DuplicateCommandRouteId = "CSAM002";
        internal const string InaccessibleRouteId = "CSAM006";
        internal const string InvalidHandlerId = "CSAM011";
        internal const string InvalidExactRouteId = "CSAM012";
        internal const string InvalidLifetimeId = "CSAM013";
        internal const string InvalidBehaviorId = "CSAM014";
        internal const string DuplicateBehaviorId = "CSAM015";
        internal const string InvalidClosedBehaviorId = "CSAM016";
        internal const string InvalidManifestId = "CSAM017";

        private const string Category = "CSteroid.AutoMediator";

        internal static readonly DiagnosticDescriptor MissingSendRoute = new DiagnosticDescriptor(
            id: MissingSendRouteId,
            title: "AutoMediator Send use has no exact route",
            messageFormat: "Command '{0}' has no valid exact {1} Send route",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor DuplicateCommandRoute = new DiagnosticDescriptor(
            id: DuplicateCommandRouteId,
            title: "AutoMediator command has multiple handler routes",
            messageFormat: "Handler '{0}' duplicates the command route for '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InaccessibleRoute = new DiagnosticDescriptor(
            id: InaccessibleRouteId,
            title: "AutoMediator route declaration is inaccessible",
            messageFormat: "AutoMediator cannot use '{0}' because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidHandler = new DiagnosticDescriptor(
            id: InvalidHandlerId,
            title: "AutoMediator handler is invalid",
            messageFormat: "Handler '{0}' is invalid because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidExactRoute = new DiagnosticDescriptor(
            id: InvalidExactRouteId,
            title: "AutoMediator message cannot form an exact route",
            messageFormat: "Message '{0}' cannot form an exact AutoMediator route because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidLifetime = new DiagnosticDescriptor(
            id: InvalidLifetimeId,
            title: "AutoMediator lifetime value is invalid",
            messageFormat: "AutoMediator declaration '{0}' is invalid because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidBehavior = new DiagnosticDescriptor(
            id: InvalidBehaviorId,
            title: "AutoMediator behavior declaration is invalid",
            messageFormat: "Behavior '{0}' is invalid because {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor DuplicateBehavior = new DiagnosticDescriptor(
            id: DuplicateBehaviorId,
            title: "AutoMediator behavior is declared more than once",
            messageFormat: "Behavior '{0}' has duplicate declarations",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidClosedBehavior = new DiagnosticDescriptor(
            id: InvalidClosedBehaviorId,
            title: "AutoMediator selected behavior closure is invalid",
            messageFormat: "Behavior '{0}' cannot be used for route '{1}' because {2}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor InvalidManifest = new DiagnosticDescriptor(
            id: InvalidManifestId,
            title: "AutoMediator manifest is invalid",
            messageFormat: "Manifest from referenced assembly '{0}' is invalid because {1}",
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
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(ruleId), ruleId, "Unknown AutoMediator rule");
            }
        }
    }
}
