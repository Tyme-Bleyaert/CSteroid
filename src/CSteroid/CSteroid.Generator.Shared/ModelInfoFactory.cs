using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Shared
{
    public static class ModelInfoFactory
    {
        public static ModelInfo BuildModelInfo(INamedTypeSymbol interfaceSymbol, AttributeData attr, List<IPropertySymbol> allProperties, INamedTypeSymbol excludeAttrSymbol, IReadOnlyList<AttributeData> ormAttributes)
        {
            string target = GetTarget(attr);
            if (string.IsNullOrWhiteSpace(target))
                return null;

            var excludedFromAttr = new HashSet<string>(GetExcludedProperties(attr), StringComparer.Ordinal);
            int validationType = GetValidationType(attr);
            int mappingTypes = GetMappingTypes(attr);
            int declarationType = GetDeclarationType(attr);
            int ormTarget = GetEffectiveOrmTarget(ormAttributes, target);
            bool isSimpleMapping = GetSimple(attr);
            bool forwardPropertyAttributes = GetForwardPropertyAttributes(attr);
            string partitionKey = GetPartitionKey(attr);
            string customNamespace = GetCustomNamespace(attr);
            string folderPath = GetFolderPath(attr);

            var props = new List<IPropertySymbol>();
            foreach (var p in allProperties)
            {
                bool skip = false;

                if (excludedFromAttr.Contains(p.Name))
                    skip = true;

                if (!skip)
                {
                    var exAttr = p.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, excludeAttrSymbol));
                    if (exAttr != null)
                    {
                        var excludedModels = GetExcludedModels(exAttr);
                        if (excludedModels.Contains(target))
                            skip = true;
                    }
                }

                if (!skip)
                    props.Add(p);
            }

            return new ModelInfo
            {
                InterfaceSymbol = interfaceSymbol,
                Target = target,
                ValidationType = validationType,
                MappingTypes = mappingTypes,
                DeclarationType = declarationType,
                OrmTarget = ormTarget,
                IsSimpleMapping = isSimpleMapping,
                ForwardPropertyAttributes = forwardPropertyAttributes,
                PartitionKey = partitionKey,
                CustomNamespace = customNamespace,
                FolderPath = folderPath,
                Properties = props
            };
        }

        public static IEnumerable<IPropertySymbol> GetAllProperties(INamedTypeSymbol symbol)
        {
            var dict = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);

            void Collect(INamedTypeSymbol s)
            {
                foreach (var member in s.GetMembers().OfType<IPropertySymbol>())
                {
                    if (!dict.ContainsKey(member.Name))
                        dict[member.Name] = member;
                }

                foreach (var iface in s.Interfaces)
                    Collect(iface);
            }

            Collect(symbol);
            return dict.Values;
        }

        public static string GetTarget(AttributeData attr)
        {
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string s)
                return s;
            return null;
        }

        public static int GetValidationType(AttributeData attr)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "ValidationType");
            if (named.Key == "ValidationType" && named.Value.Value != null)
                return Convert.ToInt32(named.Value.Value);

            if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value != null)
                return Convert.ToInt32(attr.ConstructorArguments[1].Value);

            return 0;
        }

        public static int GetMappingTypes(AttributeData attr)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "MappingTypes");
            if (named.Key == "MappingTypes" && named.Value.Value != null)
                return Convert.ToInt32(named.Value.Value);

            if (attr.ConstructorArguments.Length > 2 && attr.ConstructorArguments[2].Value != null)
                return Convert.ToInt32(attr.ConstructorArguments[2].Value);

            return 0;
        }

        public static int GetDeclarationType(AttributeData attr)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "DeclarationType");
            if (named.Key == "DeclarationType" && named.Value.Value != null)
                return Convert.ToInt32(named.Value.Value);

            if (attr.ConstructorArguments.Length > 3 && attr.ConstructorArguments[3].Value != null)
                return Convert.ToInt32(attr.ConstructorArguments[3].Value);

            return 0;
        }

        public static int GetEffectiveOrmTarget(IReadOnlyList<AttributeData> ormAttributes, string target)
        {
            int ormTarget = 0;
            foreach (var ormAttr in ormAttributes)
            {
                var modelArg = ormAttr.NamedArguments.FirstOrDefault(n => n.Key == "Model");
                var model = modelArg.Key == "Model" ? modelArg.Value.Value as string : null;
                if (model != null && model != target)
                    continue;

                if (ormAttr.ConstructorArguments.Length > 0 && ormAttr.ConstructorArguments[0].Value != null)
                    ormTarget |= Convert.ToInt32(ormAttr.ConstructorArguments[0].Value);
            }

            return ormTarget;
        }

        public static bool GetSimple(AttributeData attr)
        {
            return GetNamedBool(attr, "Simple");
        }

        public static bool GetForwardPropertyAttributes(AttributeData attr)
        {
            return GetNamedBool(attr, "ForwardPropertyAttributes");
        }

        private static bool GetNamedBool(AttributeData attr, string name)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == name);
            if (named.Key == name && named.Value.Value is bool b)
                return b;

            return false;
        }

        public static string GetPartitionKey(AttributeData attr)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "PartitionKey");
            if (named.Key == "PartitionKey" && named.Value.Value is string s)
                return s;

            if (attr.ConstructorArguments.Length > 6 && attr.ConstructorArguments[6].Value is string ps)
                return ps;

            return null;
        }

        public static string GetCustomNamespace(AttributeData attr)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "CustomNamespace");
            if (named.Key == "CustomNamespace" && named.Value.Value is string s)
                return s;
            return null;
        }

        public static string GetFolderPath(AttributeData attr)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "FolderPath");
            if (named.Key == "FolderPath" && named.Value.Value is string s)
                return s;
            return null;
        }

        public static string[] GetExcludedProperties(AttributeData attr)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "ExcludedProperties");
            if (named.Key == "ExcludedProperties" && named.Value.Kind == TypedConstantKind.Array && !named.Value.IsNull)
            {
                return named.Value.Values.Select(v => v.Value as string).Where(s => s != null).ToArray();
            }

            if (attr.ConstructorArguments.Length > 5 && attr.ConstructorArguments[5].Kind == TypedConstantKind.Array && !attr.ConstructorArguments[5].IsNull)
            {
                return attr.ConstructorArguments[5].Values.Select(v => v.Value as string).Where(s => s != null).ToArray();
            }

            return Array.Empty<string>();
        }

        public static string[] GetExcludedModels(AttributeData attr)
        {
            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "ExcludedModels");
            if (named.Key == "ExcludedModels" && named.Value.Kind == TypedConstantKind.Array && !named.Value.IsNull)
            {
                return named.Value.Values.Select(v => v.Value as string).Where(s => s != null).ToArray();
            }

            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Kind == TypedConstantKind.Array && !attr.ConstructorArguments[0].IsNull)
            {
                return attr.ConstructorArguments[0].Values.Select(v => v.Value as string).Where(s => s != null).ToArray();
            }

            return Array.Empty<string>();
        }
    }
}
