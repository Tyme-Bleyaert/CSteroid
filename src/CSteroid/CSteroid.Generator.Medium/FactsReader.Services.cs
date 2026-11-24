using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSteroid.Generator.Medium
{
    internal static partial class FactsReader
    {
        private static ServiceShapeFact ReadServiceShape(INamedTypeSymbol symbol)
        {
            var chainSymbols = new Stack<INamedTypeSymbol>();
            for (var current = symbol; current is not null; current = current.ContainingType)
                chainSymbols.Push(current);

            var chain = ImmutableArray.CreateBuilder<TypeDeclarationFact>(chainSymbols.Count);
            foreach (var current in chainSymbols)
                chain.Add(ReadTypeDeclaration(current));

            var constructors = ImmutableArray.CreateBuilder<ConstructorFact>(symbol.InstanceConstructors.Length);
            foreach (var constructor in symbol.InstanceConstructors)
                constructors.Add(ReadConstructor(constructor, symbol));
            constructors.Sort(static (left, right) => CompareLocations(left.Location, right.Location));
            return new ServiceShapeFact(
                new EquatableArray<TypeDeclarationFact>(chain.ToImmutable()),
                new EquatableArray<ConstructorFact>(constructors.ToImmutable()));
        }

        private static TypeDeclarationFact ReadTypeDeclaration(INamedTypeSymbol symbol)
        {
            TypeDeclarationSyntax? firstSyntax = null;
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is TypeDeclarationSyntax syntax)
                {
                    firstSyntax = syntax;
                    break;
                }
            }
            var location = firstSyntax is not null
                ? CreateLocation(firstSyntax.SyntaxTree, firstSyntax.Identifier.Span)
                : GetSymbolLocation(symbol, symbol);
            return new TypeDeclarationFact(
                ReadContainerKind(symbol, firstSyntax),
                symbol.TypeParameters.Length != 0,
                symbol.IsStatic,
                symbol.IsAbstract,
                symbol.IsFileLocal,
                ReadAccessibility(symbol.DeclaredAccessibility),
                location);
        }

        private static TypeContainerKind ReadContainerKind(INamedTypeSymbol symbol, TypeDeclarationSyntax? syntax)
        {
            if (syntax is RecordDeclarationSyntax)
                return symbol.TypeKind == TypeKind.Struct ? TypeContainerKind.RecordStruct : TypeContainerKind.RecordClass;
            switch (symbol.TypeKind)
            {
                case TypeKind.Class: return TypeContainerKind.Class;
                case TypeKind.Struct: return TypeContainerKind.Struct;
                case TypeKind.Interface: return TypeContainerKind.Interface;
                default: return TypeContainerKind.Class;
            }
        }

        private static ConstructorFact ReadConstructor(IMethodSymbol constructor, INamedTypeSymbol fallbackSymbol)
        {
            var fallbackLocation = GetSymbolLocation(fallbackSymbol, fallbackSymbol);
            var location = fallbackLocation;
            if (constructor.DeclaringSyntaxReferences.Length > 0)
            {
                var reference = constructor.DeclaringSyntaxReferences[0];
                location = reference.GetSyntax() is ConstructorDeclarationSyntax syntax
                    ? CreateLocation(reference.SyntaxTree, syntax.Identifier.Span)
                    : CreateLocation(reference.SyntaxTree, reference.Span);
            }

            var parameters = ImmutableArray.CreateBuilder<ConstructorParameterFact>(constructor.Parameters.Length);
            foreach (var parameter in constructor.Parameters)
            {
                parameters.Add(new ConstructorParameterFact(
                    CreateTypeRef(parameter.Type),
                    ReadParameterRefKind(parameter.RefKind),
                    parameter.Type is INamedTypeSymbol named && named.IsRefLikeType,
                    IsAccessibleType(parameter.Type, fallbackSymbol.ContainingAssembly)));
            }
            return new ConstructorFact(
                ReadAccessibility(constructor.DeclaredAccessibility),
                new EquatableArray<ConstructorParameterFact>(parameters.ToImmutable()),
                location);
        }

        private static bool IsAccessibleType(ITypeSymbol type, IAssemblySymbol consumerAssembly)
        {
            switch (type)
            {
                case ITypeParameterSymbol:
                    // Open shapes are checked after generic closure; their declaration accessibility is separate.
                    return true;
                case IArrayTypeSymbol array:
                    return IsAccessibleType(array.ElementType, consumerAssembly);
                case IPointerTypeSymbol pointer:
                    return IsAccessibleType(pointer.PointedAtType, consumerAssembly);
                case IFunctionPointerTypeSymbol:
                    return false;
                case INamedTypeSymbol named:
                    if (!IsAccessibleNamedType(named, consumerAssembly))
                        return false;
                    foreach (var argument in named.TypeArguments)
                    {
                        if (!IsAccessibleType(argument, consumerAssembly))
                            return false;
                    }
                    return named.ContainingType is null || IsAccessibleType(named.ContainingType, consumerAssembly);
                default:
                    return true;
            }
        }

        private static bool IsAccessibleNamedType(INamedTypeSymbol type, IAssemblySymbol consumerAssembly)
        {
            if (type.IsFileLocal)
                return false;
            if (type.DeclaredAccessibility == Accessibility.Public)
                return true;
            if (type.DeclaredAccessibility != Accessibility.Internal)
                return false;
            if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, consumerAssembly))
                return true;
            return HasInternalsVisibleTo(type.ContainingAssembly, consumerAssembly.Name);
        }

        private static bool HasInternalsVisibleTo(IAssemblySymbol? producer, string consumerName)
        {
            if (producer is null)
                return false;
            foreach (var attribute in producer.GetAttributes())
            {
                if (!string.Equals(
                        attribute.AttributeClass?.ToDisplayString(),
                        "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
                        StringComparison.Ordinal)
                    || attribute.ConstructorArguments.Length != 1
                    || attribute.ConstructorArguments[0].Value is not string value)
                {
                    continue;
                }
                var separator = value.IndexOf(',');
                var name = (separator < 0 ? value : value.Substring(0, separator)).Trim();
                if (string.Equals(name, consumerName, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static DeclaredAccessibilityModel ReadAccessibility(Accessibility accessibility)
        {
            switch (accessibility)
            {
                case Accessibility.Public: return DeclaredAccessibilityModel.Public;
                case Accessibility.Internal: return DeclaredAccessibilityModel.Internal;
                case Accessibility.Protected: return DeclaredAccessibilityModel.Protected;
                case Accessibility.ProtectedOrInternal: return DeclaredAccessibilityModel.ProtectedOrInternal;
                case Accessibility.ProtectedAndInternal: return DeclaredAccessibilityModel.ProtectedAndInternal;
                default: return DeclaredAccessibilityModel.Private;
            }
        }

        private static ParameterRefKind ReadParameterRefKind(RefKind refKind)
        {
            switch (refKind)
            {
                case RefKind.None: return ParameterRefKind.None;
                case RefKind.Ref: return ParameterRefKind.Ref;
                case RefKind.Out: return ParameterRefKind.Out;
                case RefKind.In: return ParameterRefKind.In;
                default: return ParameterRefKind.Other;
            }
        }

        private static int ReadNamedInt32(
            INamedTypeSymbol symbol,
            INamedTypeSymbol? attributeType,
            string name,
            int defaultValue)
        {
            if (attributeType is null)
                return defaultValue;
            foreach (var attribute in symbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                    continue;
                foreach (var argument in attribute.NamedArguments)
                {
                    if (string.Equals(argument.Key, name, StringComparison.Ordinal)
                        && argument.Value.Value is int value)
                    {
                        return value;
                    }
                }
            }
            return defaultValue;
        }

        private static int ReadConstructorInt32(
            INamedTypeSymbol symbol,
            INamedTypeSymbol? attributeType,
            int defaultValue,
            out bool hasExplicitValue,
            out LocationInfo? location)
        {
            hasExplicitValue = false;
            location = null;
            if (attributeType is null)
                return defaultValue;
            foreach (var attribute in symbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                    continue;
                hasExplicitValue = true;
                if (attribute.ApplicationSyntaxReference is { } reference)
                    location = CreateLocation(reference.SyntaxTree, reference.Span);
                if (attribute.ConstructorArguments.Length == 1
                    && attribute.ConstructorArguments[0].Value is int value)
                {
                    return value;
                }
            }
            return defaultValue;
        }

        private static int MapServiceLifetime(int lifetime)
        {
            switch (lifetime)
            {
                case 0: return (int)LifetimeModel.Singleton;
                case 1: return (int)LifetimeModel.Scoped;
                case 2: return (int)LifetimeModel.Transient;
                default: return lifetime;
            }
        }

        private static int CompareLocations(LocationInfo left, LocationInfo right)
        {
            var result = string.CompareOrdinal(left.FilePath, right.FilePath);
            return result != 0 ? result : left.TextSpan.Start.CompareTo(right.TextSpan.Start);
        }
    }
}
