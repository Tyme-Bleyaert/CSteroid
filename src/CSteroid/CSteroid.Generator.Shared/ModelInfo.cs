using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSteroid.Generator.Shared
{
    public sealed record LocationSpec
    {
        public LocationSpec(string filePath, int start, int length, int startLine, int startCharacter, int endLine, int endCharacter)
        {
            FilePath = filePath;
            Start = start;
            Length = length;
            StartLine = startLine;
            StartCharacter = startCharacter;
            EndLine = endLine;
            EndCharacter = endCharacter;
        }

        public string FilePath { get; }
        public int Start { get; }
        public int Length { get; }
        public int StartLine { get; }
        public int StartCharacter { get; }
        public int EndLine { get; }
        public int EndCharacter { get; }

        public static LocationSpec From(Location location)
        {
            if (location is null || !location.IsInSource)
                return null;

            var span = location.SourceSpan;
            var lineSpan = location.GetLineSpan();
            return new LocationSpec(
                lineSpan.Path,
                span.Start,
                span.Length,
                lineSpan.StartLinePosition.Line,
                lineSpan.StartLinePosition.Character,
                lineSpan.EndLinePosition.Line,
                lineSpan.EndLinePosition.Character);
        }

        public Location ToLocation()
        {
            return Location.Create(
                FilePath,
                new TextSpan(Start, Length),
                new LinePositionSpan(
                    new LinePosition(StartLine, StartCharacter),
                    new LinePosition(EndLine, EndCharacter)));
        }
    }

    public sealed record TypeSpec
    {
        public TypeSpec(string name, string globalName, string collectionElementName, string collectionKind)
        {
            Name = name;
            GlobalName = globalName;
            CollectionElementName = collectionElementName;
            CollectionKind = collectionKind;
        }

        public string Name { get; }
        public string GlobalName { get; }
        public string CollectionElementName { get; }
        public string CollectionKind { get; }
    }

    public sealed record AttributeArgumentSpec
    {
        public AttributeArgumentSpec(string literal, string stringValue)
        {
            Literal = literal;
            StringValue = stringValue;
        }

        public string Literal { get; }
        public string StringValue { get; }
    }

    public sealed record NamedAttributeArgumentSpec
    {
        public NamedAttributeArgumentSpec(string name, AttributeArgumentSpec value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public AttributeArgumentSpec Value { get; }
    }

    public sealed record AttributeSpec
    {
        public AttributeSpec(
            string displayName,
            string attributeNamespace,
            string name,
            bool isImplicitlyDeclared,
            string minimallyQualifiedText,
            string fullyQualifiedText,
            EquatableArray<AttributeArgumentSpec> constructorArguments,
            EquatableArray<NamedAttributeArgumentSpec> namedArguments)
        {
            DisplayName = displayName;
            Namespace = attributeNamespace;
            Name = name;
            IsImplicitlyDeclared = isImplicitlyDeclared;
            MinimallyQualifiedText = minimallyQualifiedText;
            FullyQualifiedText = fullyQualifiedText;
            ConstructorArguments = constructorArguments;
            NamedArguments = namedArguments;
        }

        public string DisplayName { get; }
        public string Namespace { get; }
        public string Name { get; }
        public bool IsImplicitlyDeclared { get; }
        public string MinimallyQualifiedText { get; }
        public string FullyQualifiedText { get; }
        public EquatableArray<AttributeArgumentSpec> ConstructorArguments { get; }
        public EquatableArray<NamedAttributeArgumentSpec> NamedArguments { get; }
    }

    public sealed record PropertySpec
    {
        public PropertySpec(string name, TypeSpec type, string documentationXml, EquatableArray<AttributeSpec> attributes)
        {
            Name = name;
            Type = type;
            DocumentationXml = documentationXml;
            Attributes = attributes;
        }

        public string Name { get; }
        public TypeSpec Type { get; }
        public string DocumentationXml { get; }
        public EquatableArray<AttributeSpec> Attributes { get; }
    }

    public sealed record ModelInfo
    {
        public ModelInfo(
            string interfaceKey,
            LocationSpec interfaceLocation,
            string target,
            int validationType,
            int mappingTypes,
            int declarationType,
            int ormTarget,
            bool isSimpleMapping,
            bool forwardPropertyAttributes,
            string partitionKey,
            string customNamespace,
            string folderPath,
            EquatableArray<PropertySpec> properties,
            bool isResolvableFromReferences = false,
            int declarationCount = 1)
        {
            InterfaceKey = interfaceKey;
            InterfaceLocation = interfaceLocation;
            Target = target;
            ValidationType = validationType;
            MappingTypes = mappingTypes;
            DeclarationType = declarationType;
            OrmTarget = ormTarget;
            IsSimpleMapping = isSimpleMapping;
            ForwardPropertyAttributes = forwardPropertyAttributes;
            PartitionKey = partitionKey;
            CustomNamespace = customNamespace;
            FolderPath = folderPath;
            Properties = properties;
            IsResolvableFromReferences = isResolvableFromReferences;
            DeclarationCount = declarationCount;
        }

        public string InterfaceKey { get; }
        public LocationSpec InterfaceLocation { get; }
        public string Target { get; }
        public int ValidationType { get; }
        public int MappingTypes { get; }
        public int DeclarationType { get; }
        public int OrmTarget { get; }
        public bool IsSimpleMapping { get; }
        public bool ForwardPropertyAttributes { get; }
        public string PartitionKey { get; }
        public string CustomNamespace { get; }
        public string FolderPath { get; }
        public EquatableArray<PropertySpec> Properties { get; }
        public bool IsResolvableFromReferences { get; }
        public int DeclarationCount { get; }

        public ModelInfo WithResolution(bool isResolvable)
        {
            return new ModelInfo(
                InterfaceKey,
                InterfaceLocation,
                Target,
                ValidationType,
                MappingTypes,
                DeclarationType,
                OrmTarget,
                IsSimpleMapping,
                ForwardPropertyAttributes,
                PartitionKey,
                CustomNamespace,
                FolderPath,
                Properties,
                isResolvable,
                DeclarationCount);
        }
    }

    public sealed record GeneratorConfigSpec
    {
        public GeneratorConfigSpec(
            int fileNamingConvention,
            string validationNamespace,
            string mappingNamespace,
            EquatableArray<string> generateOnlyNamespaces)
        {
            FileNamingConvention = fileNamingConvention;
            ValidationNamespace = validationNamespace;
            MappingNamespace = mappingNamespace;
            GenerateOnlyNamespaces = generateOnlyNamespaces;
        }

        public int FileNamingConvention { get; }
        public string ValidationNamespace { get; }
        public string MappingNamespace { get; }
        public EquatableArray<string> GenerateOnlyNamespaces { get; }
    }

    public sealed record GenerationSpec
    {
        public GenerationSpec(
            EquatableArray<ModelInfo> models,
            EquatableArray<ModelInfo> mergedModels,
            GeneratorConfigSpec config,
            bool foundReferencedInterfaces)
        {
            Models = models;
            MergedModels = mergedModels;
            Config = config;
            FoundReferencedInterfaces = foundReferencedInterfaces;
        }

        public EquatableArray<ModelInfo> Models { get; }
        public EquatableArray<ModelInfo> MergedModels { get; }
        public GeneratorConfigSpec Config { get; }
        public bool FoundReferencedInterfaces { get; }
    }

    internal sealed record CompilationSpec
    {
        public CompilationSpec(
            EquatableArray<InterfaceSpec> referencedInterfaces,
            EquatableArray<string> resolvableModelTypes,
            GeneratorConfigSpec config)
        {
            ReferencedInterfaces = referencedInterfaces;
            ResolvableModelTypes = resolvableModelTypes;
            Config = config;
        }

        public EquatableArray<InterfaceSpec> ReferencedInterfaces { get; }
        public EquatableArray<string> ResolvableModelTypes { get; }
        public GeneratorConfigSpec Config { get; }
    }

    internal sealed record InterfaceSpec
    {
        public InterfaceSpec(string key, EquatableArray<ModelInfo> models)
        {
            Key = key;
            Models = models;
        }

        public string Key { get; }
        public EquatableArray<ModelInfo> Models { get; }
    }
}
