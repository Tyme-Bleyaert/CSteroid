namespace CSteroid.Generator.Shared
{
    public static class GeneratorConstants
    {
        public const string ModelsNamespace = "CSteroid.Generated.Models";
        public const string DefaultMappingsNamespace = "CSteroid.Generated.Mappings";
        public const string DefaultValidationNamespace = "CSteroid.Generated.Validation";

        public const int ValidationTypeDataAnnotations = 1;
        public const int ValidationTypeCustom = 2;
        public const int ValidationTypeAsync = 4;
        public const int MappingTypesExtensionMethod = 1;
        public const int MappingTypesStatic = 2;
        public const int MappingTypesImplicitCast = 4;
        public const int MappingTypesExplicitCast = 8;
        public const int MappingTypesDependencyInjection = 16;
        public const int MappingTypesProjectionExpression = 32;
        public const int DeclarationTypeRecord = 1;
        public const int DeclarationTypeStruct = 2;
        public const int OrmTargetEFCore = 1;
        public const int OrmTargetDapper = 2;
        public const int OrmTargetCosmosDB = 4;
        public const int OrmTargetMongoDB = 8;
        public const int OrmTargetPostgreSQL = 16;
        public const int FileNamingConventionModelSuffix = 1;
    }
}
