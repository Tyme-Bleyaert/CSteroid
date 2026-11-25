using System;
using System.Collections.Generic;
using System.Linq;
using static CSteroid.Generator.Shared.OutputConventions;

namespace CSteroid.Generator.Shared
{
    /// <summary>
    /// Decides which models and mappings a project emits in metadata-lib mode.
    /// </summary>
    public sealed class EmissionPolicy
    {
        private readonly string[] _generateOnlyNamespaces;

        public EmissionPolicy(IEnumerable<string> generateOnlyNamespaces)
        {
            _generateOnlyNamespaces = generateOnlyNamespaces?.ToArray() ?? Array.Empty<string>();
        }

        private bool HasFilter => _generateOnlyNamespaces.Length > 0;

        public bool IsLocal(ModelInfo info)
        {
            if (!HasFilter)
                return true;

            var ns = GetModelNamespace(info);
            return _generateOnlyNamespaces.Contains(ns, StringComparer.Ordinal);
        }

        public bool IsResolvable(ModelInfo info)
        {
            return !HasFilter || IsLocal(info) || info.IsResolvableFromReferences;
        }

        public bool CanEmitMapping(ModelInfo src, ModelInfo dst)
        {
            return IsLocal(dst) && IsResolvable(src);
        }

        public bool CanEmitCast(ModelInfo src, ModelInfo dst)
        {
            return IsLocal(src) && IsResolvable(dst);
        }
    }
}
