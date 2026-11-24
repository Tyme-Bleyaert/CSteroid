using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Shared
{
    /// <summary>
    /// Finds [CanonicalModel] interfaces in referenced assemblies that reference CSteroid.Core.
    /// </summary>
    public static class ReferencedInterfaceDiscovery
    {
        internal static List<InterfaceSpec> ReadReferenced(Compilation compilation, CancellationToken cancellationToken)
        {
            var interfaces = new List<InterfaceSpec>();
            var canonicalAttrSymbol = compilation.GetTypeByMetadataName("CSteroid.CanonicalModelAttribute");
            if (canonicalAttrSymbol is null)
                return interfaces;

            var attrName = canonicalAttrSymbol.ToDisplayString();
            var visitedAssemblies = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);

            foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ReferencesCSteroidCore(referencedAssembly) && visitedAssemblies.Add(referencedAssembly))
                {
                    CollectInterfacesFromNamespace(
                        referencedAssembly.GlobalNamespace,
                        attrName,
                        interfaces,
                        cancellationToken);
                }
            }

            return interfaces;
        }

        private static bool ReferencesCSteroidCore(IAssemblySymbol assembly)
        {
            // In-memory metadata references do not reliably resolve the Core attribute through
            // GetTypeByMetadataName when the consumer also references Core. Assembly references
            // work in both metadata and in-memory scenarios.
            foreach (var module in assembly.Modules)
            {
                foreach (var reference in module.ReferencedAssemblySymbols)
                {
                    if (reference.Name == "CSteroid.Core")
                        return true;
                }
            }

            return false;
        }

        private static void CollectInterfacesFromNamespace(
            INamespaceSymbol ns,
            string canonicalAttrName,
            List<InterfaceSpec> interfaces,
            CancellationToken cancellationToken)
        {
            foreach (var member in ns.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member is INamespaceSymbol childNamespace)
                {
                    CollectInterfacesFromNamespace(childNamespace, canonicalAttrName, interfaces, cancellationToken);
                }
                else if (member is INamedTypeSymbol type)
                {
                    CollectInterfacesFromType(type, canonicalAttrName, interfaces, cancellationToken);
                }
            }
        }

        private static void CollectInterfacesFromType(
            INamedTypeSymbol type,
            string canonicalAttrName,
            List<InterfaceSpec> interfaces,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (type.TypeKind == TypeKind.Interface
                && type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == canonicalAttrName))
            {
                interfaces.Add(ModelInfoFactory.BuildInterfaceInfo(type, null, cancellationToken));
            }

            foreach (var nested in type.GetTypeMembers())
                CollectInterfacesFromType(nested, canonicalAttrName, interfaces, cancellationToken);
        }
    }
}
