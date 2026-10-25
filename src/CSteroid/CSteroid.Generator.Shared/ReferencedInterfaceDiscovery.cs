using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Shared
{
    /// <summary>
    /// Finds [CanonicalModel] interfaces outside the current compilation: in referenced
    /// assemblies that reference CSteroid.Core (the "metadata lib" pattern, where interfaces
    /// live in a shared library and each consuming project generates its own models).
    /// </summary>
    public static class ReferencedInterfaceDiscovery
    {
        /// <summary>
        /// Returns all in-source [CanonicalModel] interfaces merged with decorated interfaces
        /// from referenced CSteroid.Core-referencing assemblies. In-source symbols win on
        /// metadata-name collisions so a project can shadow a referenced definition.
        /// Assemblies without a CSteroid.Core reference are skipped: they cannot carry
        /// decorated interfaces, and walking them would waste generator time.
        /// </summary>
        public static List<INamedTypeSymbol> MergeWithReferenced(Compilation compilation, ImmutableArray<INamedTypeSymbol> sourceSymbols)
        {
            return MergeWithReferenced(compilation, sourceSymbols, out _);
        }

        /// <summary>
        /// Returns all in-source [CanonicalModel] interfaces merged with decorated interfaces
        /// from referenced CSteroid.Core-referencing assemblies. In-source symbols win on
        /// metadata-name collisions so a project can shadow a referenced definition.
        /// Assemblies without a CSteroid.Core reference are skipped: they cannot carry
        /// decorated interfaces, and walking them would waste generator time.
        /// <paramref name="foundReferencedInterfaces"/> tells callers whether any decorated
        /// interfaces came from referenced assemblies (used by config diagnostics).
        /// </summary>
        public static List<INamedTypeSymbol> MergeWithReferenced(Compilation compilation, ImmutableArray<INamedTypeSymbol> sourceSymbols, out bool foundReferencedInterfaces)
        {
            var merged = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            foreach (var symbol in sourceSymbols)
            {
                merged[symbol.ToDisplayString()] = symbol;
            }

            foundReferencedInterfaces = false;

            var canonicalAttrSymbol = compilation.GetTypeByMetadataName("CSteroid.CanonicalModelAttribute");
            if (canonicalAttrSymbol is null)
                return merged.Values.ToList();

            var attrName = canonicalAttrSymbol.ToDisplayString();
            var visitedAssemblies = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);

            foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (ReferencesCSteroidCore(referencedAssembly) && visitedAssemblies.Add(referencedAssembly))
                {
                    int countBefore = merged.Count;
                    CollectInterfacesFromNamespace(referencedAssembly.GlobalNamespace, attrName, merged);
                    foundReferencedInterfaces |= merged.Count > countBefore;
                }
            }

            return merged.Values.ToList();
        }

        private static bool ReferencesCSteroidCore(IAssemblySymbol assembly)
        {
            // NOTE: intentionally NOT assembly.GetTypeByMetadataName("CSteroid.CanonicalModel
            // Attribute"): for in-memory metadata references (MetadataReference.CreateFromImage,
            // used by the test harness) that lookup returns null when the referenced assembly's
            // own Core reference collides with the consumer's. Checking the module's assembly
            // references works in every scenario.
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
            Dictionary<string, INamedTypeSymbol> merged)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNamespace)
                {
                    CollectInterfacesFromNamespace(childNamespace, canonicalAttrName, merged);
                }
                else if (member is INamedTypeSymbol type)
                {
                    CollectInterfacesFromType(type, canonicalAttrName, merged);
                }
            }
        }

        private static void CollectInterfacesFromType(
            INamedTypeSymbol type,
            string canonicalAttrName,
            Dictionary<string, INamedTypeSymbol> merged)
        {
            if (type.TypeKind == TypeKind.Interface
                && type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == canonicalAttrName))
            {
                var key = type.ToDisplayString();
                if (!merged.ContainsKey(key))
                    merged.Add(key, type);
            }

            foreach (var nested in type.GetTypeMembers())
            {
                CollectInterfacesFromType(nested, canonicalAttrName, merged);
            }
        }
    }
}
