using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSteroid.Generator.Medium
{
    /// <summary>The sole symbol-to-DTO boundary for source handlers, imported handlers, and root facts.</summary>
    internal static partial class FactsReader
    {
        internal static HandlerDeclarationModel? ReadSourceHandler(GeneratorSyntaxContext context)
        {
            if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol symbol)
                return null;
            var contracts = MediumRuntimeContracts.Resolve(context.SemanticModel.Compilation);
            return contracts is null ? null : ReadHandler(symbol, context.SemanticModel.Compilation, contracts);
        }

        internal static HandlerDeclarationModel? ReadHandler(
            INamedTypeSymbol symbol,
            Compilation compilation,
            MediumRuntimeContracts contracts)
        {
            if (symbol.TypeKind != TypeKind.Class)
                return null;

            var routes = ImmutableArray.CreateBuilder<HandlerRouteModel>();
            foreach (var contract in symbol.Interfaces)
            {
                RouteKind kind;
                TypeRef? response = null;
                bool responseIsAccessible = true;
                if (SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, contracts.CommandHandler))
                {
                    kind = RouteKind.Command;
                }
                else if (SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, contracts.CommandHandlerOfT))
                {
                    kind = RouteKind.CommandWithResponse;
                    response = CreateTypeRef(contract.TypeArguments[1]);
                    responseIsAccessible = IsAccessibleType(contract.TypeArguments[1], compilation.Assembly);
                }
                else if (SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, contracts.MessageHandler))
                {
                    kind = RouteKind.Message;
                }
                else
                {
                    continue;
                }

                if (contract.TypeArguments.Length == 0)
                    continue;
                var message = CreateTypeRef(contract.TypeArguments[0]);
                routes.Add(new HandlerRouteModel(
                    kind,
                    message,
                    response,
                    IsAccessibleType(contract.TypeArguments[0], compilation.Assembly),
                    responseIsAccessible,
                    GetSymbolLocation(contract.TypeArguments[0], symbol)));
            }

            if (routes.Count == 0)
                return null;

            routes.Sort(static (left, right) =>
            {
                var result = string.CompareOrdinal(left.Message.RouteKey, right.Message.RouteKey);
                if (result != 0)
                    return result;
                result = left.Kind.CompareTo(right.Kind);
                return result != 0
                    ? result
                    : string.CompareOrdinal(left.Response?.RouteKey, right.Response?.RouteKey);
            });

            var lifetime = MapServiceLifetime(ReadConstructorInt32(
                symbol,
                contracts.HandlerLifetimeAttribute,
                1,
                out _,
                out var lifetimeLocation));
            return new HandlerDeclarationModel(
                CreateTypeRef(symbol),
                ReadServiceShape(symbol),
                lifetime,
                lifetimeLocation,
                new EquatableArray<HandlerRouteModel>(routes.ToImmutable()),
                IsAccessibleType(symbol, compilation.Assembly),
                GetSymbolLocation(symbol, symbol));
        }

        internal static CompilationFacts ReadCompilationFacts(
            Compilation compilation,
            MediumRuntimeContracts contracts)
        {
            AttributeData? rootAttribute = null;
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, contracts.CompositionRootAttribute))
                {
                    rootAttribute = attribute;
                    break;
                }
            }

            var behaviors = ReadBehaviors(compilation, contracts);
            if (rootAttribute is null)
            {
                return new CompilationFacts(
                    false,
                    behaviors.Declarations,
                    Empty<HandlerDeclarationModel>(),
                    Empty<RuleIssue>());
            }

            var rootLocation = rootAttribute.ApplicationSyntaxReference is { } rootReference
                ? CreateLocation(rootReference.SyntaxTree, rootReference.Span)
                : new LocationInfo(string.Empty, default, default);
            var imported = ImmutableArray.CreateBuilder<HandlerDeclarationModel>();
            var issues = ImmutableArray.CreateBuilder<RuleIssue>();
            var assemblies = new List<IAssemblySymbol>();
            foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
                assemblies.Add(reference);
            assemblies.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

            foreach (var assembly in assemblies)
            {
                var attributes = new List<AttributeData>();
                foreach (var attribute in assembly.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, contracts.ManifestAttribute))
                        attributes.Add(attribute);
                }
                attributes.Sort(static (left, right) =>
                    string.CompareOrdinal(
                        left.ConstructorArguments.Length > 1 && left.ConstructorArguments[1].Value is INamedTypeSymbol leftType
                            ? CreateTypeRef(leftType).RouteKey
                            : string.Empty,
                        right.ConstructorArguments.Length > 1 && right.ConstructorArguments[1].Value is INamedTypeSymbol rightType
                            ? CreateTypeRef(rightType).RouteKey
                            : string.Empty));

                for (var attributeIndex = 0; attributeIndex < attributes.Count; attributeIndex++)
                {
                    var attribute = attributes[attributeIndex];
                    var validVersion = attribute.ConstructorArguments.Length == 2
                        && attribute.ConstructorArguments[0].Value is int version
                        && version == 1;
                    var handlerType = attribute.ConstructorArguments.Length == 2
                        ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol
                        : null;
                    if (!validVersion || handlerType is null)
                    {
                        issues.Add(Issue(
                            MediumDiagnostics.InvalidManifestId,
                            assembly.Name + "\0" + attributeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            rootLocation,
                            assembly.Name,
                            "the manifest version or handler payload is invalid"));
                        continue;
                    }

                    var handler = ReadHandler(handlerType, compilation, contracts);
                    if (handler is null)
                    {
                        issues.Add(Issue(
                            MediumDiagnostics.InvalidManifestId,
                            assembly.Name + "\0" + CreateTypeRef(handlerType).RouteKey,
                            rootLocation,
                            assembly.Name,
                            "the manifest handler has no directly declared supported handler interface"));
                        continue;
                    }
                    imported.Add(handler);
                }
            }

            imported.Sort(static (left, right) => string.CompareOrdinal(left.HandlerType.RouteKey, right.HandlerType.RouteKey));
            return new CompilationFacts(
                true,
                behaviors.Declarations,
                new EquatableArray<HandlerDeclarationModel>(imported.ToImmutable()),
                new EquatableArray<RuleIssue>(issues.ToImmutable()));
        }

        internal static TypeRef CreateTypeRef(ITypeSymbol symbol) => CreateTypeRef(symbol, true);

        private static TypeRef CreateTypeRef(ITypeSymbol symbol, bool includeRelations)
        {
            var namedSymbol = symbol as INamedTypeSymbol;
            var metadataIdentity = namedSymbol is not null
                ? BuildMetadataIdentity(namedSymbol)
                : symbol.ToDisplayString();
            var assemblyName = symbol.ContainingAssembly?.Name ?? string.Empty;
            return new TypeRef(
                BuildRouteKey(symbol) + "|" + assemblyName,
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                assemblyName,
                symbol.IsValueType,
                metadataIdentity,
                ReadTypeKind(symbol),
                ReadTypeArguments(symbol, includeRelations),
                symbol is INamedTypeSymbol { IsAbstract: true },
                ContainsTypeParameters(symbol),
                ContainsPointer(symbol),
                includeRelations && namedSymbol?.BaseType is not null
                    ? CreateTypeRef(namedSymbol.BaseType, true)
                    : null,
                includeRelations ? ReadAllInterfaces(symbol) : Empty<TypeRef>(),
                symbol.IsReferenceType,
                symbol.IsUnmanagedType,
                HasPublicParameterlessConstructor(symbol));
        }

        private static EquatableArray<TypeRef> ReadAllInterfaces(ITypeSymbol symbol)
        {
            if (symbol is not INamedTypeSymbol named)
                return Empty<TypeRef>();
            var interfaces = ImmutableArray.CreateBuilder<TypeRef>(named.AllInterfaces.Length);
            foreach (var contract in named.AllInterfaces)
                interfaces.Add(CreateTypeRef(contract, false));
            interfaces.Sort(static (left, right) => string.CompareOrdinal(left.RouteKey, right.RouteKey));
            return new EquatableArray<TypeRef>(interfaces.ToImmutable());
        }

        private static EquatableArray<TypeRef> ReadTypeArguments(ITypeSymbol symbol, bool includeRelations)
        {
            if (symbol is not INamedTypeSymbol named || named.TypeArguments.Length == 0)
                return Empty<TypeRef>();
            var arguments = ImmutableArray.CreateBuilder<TypeRef>(named.TypeArguments.Length);
            foreach (var argument in named.TypeArguments)
                arguments.Add(CreateTypeRef(argument, includeRelations));
            return new EquatableArray<TypeRef>(arguments.ToImmutable());
        }

        private static TypeRefKind ReadTypeKind(ITypeSymbol symbol)
        {
            switch (symbol)
            {
                case IArrayTypeSymbol: return TypeRefKind.Array;
                case IPointerTypeSymbol: return TypeRefKind.Pointer;
                case ITypeParameterSymbol: return TypeRefKind.TypeParameter;
                case IFunctionPointerTypeSymbol: return TypeRefKind.FunctionPointer;
                case INamedTypeSymbol { TypeKind: TypeKind.Interface }: return TypeRefKind.Interface;
                case INamedTypeSymbol: return TypeRefKind.Named;
                default: return TypeRefKind.Other;
            }
        }

        private static bool HasPublicParameterlessConstructor(ITypeSymbol symbol)
        {
            if (symbol.IsValueType)
                return true;
            if (symbol is not INamedTypeSymbol { IsAbstract: false } named)
                return false;
            foreach (var constructor in named.InstanceConstructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 0)
                    return true;
            }
            return false;
        }

        private static bool ContainsTypeParameters(ITypeSymbol symbol)
        {
            switch (symbol)
            {
                case ITypeParameterSymbol: return true;
                case IArrayTypeSymbol array: return ContainsTypeParameters(array.ElementType);
                case IPointerTypeSymbol pointer: return ContainsTypeParameters(pointer.PointedAtType);
                case INamedTypeSymbol named:
                    foreach (var argument in named.TypeArguments)
                    {
                        if (ContainsTypeParameters(argument))
                            return true;
                    }
                    return named.ContainingType is not null && ContainsTypeParameters(named.ContainingType);
                default: return false;
            }
        }

        private static bool ContainsPointer(ITypeSymbol symbol)
        {
            switch (symbol)
            {
                case IPointerTypeSymbol:
                case IFunctionPointerTypeSymbol:
                    return true;
                case IArrayTypeSymbol array:
                    return ContainsPointer(array.ElementType);
                case INamedTypeSymbol named:
                    foreach (var argument in named.TypeArguments)
                    {
                        if (ContainsPointer(argument))
                            return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private static string BuildRouteKey(ITypeSymbol symbol)
        {
            switch (symbol)
            {
                case INamedTypeSymbol named:
                    var key = BuildMetadataIdentity(named);
                    if (named.TypeArguments.Length > 0)
                    {
                        key += "<";
                        for (int i = 0; i < named.TypeArguments.Length; i++)
                        {
                            if (i > 0)
                                key += ",";
                            key += BuildRouteKey(named.TypeArguments[i]);
                        }
                        key += ">";
                    }
                    return key;
                case IArrayTypeSymbol array:
                    return BuildRouteKey(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
                case IPointerTypeSymbol pointer:
                    return BuildRouteKey(pointer.PointedAtType) + "*";
                case ITypeParameterSymbol parameter:
                    return "!" + parameter.Name;
                default:
                    return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }

        private static string BuildMetadataIdentity(INamedTypeSymbol symbol)
        {
            var name = symbol.MetadataName;
            var containing = symbol.ContainingType;
            while (containing is not null)
            {
                name = containing.MetadataName + "+" + name;
                containing = containing.ContainingType;
            }
            var ns = symbol.ContainingNamespace;
            return ns is { IsGlobalNamespace: false } ? ns.ToDisplayString() + "." + name : name;
        }

        private static LocationInfo GetSymbolLocation(ISymbol symbol, ISymbol fallback)
        {
            if (symbol.DeclaringSyntaxReferences.Length > 0)
            {
                var reference = symbol.DeclaringSyntaxReferences[0];
                return CreateLocation(reference.SyntaxTree, reference.Span);
            }
            if (!ReferenceEquals(symbol, fallback) && fallback.DeclaringSyntaxReferences.Length > 0)
            {
                var reference = fallback.DeclaringSyntaxReferences[0];
                return CreateLocation(reference.SyntaxTree, reference.Span);
            }
            return new LocationInfo(string.Empty, default, default);
        }

        private static LocationInfo CreateLocation(SyntaxTree tree, TextSpan span)
        {
            var lineSpan = tree.GetLineSpan(span);
            return new LocationInfo(tree.FilePath ?? string.Empty, span, lineSpan.Span);
        }

        private static RuleIssue Issue(
            string ruleId,
            string stableKey,
            LocationInfo location,
            params string[] arguments) =>
            new(ruleId, stableKey, location, new EquatableArray<string>(ImmutableArray.Create(arguments)));

        private static EquatableArray<T> Empty<T>() where T : IEquatable<T> =>
            new(ImmutableArray<T>.Empty);
    }
}
