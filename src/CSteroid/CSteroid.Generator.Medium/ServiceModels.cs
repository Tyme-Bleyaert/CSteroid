namespace CSteroid.Generator.Medium
{
    internal enum LifetimeModel
    {
        Scoped = 0,
        Singleton = 1,
        Transient = 2,
    }

    internal enum DeclaredAccessibilityModel
    {
        Public,
        Internal,
        Protected,
        ProtectedOrInternal,
        ProtectedAndInternal,
        Private,
    }

    internal enum TypeContainerKind
    {
        Class,
        Struct,
        RecordClass,
        RecordStruct,
        Interface,
    }

    internal enum ParameterRefKind
    {
        None,
        Ref,
        Out,
        In,
        Other,
    }

    internal sealed record TypeDeclarationFact(
        TypeContainerKind ContainerKind,
        bool IsGeneric,
        bool IsStatic,
        bool IsAbstract,
        bool IsFileLocal,
        DeclaredAccessibilityModel Accessibility,
        LocationInfo Location);

    internal sealed record ConstructorParameterFact(
        TypeRef Type,
        ParameterRefKind RefKind,
        bool IsByRefLike,
        bool IsAccessible);

    internal sealed record ConstructorFact(
        DeclaredAccessibilityModel Accessibility,
        EquatableArray<ConstructorParameterFact> Parameters,
        LocationInfo Location);

    internal sealed record ServiceShapeFact(
        EquatableArray<TypeDeclarationFact> TypeChain,
        EquatableArray<ConstructorFact> Constructors);
}
