using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Shared.OutputConventions;

namespace CSteroid.Generator.Shared
{
    /// <summary>
    /// Decides which models/mappings a project emits in metadata-lib mode
    /// (see GeneratorConfigAttribute.GenerateOnlyNamespaces). With no filter
    /// configured, everything is local and every rule is permissive.
    /// </summary>
    public sealed class EmissionPolicy
    {
        private readonly string[] _generateOnlyNamespaces;

        public EmissionPolicy(string[] generateOnlyNamespaces)
        {
            _generateOnlyNamespaces = generateOnlyNamespaces;
        }

        public static EmissionPolicy For(Compilation compilation)
        {
            return new EmissionPolicy(GetGenerateOnlyNamespaces(compilation.Assembly));
        }

        private bool HasFilter => _generateOnlyNamespaces is { Length: > 0 };

        /// <summary>
        /// A model is "local" to this project when no namespace filter is configured, or its
        /// generated namespace is listed in the filter. Only local models are generated here.
        /// </summary>
        public bool IsLocal(ModelInfo info)
        {
            if (!HasFilter)
                return true;

            var ns = GetModelNamespace(info);
            return _generateOnlyNamespaces.Contains(ns, StringComparer.Ordinal);
        }

        /// <summary>
        /// The model type can be named in generated code here: either it is generated in this
        /// compilation or a referenced project already generated it (metadata-lib mode).
        /// </summary>
        public bool IsResolvable(ModelInfo info, Compilation compilation)
        {
            if (!HasFilter || IsLocal(info))
                return true;

            return compilation.GetTypeByMetadataName($"{GetModelNamespace(info)}.{info.Target}") is not null;
        }

        /// <summary>
        /// Mappings (extension methods, mappers, projections, DI) are emitted in the project
        /// that owns the destination type, and only when the source type can be named here.
        /// This keeps mapping direction "inward": Domain never emits mappings toward
        /// Application types it cannot reference.
        /// </summary>
        public bool CanEmitMapping(ModelInfo src, ModelInfo dst, Compilation compilation)
        {
            return IsLocal(dst) && IsResolvable(src, compilation);
        }

        /// <summary>
        /// Cast operators must be declared inside the source type, so only a locally-generated
        /// source can carry them; the destination must be nameable here as well.
        /// </summary>
        public bool CanEmitCast(ModelInfo src, ModelInfo dst, Compilation compilation)
        {
            return IsLocal(src) && IsResolvable(dst, compilation);
        }
    }
}
