using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.AutoMediator
{
    internal static partial class FactsReader
    {
        internal static BehaviorSet ReadBehaviors(
            Compilation compilation,
            AutoMediatorRuntimeContracts contracts)
        {
            if (contracts.BehaviorAttribute is null)
                return new BehaviorSet(new EquatableArray<BehaviorDeclarationFact>(ImmutableArray<BehaviorDeclarationFact>.Empty));

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
                var typeParameterNames = ImmutableArray<string>.Empty;
                var constraints = ImmutableArray<BehaviorConstraintFact>.Empty;
                var contractsRead = ImmutableArray<BehaviorContractFact>.Empty;
                ServiceShapeFact? shape = null;
                var isOpenGenericDefinition = false;
                var globalDefinitionName = string.Empty;

                if (behaviorType is not null)
                {
                    var definition = behaviorType.IsUnboundGenericType
                        ? behaviorType.OriginalDefinition
                        : behaviorType;
                    shape = ReadServiceShape(definition);
                    isOpenGenericDefinition = behaviorType.IsUnboundGenericType;
                    globalDefinitionName = GetDefinitionGlobalName(CreateTypeRef(behaviorType));
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
                    contractsRead = ReadBehaviorContracts(definition, contracts);
                }

                declarations.Add(new BehaviorDeclarationFact(
                    behaviorType is null ? null : CreateTypeRef(behaviorType),
                    globalDefinitionName,
                    shape,
                    isOpenGenericDefinition,
                    new EquatableArray<string>(typeParameterNames),
                    new EquatableArray<BehaviorConstraintFact>(constraints),
                    new EquatableArray<BehaviorContractFact>(contractsRead),
                    ReadNamedInt32(attribute, "Order", 0),
                    MapServiceLifetime(ReadNamedInt32(attribute, "Lifetime", 1)),
                    location));
            }

            declarations.Sort(static (left, right) =>
            {
                var result = string.CompareOrdinal(left.StableKey, right.StableKey);
                return result != 0 ? result : left.Location.TextSpan.Start.CompareTo(right.Location.TextSpan.Start);
            });
            return new BehaviorSet(new EquatableArray<BehaviorDeclarationFact>(declarations.ToImmutable()));
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
            AutoMediatorRuntimeContracts contracts)
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

        private static string GetDefinitionGlobalName(TypeRef behaviorType)
        {
            var globalName = behaviorType.GlobalName;
            var genericStart = globalName.IndexOf('<');
            return genericStart < 0 ? globalName : globalName.Substring(0, genericStart);
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
