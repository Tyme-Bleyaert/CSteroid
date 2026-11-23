using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace CSteroid.Generator.Medium.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MediumAssemblyAttributeCodeFixProvider)), Shared]
    public sealed class MediumAssemblyAttributeCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            MediumDiagnostics.MissingCompositionRootId,
            MediumDiagnostics.UnregisteredBehaviorId);

        public override FixAllProvider? GetFixAllProvider() => null;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic is null || !await IsCurrentDiagnostic(context.Document, diagnostic, context.CancellationToken))
                return;

            var compilation = await context.Document.Project.GetCompilationAsync(context.CancellationToken);
            if (compilation is null)
                return;

            if (diagnostic.Id == MediumDiagnostics.MissingCompositionRootId)
            {
                if (HasCompositionRoot(compilation))
                    return;
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Add Medium composition root",
                        cancellationToken => AddAttributeAsync(context.Document, diagnostic, cancellationToken),
                        MediumDiagnostics.MissingCompositionRootId),
                    diagnostic);
                return;
            }

            if (!TryGetProperty(diagnostic, MediumDiagnosticProperties.BehaviorTypeOfOperand, out _)
                || !TryGetProperty(diagnostic, MediumDiagnosticProperties.BehaviorIdentity, out var identity)
                || HasBehavior(compilation, identity))
            {
                return;
            }
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Declare Medium behavior",
                    cancellationToken => AddAttributeAsync(context.Document, diagnostic, cancellationToken),
                    MediumDiagnostics.UnregisteredBehaviorId + ":" + identity),
                diagnostic);
        }

        private static async Task<Solution> AddAttributeAsync(
            Document document,
            Diagnostic diagnostic,
            CancellationToken cancellationToken)
        {
            var compilation = await document.Project.GetCompilationAsync(cancellationToken);
            if (compilation is null || await IsAlreadyDeclared(document.Project, diagnostic, cancellationToken))
                return document.Project.Solution;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root is not CompilationUnitSyntax unit)
                return document.Project.Solution;

            var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName(
                diagnostic.Id == MediumDiagnostics.MissingCompositionRootId
                    ? "global::CSteroid.Medium.MediumCompositionRoot"
                    : "global::CSteroid.Medium.MediumBehavior"));
            if (diagnostic.Id == MediumDiagnostics.UnregisteredBehaviorId)
            {
                if (!TryGetProperty(diagnostic, MediumDiagnosticProperties.BehaviorTypeOfOperand, out var operand))
                    return document.Project.Solution;
                attribute = attribute.WithArgumentList(SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(SyntaxFactory.AttributeArgument(
                        SyntaxFactory.ParseExpression("typeof(" + operand + ")")))));
            }

            var attributeList = SyntaxFactory.AttributeList(
                    SyntaxFactory.SingletonSeparatedList(attribute))
                .WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.AssemblyKeyword)))
                .WithAdditionalAnnotations(Formatter.Annotation);
            var changedUnit = unit.AddAttributeLists(attributeList);
            if (diagnostic.Id == MediumDiagnostics.MissingCompositionRootId
                && !unit.Usings.Any(static usingDirective =>
                    usingDirective.Alias is null
                    && usingDirective.Name?.ToString() is "CSteroid.Medium" or "global::CSteroid.Medium"))
            {
                changedUnit = changedUnit.AddUsings(
                    SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("CSteroid.Medium"))
                        .WithAdditionalAnnotations(Formatter.Annotation));
            }

            var changedDocument = document.WithSyntaxRoot(changedUnit);
            changedDocument = await Formatter.FormatAsync(
                changedDocument,
                Formatter.Annotation,
                options: null,
                cancellationToken: cancellationToken);
            return changedDocument.Project.Solution;
        }

        private static async Task<bool> IsAlreadyDeclared(
            Project project,
            Diagnostic diagnostic,
            CancellationToken cancellationToken)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
                return true;
            return diagnostic.Id == MediumDiagnostics.MissingCompositionRootId
                ? HasCompositionRoot(compilation)
                : TryGetProperty(diagnostic, MediumDiagnosticProperties.BehaviorIdentity, out var identity)
                    && HasBehavior(compilation, identity);
        }

        private static bool HasCompositionRoot(Compilation compilation)
        {
            var attributeType = compilation.GetTypeByMetadataName(
                MediumRuntimeContracts.CompositionRootAttributeMetadataName);
            if (attributeType is null)
                return false;
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                    return true;
            }
            return false;
        }

        private static bool HasBehavior(Compilation compilation, string identity)
        {
            var attributeType = compilation.GetTypeByMetadataName(MediumRuntimeContracts.BehaviorAttributeMetadataName);
            if (attributeType is null)
                return false;
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)
                    || attribute.ConstructorArguments.Length != 1
                    || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol behaviorType)
                {
                    continue;
                }
                var type = FactsReader.CreateTypeRef(behaviorType);
                if (string.Equals(type.MetadataIdentity + "|" + type.AssemblyName, identity, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static async Task<bool> IsCurrentDiagnostic(
            Document document,
            Diagnostic diagnostic,
            CancellationToken cancellationToken)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            return tree is not null && diagnostic.Location.SourceTree == tree;
        }

        private static bool TryGetProperty(Diagnostic diagnostic, string key, out string value)
        {
            if (diagnostic.Properties.TryGetValue(key, out var result) && result is not null)
            {
                value = result;
                return true;
            }
            value = string.Empty;
            return false;
        }
    }
}
