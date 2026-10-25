using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Shared.AttributeFormatter;
using static CSteroid.Generator.Shared.GeneratorConstants;

namespace CSteroid.Generator.Shared
{
    public static class OutputConventions
    {
        private static AttributeData GetGeneratorConfigAttribute(IAssemblySymbol assembly)
        {
            return assembly.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "CSteroid.GeneratorConfigAttribute");
        }

        public static int GetFileNamingConvention(IAssemblySymbol assembly)
        {
            var attr = GetGeneratorConfigAttribute(assembly);
            if (attr is null)
                return 0;

            var named = attr.NamedArguments.FirstOrDefault(n => n.Key == "FileNamingConvention");
            if (named.Key == "FileNamingConvention" && named.Value.Value != null)
                return Convert.ToInt32(named.Value.Value);

            return 0;
        }

        public static (string ValidationNamespace, string MappingNamespace) GetOutputNamespaces(IAssemblySymbol assembly)
        {
            var attr = GetGeneratorConfigAttribute(assembly);
            if (attr is null)
                return (DefaultValidationNamespace, DefaultMappingsNamespace);

            var validationProject = GetAttributeNamedArgument(attr, "ValidationProject") as string;
            var mappingProject = GetAttributeNamedArgument(attr, "MappingProject") as string;

            return (
                string.IsNullOrWhiteSpace(validationProject) ? DefaultValidationNamespace : validationProject,
                string.IsNullOrWhiteSpace(mappingProject) ? DefaultMappingsNamespace : mappingProject);
        }

        /// <summary>
        /// Parses <c>GenerateOnlyNamespaces</c> from the consuming assembly's
        /// [assembly: GeneratorConfig(...)] attribute. Null/empty means every model is local
        /// (pre-metadata-lib behavior).
        /// </summary>
        public static string[] GetGenerateOnlyNamespaces(IAssemblySymbol assembly)
        {
            var attr = GetGeneratorConfigAttribute(assembly);
            if (attr is null)
                return null;

            foreach (var kvp in attr.NamedArguments)
            {
                if (kvp.Key != "GenerateOnlyNamespaces" || kvp.Value.IsNull)
                    continue;

                var values = kvp.Value.Values
                    .Select(v => v.Value as string)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                return values.Length == 0 ? null : values;
            }

            return null;
        }

        public static string GetFileName(string baseName, int convention)
        {
            var suffix = convention == FileNamingConventionModelSuffix ? ".model.cs" : ".g.cs";
            return $"{baseName}{suffix}";
        }

        public static string GetModelNamespace(ModelInfo info)
        {
            return info.CustomNamespace ?? ModelsNamespace;
        }

        public static string GetValidationNamespace(ModelInfo info, string defaultNamespace)
        {
            return info.CustomNamespace ?? defaultNamespace;
        }

        public static string GetHintPath(ModelInfo info, string fileName)
        {
            var folderPath = info.FolderPath;
            if (string.IsNullOrEmpty(folderPath) && !string.IsNullOrEmpty(info.CustomNamespace))
            {
                folderPath = info.CustomNamespace.Replace('.', '/');
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                folderPath = folderPath.Replace('\\', '/').Trim('/');
                return $"{folderPath}/{fileName}";
            }

            return fileName;
        }

        public static string GetOutputHintPath(string outputNamespace, string defaultNamespace, string fileName)
        {
            if (outputNamespace == defaultNamespace)
                return fileName;

            var folderPath = outputNamespace.Replace('.', '/').Trim('/');
            return $"{folderPath}/{fileName}";
        }
    }
}
