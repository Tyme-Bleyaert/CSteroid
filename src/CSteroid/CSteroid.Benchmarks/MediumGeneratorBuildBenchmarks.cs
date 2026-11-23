using System.Text;
using BenchmarkDotNet.Attributes;
using CSteroid.Medium;
using CSteroid.Generator.Medium;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace CSteroid.Benchmarks
{
    /// <summary>
    /// Measures fresh-driver generation from parsed compilations. Source parsing and producer
    /// emission are fixture setup; imported-manifest benchmarks measure root generation only.
    /// </summary>
    [MemoryDiagnoser]
    public class MediumGeneratorBuildBenchmarks
    {
        private const int ProducerCount = 4;
        private static readonly MetadataReference[] s_commonReferences = CreateCommonReferences();

        private CSharpCompilation _sourceCompilation;
        private CSharpCompilation _projectionCompilation;

        [Params(8, 256)]
        public int HandlerCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _sourceCompilation = CreateCompilation(
                "Benchmark.Source",
                [],
                CreateHandlers(0, HandlerCount, compositionRoot: true));
            _projectionCompilation = CreateProjectionCompilation();

            Validate(Generate(_sourceCompilation, out var sourceOutput), HandlerCount, localHandlers: true);
            ValidateCompilation(sourceOutput);
            Validate(Generate(_projectionCompilation, out var projectionOutput), HandlerCount, localHandlers: false);
            ValidateCompilation(projectionOutput);
        }

        [Benchmark(Description = "Direct-interface discovery, local manifest, and root emission")]
        public GeneratorDriverRunResult SourceDiscoveryAndManifest() => Generate(_sourceCompilation, out _);

        [Benchmark(Description = "Imported manifest projection, merged planning, and root emission")]
        public GeneratorDriverRunResult ImportedManifestProjection() => Generate(_projectionCompilation, out _);

        internal static void CheckCorrectness()
        {
            foreach (var handlerCount in new[] { 8, 256 })
            {
                var benchmark = new MediumGeneratorBuildBenchmarks { HandlerCount = handlerCount };
                benchmark.Setup();
            }
        }

        private CSharpCompilation CreateProjectionCompilation()
        {
            var producerReferences = new List<MetadataReference>();
            var handlersPerProducer = HandlerCount / ProducerCount;
            for (var producer = 0; producer < ProducerCount; producer++)
            {
                var compilation = CreateCompilation(
                    $"Benchmark.Producer{producer}",
                    [],
                    CreateHandlers(producer * handlersPerProducer, handlersPerProducer, compositionRoot: false));
                var result = Generate(compilation, out var output);
                var generated = result.Results.Single().GeneratedSources.Single();
                var manifest = generated.SourceText.ToString();
                Require(Count(manifest, "MediumManifest(") == handlersPerProducer,
                    $"Producer {producer} manifest count");
                ValidateCompilation(output);
                producerReferences.Add(EmitMetadataReference(output));
            }

            return CreateCompilation(
                "Benchmark.ManifestRoot",
                producerReferences,
                "using CSteroid.Medium; [assembly: MediumCompositionRoot] public sealed class RootMarker { }");
        }

        private static GeneratorDriverRunResult Generate(CSharpCompilation compilation, out Compilation output)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new[] { new MediumGenerator().AsSourceGenerator() },
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, false));
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out var diagnostics);
            Require(diagnostics.IsEmpty, "Generator diagnostics: " + string.Join(Environment.NewLine, diagnostics));
            return driver.GetRunResult();
        }

        private static void Validate(
            GeneratorDriverRunResult result,
            int handlerCount,
            bool localHandlers)
        {
            var sources = result.Results.Single().GeneratedSources;
            Require(sources.Length == (localHandlers ? 3 : 2), "Generated source count");
            var mediator = sources.Single(source => source.HintName == "Medium.Mediator.g.cs")
                .SourceText.ToString();
            var registration = sources.Single(source => source.HintName == "Medium.Registration.g.cs")
                .SourceText.ToString();
            Require(Count(mediator, "{ typeof(global::Command") == handlerCount, "Generated route count");
            Require(Count(registration, "TryAddScoped<global::Handler") == handlerCount, "Generated registration count");

            if (localHandlers)
            {
                var manifest = sources.Single(source => source.HintName == "Medium.Manifest.g.cs")
                    .SourceText.ToString();
                Require(Count(manifest, "MediumManifest(") == handlerCount, "Local manifest count");
            }
            else
            {
                Require(sources.All(source => source.HintName != "Medium.Manifest.g.cs"),
                    "Imported handlers must not be re-exported");
            }

            for (var route = 0; route < handlerCount; route++)
            {
                Require(mediator.Contains($"{{ typeof(global::Command{route:D4}),", StringComparison.Ordinal),
                    $"Missing route {route}");
                Require(registration.Contains($"TryAddScoped<global::Handler{route:D4}>(services)", StringComparison.Ordinal),
                    $"Missing registration {route}");
            }
        }

        private static CSharpCompilation CreateCompilation(
            string assemblyName,
            IEnumerable<MetadataReference> additionalReferences,
            string source)
        {
            return CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source, path: "0.cs")],
                s_commonReferences.Concat(additionalReferences),
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable,
                    deterministic: true));
        }

        private static string CreateHandlers(int start, int count, bool compositionRoot)
        {
            var source = new StringBuilder(
                """
                using System.Threading;
                using System.Threading.Tasks;
                using CSteroid.Medium;

                """);
            if (compositionRoot)
                source.AppendLine("[assembly: MediumCompositionRoot]");
            for (var route = start; route < start + count; route++)
            {
                source.AppendLine($"public sealed class Command{route:D4} : ICommand<int> {{ }}");
                source.AppendLine($"public sealed class Handler{route:D4} : ICommandHandler<Command{route:D4}, int>");
                source.AppendLine("{");
                source.AppendLine($"    public ValueTask<int> Handle(Command{route:D4} command, CancellationToken cancellationToken) => new({route});");
                source.AppendLine("}");
            }
            return source.ToString();
        }

        private static MetadataReference[] CreateCommonReferences()
        {
            return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Concat(
                [
                    typeof(IMessage).Assembly.Location,
                    typeof(ServiceCollection).Assembly.Location,
                ])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToArray();
        }

        private static MetadataReference EmitMetadataReference(Compilation compilation)
        {
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Require(result.Success, "Producer emit: " + string.Join(Environment.NewLine, result.Diagnostics));
            return MetadataReference.CreateFromImage(stream.ToArray());
        }

        private static void ValidateCompilation(Compilation compilation)
        {
            var errors = compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
            Require(errors.Length == 0, "Generated compilation: " + string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        }

        private static int Count(string text, string value)
        {
            var count = 0;
            for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
                count++;
            return count;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Generator build benchmark correctness failed: " + message);
        }
    }
}
