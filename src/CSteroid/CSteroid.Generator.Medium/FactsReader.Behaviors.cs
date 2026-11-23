using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSteroid.Generator.Medium
{
    internal static partial class FactsReader
    {
        internal static EquatableArray<BehaviorDeclarationFact> ReadBehaviors(
            Compilation compilation,
            MediumRuntimeContracts contracts)
        {
            if (contracts.BehaviorAttribute is null)
                return new EquatableArray<BehaviorDeclarationFact>(ImmutableArray<BehaviorDeclarationFact>.Empty);

            var declarations = ImmutableArray.CreateBuilder<BehaviorDeclarationFact>();
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, contracts.BehaviorAttribute))
                    continue;

                var location = attribute.ApplicationSyntaxReference is { } reference
                    ? CreateLocation(reference.SyntaxTree, reference.Span)
                    : new LocationInfo(string.Empty, default, default);
                var behaviorType = attribute.ConstructorArguments.Length == 1
                    ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
                    : null;
                declarations.Add(behaviorType is null
                    ? new BehaviorDeclarationFact(
                        null,
                        null,
                        false,
                        new EquatableArray<string>(ImmutableArray<string>.Empty),
                        new EquatableArray<BehaviorConstraintFact>(ImmutableArray<BehaviorConstraintFact>.Empty),
                        new EquatableArray<BehaviorContractFact>(ImmutableArray<BehaviorContractFact>.Empty),
                        ReadNamedInt32(attribute, "Order", 0),
                        MapServiceLifetime(ReadNamedInt32(attribute, "Lifetime", 1)),
                        location)
                    : ReadBehaviorDeclaration(
                        behaviorType,
                        contracts,
                        location,
                        ReadNamedInt32(attribute, "Order", 0),
                        MapServiceLifetime(ReadNamedInt32(attribute, "Lifetime", 1))));
            }

            declarations.Sort(static (left, right) =>
            {
                var result = string.CompareOrdinal(left.StableKey, right.StableKey);
                return result != 0 ? result : left.Location.TextSpan.Start.CompareTo(right.Location.TextSpan.Start);
            });
            return new EquatableArray<BehaviorDeclarationFact>(declarations.ToImmutable());
        }

        internal static BehaviorDeclarationFact? ReadSourceBehavior(
            INamedTypeSymbol symbol,
            Compilation compilation,
            MediumRuntimeContracts contracts)
        {
            if (symbol.TypeKind != TypeKind.Class
                || ReadBehaviorContracts(symbol, contracts).Length == 0
                || !IsAccessibleType(symbol, compilation.Assembly))
            {
                return null;
            }

            var location = GetSymbolLocation(symbol, symbol);
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is TypeDeclarationSyntax declarationSyntax)
                {
                    location = CreateLocation(reference.SyntaxTree, declarationSyntax.Identifier.Span);
                    break;
                }
            }

            var declarationType = symbol.TypeParameters.Length == 0
                ? symbol
                : symbol.ConstructUnboundGenericType();
            var declaration = ReadBehaviorDeclaration(
                declarationType,
                contracts,
                location,
                order: 0,
                lifetime: (int)LifetimeModel.Scoped);
            var plan = BehaviorPolicy.BuildDeclarationPlan(
                new EquatableArray<BehaviorDeclarationFact>(ImmutableArray.Create(declaration)));
            return plan.ValidDeclarations.Count == 1 && BehaviorPolicy.HasUsableClosedShape(declaration)
                ? declaration
                : null;
        }

        private static BehaviorDeclarationFact ReadBehaviorDeclaration(
            INamedTypeSymbol behaviorType,
            MediumRuntimeContracts contracts,
            LocationInfo location,
            int order,
            int lifetime)
        {
            var definition = behaviorType.IsUnboundGenericType
                ? behaviorType.OriginalDefinition
                : behaviorType;
            var typeParameterNames = ImmutableArray<string>.Empty;
            var constraints = ImmutableArray<BehaviorConstraintFact>.Empty;
            if (definition.TypeParameters.Length != 0)
            {
                var names = ImmutableArray.CreateBuilder<string>(definition.TypeParameters.Length);
                var constraintBuilder = ImmutableArray.CreateBuilder<BehaviorConstraintFact>(definition.TypeParameters.Length);
                foreach (var parameter in definition.TypeParameters)
                {
                    names.Add(parameter.Name);
                    constraintBuilder.Add(ReadBehaviorConstraint(parameter));
                }
                typeParameterNames = names.ToImmutable();
                constraints = constraintBuilder.ToImmutable();
            }

            return new BehaviorDeclarationFact(
                CreateTypeRef(behaviorType),
                ReadServiceShape(definition),
                behaviorType.IsUnboundGenericType,
                new EquatableArray<string>(typeParameterNames),
                new EquatableArray<BehaviorConstraintFact>(constraints),
                new EquatableArray<BehaviorContractFact>(ReadBehaviorContracts(definition, contracts)),
                order,
                lifetime,
                location);
        }

        private static BehaviorConstraintFact ReadBehaviorConstraint(ITypeParameterSymbol parameter)
        {
            var special = BehaviorSpecialConstraints.None;
            if (parameter.HasReferenceTypeConstraint)
                special |= BehaviorSpecialConstraints.ReferenceType;
            if (parameter.HasValueTypeConstraint)
                special |= BehaviorSpecialConstraints.ValueType;
            if (parameter.HasConstructorConstraint)
                special |= BehaviorSpecialConstraints.Constructor;
            if (parameter.HasNotNullConstraint)
                special |= BehaviorSpecialConstraints.NotNull;
            if (parameter.HasUnmanagedTypeConstraint)
                special |= BehaviorSpecialConstraints.Unmanaged;

            var types = ImmutableArray.CreateBuilder<TypeRef>(parameter.ConstraintTypes.Length);
            foreach (var constraintType in parameter.ConstraintTypes)
                types.Add(CreateTypeRef(constraintType));
            types.Sort(static (left, right) => string.CompareOrdinal(left.RouteKey, right.RouteKey));
            return new BehaviorConstraintFact(
                parameter.Name,
                special,
                new EquatableArray<TypeRef>(types.ToImmutable()));
        }

        private static ImmutableArray<BehaviorContractFact> ReadBehaviorContracts(
            INamedTypeSymbol behaviorType,
            MediumRuntimeContracts contracts)
        {
            var result = ImmutableArray.CreateBuilder<BehaviorContractFact>();
            foreach (var contract in behaviorType.AllInterfaces)
            {
                if (contracts.PipelineBehavior is not null
                    && SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, contracts.PipelineBehavior)
                    && contract.TypeArguments.Length == 1)
                {
                    result.Add(new BehaviorContractFact(
                        BehaviorContractKind.NonResponse,
                        CreateTypeRef(contract.TypeArguments[0]),
                        null));
                }
                else if (contracts.PipelineBehaviorOfT is not null
                    && SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, contracts.PipelineBehaviorOfT)
                    && contract.TypeArguments.Length == 2)
                {
                    result.Add(new BehaviorContractFact(
                        BehaviorContractKind.Response,
                        CreateTypeRef(contract.TypeArguments[0]),
                        CreateTypeRef(contract.TypeArguments[1])));
                }
            }

            result.Sort(static (left, right) =>
            {
                var comparison = string.CompareOrdinal(left.MessageType.RouteKey, right.MessageType.RouteKey);
                if (comparison != 0)
                    return comparison;
                comparison = left.Kind.CompareTo(right.Kind);
                return comparison != 0
                    ? comparison
                    : string.CompareOrdinal(left.ResponseType?.RouteKey, right.ResponseType?.RouteKey);
            });
            return result.ToImmutable();
        }

        private static int ReadNamedInt32(AttributeData attribute, string name, int defaultValue)
        {
            foreach (var argument in attribute.NamedArguments)
            {
                if (string.Equals(argument.Key, name, StringComparison.Ordinal)
                    && argument.Value.Value is int value)
                {
                    return value;
                }
            }
            return defaultValue;
        }
    }
}
