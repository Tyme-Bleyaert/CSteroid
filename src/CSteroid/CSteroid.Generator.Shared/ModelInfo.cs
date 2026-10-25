using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Shared
{
    public class ModelInfo
    {
        public INamedTypeSymbol InterfaceSymbol { get; set; }
        public string Target { get; set; }
        public int ValidationType { get; set; }
        public int MappingTypes { get; set; }
        public int DeclarationType { get; set; }
        public int OrmTarget { get; set; }
        public bool IsSimpleMapping { get; set; }
        public bool ForwardPropertyAttributes { get; set; }
        public string PartitionKey { get; set; }
        public string CustomNamespace { get; set; }
        public string FolderPath { get; set; }
        public List<IPropertySymbol> Properties { get; set; }
    }
}
