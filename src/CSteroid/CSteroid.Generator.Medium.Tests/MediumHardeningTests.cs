using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit.Abstractions;
using static CSteroid.Generator.Medium.Tests.MediumExecution;

namespace CSteroid.Generator.Medium.Tests;

public sealed class MediumHardeningTests
{
    private const int ProducerCount = 4;
    private const int HandlersPerProducer = 80;
    private const int HandlerCount = ProducerCount * HandlersPerProducer;

    private readonly ITestOutputHelper _output;

    public MediumHardeningTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void RetainedDriver_UnrelatedAndSendOnlyEdits_ReuseProjectionAndEveryPlan()
    {
        var producer = MediumTestCompilation.Create(
            "Hardening.Producer",
            [],
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class ImportedCommand : ICommand { }
            public sealed class ImportedHandler : ICommandHandler<ImportedCommand>
            {
                public ValueTask Handle(ImportedCommand command, CancellationToken cancellationToken) => default;
            }
            """);
        var producerOutput = MediumExecution.RunGenerator(producer, out var producerDiagnostics);
        Assert.Empty(producerDiagnostics);
        var producerManifest = Assert.Single(MediumExecution.GeneratedSources(producerOutput));
        Assert.Contains("typeof(global::ImportedHandler)", producerManifest);

        var compilation = MediumTestCompilation.Create(
            "Hardening.Root",
            [MediumExecution.EmitMetadataReference(producerOutput)],
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(LocalBehavior))]
            public sealed class LocalCommand : ICommand<int> { }
            public sealed class LocalHandler : ICommandHandler<LocalCommand, int>
            {
                public ValueTask<int> Handle(LocalCommand command, CancellationToken cancellationToken) => new(42);
            }
            public sealed class LocalBehavior : IPipelineBehavior<LocalCommand, int>
            {
                public ValueTask<int> Handle(
                    LocalCommand message,
                    MediumNext<LocalCommand, int> next,
                    CancellationToken cancellationToken) => next(message, cancellationToken);
            }
            """,
            """
            using CSteroid.Medium;
            public sealed class Consumer
            {
                public void Call(ISender sender)
                {
                    _ = sender.Send(new LocalCommand());
                    _ = sender.Send(new ImportedCommand());
                }
            }
            """);

        var driver = Run(MediumExecution.CreateDriver(trackIncrementalSteps: true), compilation, out var output);
        var expected = Snapshots(driver);
        Assert.Equal(3, expected.Length);
        Assert.All(expected, snapshot => Assert.Equal(65001, snapshot.CodePage));
        MediumTrackedSteps.AssertInitial(driver);

        var generated = string.Join("\n", MediumExecution.GeneratedSources(output));
        Assert.Contains("global::ImportedHandler", generated);
        Assert.Contains("global::LocalHandler", generated);
        Assert.Contains("global::LocalBehavior", generated);
        var consumerTree = compilation.SyntaxTrees.Single(tree => tree.FilePath == "1.cs");

        var unrelatedEdit = CSharpSyntaxTree.ParseText(
            """
            using CSteroid.Medium;
            public sealed class Consumer
            {
                public int UnrelatedProperty { get; set; }
                public void Call(ISender sender)
                {
                    _ = sender.Send(new LocalCommand());
                    _ = sender.Send(new ImportedCommand());
                }
            }
            """,
            path: consumerTree.FilePath);
        var unrelatedCompilation = compilation.ReplaceSyntaxTree(consumerTree, unrelatedEdit);
        driver = Run(driver, unrelatedCompilation, out _);
        AssertBytesEqual(expected, Snapshots(driver));
        MediumTrackedSteps.AssertRetained(driver);

        var sendOnlyEdit = CSharpSyntaxTree.ParseText(
            """
            using CSteroid.Medium;
            public sealed class Consumer
            {
                public int UnrelatedProperty { get; set; }
                public void Call(ISender sender)
                {
                    _ = sender.Send(new LocalCommand());
                    _ = sender.Send(new ImportedCommand());
                    _ = sender.Send(new LocalCommand());
                }
            }
            """,
            path: consumerTree.FilePath);
        driver = Run(driver, unrelatedCompilation.ReplaceSyntaxTree(unrelatedEdit, sendOnlyEdit), out _);
        AssertBytesEqual(expected, Snapshots(driver));
        MediumTrackedSteps.AssertRetained(driver);
    }

    [Fact]
    public void LargeSyntheticMultiAssemblySolution_ProjectsEveryHandlerDeterministically()
    {
        var producerReferences = new List<MetadataReference>(ProducerCount);
        var discoveryElapsed = TimeSpan.Zero;
        for (var producer = 0; producer < ProducerCount; producer++)
        {
            var compilation = MediumTestCompilation.Create(
                $"Hardening.Synthetic.Producer{producer}",
                [],
                ProducerSource(producer));
            var start = Stopwatch.GetTimestamp();
            var output = MediumExecution.RunGenerator(compilation, out var diagnostics);
            var elapsed = Stopwatch.GetElapsedTime(start);
            discoveryElapsed += elapsed;

            Assert.Empty(diagnostics);
            var manifest = Assert.Single(MediumExecution.GeneratedSources(output));
            Assert.Equal(HandlersPerProducer, Count(manifest, "MediumManifest("));
            for (var handler = 0; handler < HandlersPerProducer; handler++)
            {
                var route = producer * HandlersPerProducer + handler;
                Assert.Contains($"typeof(global::Handler{route:D4})", manifest);
            }

            producerReferences.Add(MediumExecution.EmitMetadataReference(output));
            _output.WriteLine(FormattableString.Invariant(
                $"producer={producer} handlers={HandlersPerProducer} direct-discovery+manifest={elapsed.TotalMilliseconds:F2}ms"));
        }

        var rootCompilation = MediumTestCompilation.Create(
            "Hardening.Synthetic.Root",
            producerReferences,
            "using CSteroid.Medium; [assembly: MediumCompositionRoot]",
            """
            using CSteroid.Medium;
            public sealed class Consumer
            {
                public void Call(ISender sender)
                {
                    _ = sender.Send(new Command0000());
                    _ = sender.Send(new Command0319());
                }
            }
            """);

        var projectionStart = Stopwatch.GetTimestamp();
        var driver = Run(MediumExecution.CreateDriver(trackIncrementalSteps: true), rootCompilation, out var rootOutput);
        var projectionElapsed = Stopwatch.GetElapsedTime(projectionStart);
        AssertRootOutput(rootOutput);
        var expected = Snapshots(driver);

        var shuffledCompilation = CSharpCompilation.Create(
            rootCompilation.AssemblyName,
            rootCompilation.SyntaxTrees.Reverse(),
            rootCompilation.References,
            rootCompilation.Options);
        var repeatedStart = Stopwatch.GetTimestamp();
        var repeatedDriver = Run(MediumExecution.CreateDriver(), shuffledCompilation, out var repeatedOutput);
        var repeatedElapsed = Stopwatch.GetElapsedTime(repeatedStart);
        AssertRootOutput(repeatedOutput);
        AssertBytesEqual(expected, Snapshots(repeatedDriver));

        var consumerTree = rootCompilation.SyntaxTrees.Single(tree => tree.FilePath == "1.cs");
        var unrelatedEdit = CSharpSyntaxTree.ParseText(
            """
            using CSteroid.Medium;
            public sealed class Consumer
            {
                public int UnrelatedProperty { get; set; }
                public void Call(ISender sender)
                {
                    _ = sender.Send(new Command0000());
                    _ = sender.Send(new Command0319());
                }
            }
            """,
            path: consumerTree.FilePath);
        driver = Run(driver, rootCompilation.ReplaceSyntaxTree(consumerTree, unrelatedEdit), out _);
        AssertBytesEqual(expected, Snapshots(driver));
        MediumTrackedSteps.AssertRetained(driver);

        _output.WriteLine(FormattableString.Invariant(
            $"root imported-handlers={HandlerCount} manifest-projection+generation={projectionElapsed.TotalMilliseconds:F2}ms"));
        _output.WriteLine(FormattableString.Invariant(
            $"root repeated-shuffled-generation={repeatedElapsed.TotalMilliseconds:F2}ms"));
        _output.WriteLine(FormattableString.Invariant(
            $"total direct-discovery+manifest={discoveryElapsed.TotalMilliseconds:F2}ms; no timing threshold asserted"));
    }

    private static GeneratorDriver Run(GeneratorDriver driver, Compilation compilation, out Compilation output)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out var diagnostics);
        Assert.Empty(diagnostics);
        MediumExecution.AssertCompiles(output);
        return driver;
    }

    private static Snapshot[] Snapshots(GeneratorDriver driver) =>
        Assert.Single(driver.GetRunResult().Results).GeneratedSources
            .Select(source => new Snapshot(
                source.HintName,
                source.SourceText.Encoding!.CodePage,
                source.SourceText.Encoding.GetBytes(source.SourceText.ToString())))
            .ToArray();

    private static void AssertBytesEqual(Snapshot[] expected, Snapshot[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].HintName, actual[index].HintName);
            Assert.Equal(expected[index].CodePage, actual[index].CodePage);
            Assert.Equal(expected[index].Bytes, actual[index].Bytes);
        }
    }

    private static void AssertRootOutput(Compilation output)
    {
        var sources = MediumExecution.GeneratedSources(output);
        Assert.Equal(2, sources.Length);
        var mediator = Assert.Single(sources, source => source.Contains("class GeneratedMedium"));
        var registration = Assert.Single(sources, source => source.Contains("AddMedium"));
        Assert.Equal(HandlerCount, Count(mediator, "{ typeof(global::Command"));
        Assert.Equal(HandlerCount, Count(registration, "TryAddScoped<global::Handler"));
        for (var route = 0; route < HandlerCount; route++)
        {
            Assert.Contains($"{{ typeof(global::Command{route:D4}),", mediator);
            Assert.Contains($"TryAddScoped<global::Handler{route:D4}>(services)", registration);
        }
    }

    private static string ProducerSource(int producer)
    {
        var source = new StringBuilder(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            """);
        for (var handler = 0; handler < HandlersPerProducer; handler++)
        {
            var route = producer * HandlersPerProducer + handler;
            source.AppendLine($"public sealed class Command{route:D4} : ICommand<int> {{ }}");
            source.AppendLine($"public sealed class Handler{route:D4} : ICommandHandler<Command{route:D4}, int>");
            source.AppendLine("{");
            source.AppendLine($"    public ValueTask<int> Handle(Command{route:D4} command, CancellationToken cancellationToken) => new({route});");
            source.AppendLine("}");
        }
        return source.ToString();
    }

    private sealed record Snapshot(string HintName, int CodePage, byte[] Bytes);
}
