using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSteroid.Generator.Medium
{
    internal static partial class FactsReader
    {
        internal static SendUseFact? ReadSendUse(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            MediumRuntimeContracts contracts)
        {
            if (invocation.ArgumentList.Arguments.Count < 1 || invocation.ArgumentList.Arguments.Count > 2)
                return null;
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || !IsMediatorSendMethod(method, contracts))
            {
                return null;
            }

            var isStream = string.Equals(method.Name, "Stream", StringComparison.Ordinal);
            var argument = invocation.ArgumentList.Arguments[0].Expression;
            var commandType = ReadConcreteMessageType(
                argument,
                semanticModel,
                isStream ? contracts.StreamCommand : contracts.Command);
            if (commandType is null)
                return null;

            var kind = isStream
                ? RouteKind.Stream
                : method.IsGenericMethod
                    || method.Parameters[0].Type is INamedTypeSymbol named
                        && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, contracts.CommandOfT)
                    ? RouteKind.CommandWithResponse
                    : RouteKind.Command;
            ITypeSymbol? responseType = null;
            if (kind == RouteKind.CommandWithResponse || kind == RouteKind.Stream)
            {
                responseType = method.IsGenericMethod && method.TypeArguments.Length == 1
                    ? method.TypeArguments[0]
                    : ((INamedTypeSymbol)method.Parameters[0].Type).TypeArguments[0];
            }

            return new SendUseFact(
                CreateTypeRef(commandType),
                kind,
                responseType is null ? null : CreateTypeRef(responseType),
                IsAccessibleType(commandType, semanticModel.Compilation.Assembly),
                responseType is null || IsAccessibleType(responseType, semanticModel.Compilation.Assembly),
                CreateLocation(invocation.SyntaxTree, invocation.Span));
        }

        internal static PublishUseFact? ReadPublishUse(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            MediumRuntimeContracts contracts)
        {
            if (invocation.ArgumentList.Arguments.Count < 1 || invocation.ArgumentList.Arguments.Count > 2)
                return null;
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || !IsMediatorPublishMethod(method, contracts))
            {
                return null;
            }

            var argument = invocation.ArgumentList.Arguments[0].Expression;
            var messageType = ReadConcreteMessageType(argument, semanticModel, contracts.Message);
            if (messageType is null)
                return null;

            return new PublishUseFact(
                CreateTypeRef(messageType),
                IsAccessibleType(messageType, semanticModel.Compilation.Assembly),
                CreateLocation(invocation.SyntaxTree, invocation.Span));
        }

        private static INamedTypeSymbol? ReadConcreteMessageType(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            INamedTypeSymbol contract)
        {
            while (true)
            {
                if (expression is ParenthesizedExpressionSyntax parenthesized)
                {
                    expression = parenthesized.Expression;
                    continue;
                }
                if (expression is CastExpressionSyntax cast)
                {
                    expression = cast.Expression;
                    continue;
                }
                break;
            }

            if (semanticModel.GetTypeInfo(expression).Type is not INamedTypeSymbol type
                || type.TypeKind == TypeKind.Interface
                || type.IsAbstract
                || CreateTypeRef(type).ContainsTypeParameters
                || !ImplementsContract(type, contract))
            {
                return null;
            }
            return type;
        }

        private static bool IsMediatorSendMethod(
            IMethodSymbol method,
            MediumRuntimeContracts contracts)
        {
            var isSend = string.Equals(method.Name, "Send", StringComparison.Ordinal);
            var isStream = string.Equals(method.Name, "Stream", StringComparison.Ordinal);
            if (!isSend && !isStream
                || method.Parameters.Length < 1
                || method.Parameters.Length > 2
                || method.ContainingType.TypeKind != TypeKind.Interface)
            {
                return false;
            }

            var firstType = method.Parameters[0].Type;
            if (isStream)
            {
                return ImplementsContract(method.ContainingType, contracts.StreamSender)
                    && firstType is INamedTypeSymbol streamCommand
                    && SymbolEqualityComparer.Default.Equals(
                        streamCommand.OriginalDefinition,
                        contracts.StreamCommandOfT);
            }

            return ImplementsContract(method.ContainingType, contracts.Sender)
                && (SymbolEqualityComparer.Default.Equals(firstType, contracts.Command)
                    || firstType is INamedTypeSymbol named
                        && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, contracts.CommandOfT));
        }

        private static bool IsMediatorPublishMethod(
            IMethodSymbol method,
            MediumRuntimeContracts contracts)
        {
            if (!string.Equals(method.Name, "Publish", StringComparison.Ordinal)
                || method.Parameters.Length < 1
                || method.Parameters.Length > 2
                || method.ContainingType.TypeKind != TypeKind.Interface
                || !ImplementsContract(method.ContainingType, contracts.Publisher))
            {
                return false;
            }

            return SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contracts.Message);
        }

        private static bool ImplementsContract(INamedTypeSymbol type, INamedTypeSymbol contract)
        {
            if (SymbolEqualityComparer.Default.Equals(type, contract))
                return true;
            foreach (var item in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(item, contract))
                    return true;
            }
            return false;
        }
    }
}
