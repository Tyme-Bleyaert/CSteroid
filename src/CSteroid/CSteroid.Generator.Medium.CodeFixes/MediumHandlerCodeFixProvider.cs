using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSteroid.Generator.Medium.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MediumHandlerCodeFixProvider)), Shared]
    public sealed class MediumHandlerCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            MediumDiagnostics.MissingSendRouteId,
            MediumDiagnostics.MissingPublishRouteId,
            MediumDiagnostics.MissingStreamRouteId);

        public override FixAllProvider? GetFixAllProvider() => null;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic is null
                || !TryGetRequest(diagnostic, out var request)
                || !await IsCurrentDiagnostic(context.Document, diagnostic, context.CancellationToken))
            {
                return;
            }

            var compilation = await context.Document.Project.GetCompilationAsync(context.CancellationToken);
            if (compilation is null
                || !TryGetMessage(compilation, request, out _)
                || HasHandler(compilation, request))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Add Medium handler for " + request.MessageGlobalName,
                    cancellationToken => AddHandlerAsync(context.Document, request, cancellationToken),
                    diagnostic.Id + ":" + request.MessageMetadataIdentity + ":" + request.Kind),
                diagnostic);
        }

        private static async Task<Solution> AddHandlerAsync(
            Document document,
            RouteRequest request,
            CancellationToken cancellationToken)
        {
            var project = document.Project;
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null
                || !TryGetMessage(compilation, request, out var message)
                || HasHandler(compilation, request))
            {
                return project.Solution;
            }

            var baseName = GetHandlerBaseName(request.MessageMetadataIdentity);
            var className = baseName;
            var documentName = className + ".cs";
            for (var suffix = 2; TypeOrDocumentExists(project, compilation, request.Namespace, className, documentName); suffix++)
            {
                className = baseName + suffix.ToString(CultureInfo.InvariantCulture);
                documentName = className + ".cs";
            }

            var directory = Path.GetDirectoryName(document.FilePath);
            var filePath = string.IsNullOrEmpty(directory) ? documentName : Path.Combine(directory, documentName);
            var source = CreateHandlerSource(request, className);
            var added = project.AddDocument(
                documentName,
                SourceText.From(source, Encoding.UTF8),
                document.Folders,
                filePath);
            return added.Project.Solution;
        }

        private static bool TypeOrDocumentExists(
            Project project,
            Compilation compilation,
            string namespaceName,
            string className,
            string documentName)
        {
            var metadataName = namespaceName.Length == 0 ? className : namespaceName + "." + className;
            if (compilation.GetTypeByMetadataName(metadataName) is not null)
                return true;
            return project.Documents.Any(document => string.Equals(document.Name, documentName, StringComparison.OrdinalIgnoreCase));
        }

        private static string CreateHandlerSource(RouteRequest request, string className)
        {
            var source = new StringBuilder();
            source.AppendLine("#nullable enable");
            source.AppendLine();
            var indent = string.Empty;
            if (request.Namespace.Length != 0)
            {
                source.AppendLine("namespace " + EscapeNamespace(request.Namespace));
                source.AppendLine("{");
                indent = "    ";
            }

            source.Append(indent).Append("internal sealed class ").Append(className).Append(" : ");
            switch (request.Kind)
            {
                case RouteKind.Command:
                    source.Append("global::CSteroid.Medium.ICommandHandler<").Append(request.MessageGlobalName).AppendLine(">");
                    break;
                case RouteKind.CommandWithResponse:
                    source.Append("global::CSteroid.Medium.ICommandHandler<")
                        .Append(request.MessageGlobalName)
                        .Append(", ")
                        .Append(request.ResponseGlobalName)
                        .AppendLine(">");
                    break;
                case RouteKind.Stream:
                    source.Append("global::CSteroid.Medium.IStreamCommandHandler<")
                        .Append(request.MessageGlobalName)
                        .Append(", ")
                        .Append(request.ResponseGlobalName)
                        .AppendLine(">");
                    break;
                default:
                    source.Append("global::CSteroid.Medium.IMessageHandler<").Append(request.MessageGlobalName).AppendLine(">");
                    break;
            }
            source.Append(indent).AppendLine("{");
            source.Append(indent).Append("    public ");
            if (request.Kind == RouteKind.CommandWithResponse)
            {
                source.Append("global::System.Threading.Tasks.ValueTask<")
                    .Append(request.ResponseGlobalName)
                    .Append("> Handle(");
            }
            else if (request.Kind == RouteKind.Stream)
            {
                source.Append("async global::System.Collections.Generic.IAsyncEnumerable<")
                    .Append(request.ResponseGlobalName)
                    .Append("> Handle(");
            }
            else
            {
                source.Append("global::System.Threading.Tasks.ValueTask Handle(");
            }
            source.Append(request.MessageGlobalName)
                .Append(request.Kind == RouteKind.Message ? " message, " : " command, ");
            if (request.Kind == RouteKind.Stream)
                source.Append("[global::System.Runtime.CompilerServices.EnumeratorCancellation] ");
            source.AppendLine("global::System.Threading.CancellationToken cancellationToken)");
            source.Append(indent).AppendLine("    {");
            if (request.Kind == RouteKind.CommandWithResponse)
            {
                source.Append(indent)
                    .Append("        return new global::System.Threading.Tasks.ValueTask<")
                    .Append(request.ResponseGlobalName)
                    .Append(">(default(")
                    .Append(request.ResponseGlobalName)
                    .AppendLine(")!);");
            }
            else if (request.Kind == RouteKind.Stream)
            {
                source.Append(indent).AppendLine("        await global::System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);");
                source.Append(indent).AppendLine("        yield break;");
            }
            else
            {
                source.Append(indent).AppendLine("        return default;");
            }
            source.Append(indent).AppendLine("    }");
            source.Append(indent).AppendLine("}");
            if (request.Namespace.Length != 0)
                source.AppendLine("}");
            return source.ToString();
        }

        private static bool TryGetRequest(Diagnostic diagnostic, out RouteRequest request)
        {
            request = default;
            if (!TryGetProperty(diagnostic, MediumDiagnosticProperties.HandlerFixEligible, out var eligibleText)
                || !bool.TryParse(eligibleText, out var eligible)
                || !eligible
                || !TryGetProperty(diagnostic, MediumDiagnosticProperties.RouteKind, out var kindText)
                || !int.TryParse(kindText, NumberStyles.None, CultureInfo.InvariantCulture, out var kind)
                || kind < (int)RouteKind.Message
                || kind > (int)RouteKind.Stream
                || !TryGetProperty(diagnostic, MediumDiagnosticProperties.MessageGlobalName, out var messageGlobalName)
                || !TryGetProperty(diagnostic, MediumDiagnosticProperties.MessageMetadataIdentity, out var metadataIdentity)
                || !TryGetProperty(diagnostic, MediumDiagnosticProperties.MessageAssemblyName, out var assemblyName)
                || !TryGetProperty(diagnostic, MediumDiagnosticProperties.ResponseGlobalName, out var responseGlobalName)
                || ((RouteKind)kind == RouteKind.CommandWithResponse || (RouteKind)kind == RouteKind.Stream)
                    && responseGlobalName.Length == 0)
            {
                return false;
            }

            request = new RouteRequest(
                (RouteKind)kind,
                messageGlobalName,
                responseGlobalName,
                metadataIdentity,
                assemblyName,
                GetNamespace(metadataIdentity));
            return true;
        }

        private static bool TryGetMessage(
            Compilation compilation,
            RouteRequest request,
            out INamedTypeSymbol message)
        {
            message = compilation.GetTypeByMetadataName(request.MessageMetadataIdentity)!;
            if (message is null
                || !string.Equals(message.ContainingAssembly?.Name, request.AssemblyName, StringComparison.Ordinal)
                || !string.Equals(
                    message.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    request.MessageGlobalName,
                    StringComparison.Ordinal))
            {
                return false;
            }
            if (request.Kind != RouteKind.CommandWithResponse && request.Kind != RouteKind.Stream)
                return true;

            var commandContract = compilation.GetTypeByMetadataName(request.Kind == RouteKind.Stream
                ? MediumRuntimeContracts.StreamCommandOfTMetadataName
                : MediumRuntimeContracts.CommandOfTMetadataName);
            return commandContract is not null
                && message.AllInterfaces.Any(contract =>
                    SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, commandContract)
                    && contract.TypeArguments.Length == 1
                    && string.Equals(
                        contract.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        request.ResponseGlobalName,
                        StringComparison.Ordinal));
        }

        private static bool HasHandler(Compilation compilation, RouteRequest request)
        {
            if (!TryGetMessage(compilation, request, out var message))
                return false;
            var handlerContract = compilation.GetTypeByMetadataName(request.Kind switch
            {
                RouteKind.Command => MediumRuntimeContracts.CommandHandlerMetadataName,
                RouteKind.CommandWithResponse => MediumRuntimeContracts.CommandHandlerOfTMetadataName,
                RouteKind.Stream => MediumRuntimeContracts.StreamCommandHandlerMetadataName,
                _ => MediumRuntimeContracts.MessageHandlerMetadataName,
            });
            if (handlerContract is null)
                return false;

            foreach (var type in EnumerateTypes(compilation.Assembly.GlobalNamespace))
            {
                foreach (var contract in type.Interfaces)
                {
                    if (!SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, handlerContract)
                        || contract.TypeArguments.Length == 0
                        || !SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], message))
                    {
                        continue;
                    }
                    if (request.Kind != RouteKind.CommandWithResponse
                        && request.Kind != RouteKind.Stream
                        || contract.TypeArguments.Length == 2
                            && string.Equals(
                                contract.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                request.ResponseGlobalName,
                                StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol namespaceSymbol)
        {
            foreach (var type in namespaceSymbol.GetTypeMembers())
            {
                foreach (var current in EnumerateNestedTypes(type))
                    yield return current;
            }
            foreach (var child in namespaceSymbol.GetNamespaceMembers())
            {
                foreach (var type in EnumerateTypes(child))
                    yield return type;
            }
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nested in type.GetTypeMembers())
            {
                foreach (var current in EnumerateNestedTypes(nested))
                    yield return current;
            }
        }

        private static string GetHandlerBaseName(string metadataIdentity)
        {
            var name = new StringBuilder("Medium");
            foreach (var character in metadataIdentity)
                name.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            return name.Append("Handler").ToString();
        }

        private static string EscapeNamespace(string namespaceName) =>
            string.Join(".", namespaceName.Split('.').Select(EscapeIdentifier));

        private static string EscapeIdentifier(string identifier) =>
            SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
                ? "@" + identifier
                : identifier;

        private static string GetNamespace(string metadataIdentity)
        {
            var nested = metadataIdentity.IndexOf('+');
            var end = nested < 0 ? metadataIdentity.Length - 1 : nested - 1;
            if (end < 0)
                return string.Empty;
            var separator = metadataIdentity.LastIndexOf('.', end);
            return separator < 0 ? string.Empty : metadataIdentity.Substring(0, separator);
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

        private readonly struct RouteRequest
        {
            internal RouteRequest(
                RouteKind kind,
                string messageGlobalName,
                string responseGlobalName,
                string messageMetadataIdentity,
                string assemblyName,
                string namespaceName)
            {
                Kind = kind;
                MessageGlobalName = messageGlobalName;
                ResponseGlobalName = responseGlobalName;
                MessageMetadataIdentity = messageMetadataIdentity;
                AssemblyName = assemblyName;
                Namespace = namespaceName;
            }

            internal RouteKind Kind { get; }
            internal string MessageGlobalName { get; }
            internal string ResponseGlobalName { get; }
            internal string MessageMetadataIdentity { get; }
            internal string AssemblyName { get; }
            internal string Namespace { get; }
        }
    }
}
