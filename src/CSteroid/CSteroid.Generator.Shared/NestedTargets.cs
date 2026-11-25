using System;
using System.Collections.Generic;
using System.Linq;

namespace CSteroid.Generator.Shared
{
    public static class NestedTargets
    {
        public static bool TryGetNestedTarget(TypeSpec type, HashSet<string> targetNames, out string targetName)
        {
            targetName = type.Name;
            return targetNames.Contains(targetName);
        }

        public static bool TryGetCollectionElementTarget(TypeSpec type, HashSet<string> targetNames, out string elementTarget, out string collectionKind)
        {
            elementTarget = type.CollectionElementName;
            collectionKind = type.CollectionKind;
            return elementTarget is not null && targetNames.Contains(elementTarget);
        }

        public static Dictionary<string, HashSet<string>> BuildNestedTargetGraph(IEnumerable<IEnumerable<ModelInfo>> byInterface, HashSet<string> targetNames)
        {
            var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var group in byInterface)
            {
                foreach (var info in group)
                {
                    if (!graph.ContainsKey(info.Target))
                        graph[info.Target] = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var prop in info.Properties)
                    {
                        if (TryGetNestedTarget(prop.Type, targetNames, out var nestedTarget))
                            graph[info.Target].Add(nestedTarget);
                        else if (TryGetCollectionElementTarget(prop.Type, targetNames, out var elementTarget, out _))
                            graph[info.Target].Add(elementTarget);
                    }
                }
            }

            return graph;
        }

        public static bool IsInCycle(string source, string target, Dictionary<string, HashSet<string>> graph)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            stack.Push(target);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current == source)
                    return true;

                if (!visited.Add(current))
                    continue;

                if (graph.TryGetValue(current, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                        stack.Push(neighbor);
                }
            }

            return false;
        }

        public static List<PropertySpec> GetCommonProperties(ModelInfo src, ModelInfo dst)
        {
            var dstNames = new HashSet<string>(dst.Properties.Select(p => p.Name), StringComparer.Ordinal);
            return src.Properties.Where(p => dstNames.Contains(p.Name)).ToList();
        }
    }
}
