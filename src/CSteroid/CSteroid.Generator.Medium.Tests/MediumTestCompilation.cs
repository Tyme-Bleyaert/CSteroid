using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSteroid.Generator.Medium.Tests;

internal static class MediumTestCompilation
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> CommonReferences = new(CreateCommonReferences);

    internal static CSharpCompilation Create(params string[] sources) =>
        Create("MediumTests", Array.Empty<MetadataReference>(), sources);

    internal static CSharpCompilation Create(
        string assemblyName,
        IEnumerable<MetadataReference> additionalReferences,
        params string[] sources)
    {
        var references = CommonReferences.Value.Concat(additionalReferences);
        return CSharpCompilation.Create(
            assemblyName,
            sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: index + ".cs")),
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));
    }

    private static IReadOnlyList<MetadataReference> CreateCommonReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Concat(
            [
                typeof(CSteroid.Medium.IMessage).Assembly.Location,
                typeof(Microsoft.Extensions.DependencyInjection.ServiceLifetime).Assembly.Location,
                typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly.Location,
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    }
}
