using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Medium.Tests.MediumExecution;

namespace CSteroid.Generator.Medium.Tests;

public class MediumCrossAssemblyTests
{
    [Fact]
    public async Task RootImportsExplicitProducerHandlers_WithoutReExportingManifestOrProducerBehaviors()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            [assembly: MediumBehavior(typeof(ProducerBehavior))]

            public sealed class ImportedCommand : ICommand<int> { }
            public sealed class ImportedNotice : IMessage { }
            public static class ProducerState { public static int Calls; }
            public sealed class ImportedHandler : ICommandHandler<ImportedCommand, int>, IMessageHandler<ImportedNotice>
            {
                ValueTask<int> ICommandHandler<ImportedCommand, int>.Handle(
                    ImportedCommand command,
                    CancellationToken cancellationToken) => new(7);
                ValueTask IMessageHandler<ImportedNotice>.Handle(ImportedNotice message, CancellationToken cancellationToken)
                {
                    ProducerState.Calls++;
                    return default;
                }
            }
            public sealed class ProducerBehavior : IPipelineBehavior<ImportedCommand, int>
            {
                public ValueTask<int> Handle(
                    ImportedCommand message,
                    MediumNext<ImportedCommand, int> next,
                    CancellationToken cancellationToken) => throw new System.InvalidOperationException("Must not be imported.");
            }
            """);
        var producerGenerated = string.Join("\n", MediumExecution.GeneratedSources(producer));
        Assert.Equal(1, Count(producerGenerated, "MediumManifest("));
        Assert.DoesNotContain("class GeneratedMedium", producerGenerated);
        Assert.DoesNotContain("AddMedium", producerGenerated);

        var producerImage = MediumExecution.EmitImage(producer);
        var producerReference = MetadataReference.CreateFromImage(producerImage);
        var root = Generate("Sample.WebApi", [producerReference], """
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMedium>();
                    var response = await mediator.Send(new ImportedCommand());
                    await mediator.Publish(new ImportedNotice());
                    return response + ":" + ProducerState.Calls;
                }
            }
            """);

        var rootGenerated = string.Join("\n", MediumExecution.GeneratedSources(root));
        Assert.Contains("global::ImportedHandler", rootGenerated);
        Assert.DoesNotContain("MediumManifest(", rootGenerated);
        Assert.DoesNotContain("ProducerBehavior", rootGenerated);
        Assert.Equal("7:1", await MediumExecution.RunProgramAsync<string>(
            MediumExecution.EmitImage(root),
            ("Sample.Application", producerImage)));
    }

    [Fact]
    public async Task RootPreservesExplicitOrderForImportedMessageHandlers()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            public sealed class ImportedNotice : IMessage { }
            public static class ProducerState { public static string Log = string.Empty; }
            [MessageHandler(Order = -10)]
            public sealed class ZuluImportedHandler : IMessageHandler<ImportedNotice>
            {
                public ValueTask Handle(ImportedNotice message, CancellationToken cancellationToken)
                {
                    ProducerState.Log += "I";
                    return default;
                }
            }
            """);
        var producerImage = MediumExecution.EmitImage(producer);
        var root = Generate(
            "Sample.WebApi",
            [MetadataReference.CreateFromImage(producerImage)],
            """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class AlphaLocalHandler : IMessageHandler<ImportedNotice>
            {
                public ValueTask Handle(ImportedNotice message, CancellationToken cancellationToken)
                {
                    ProducerState.Log += "L";
                    return default;
                }
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new ImportedNotice());
                    return ProducerState.Log;
                }
            }
            """);

        Assert.Equal("IL", await MediumExecution.RunProgramAsync<string>(
            MediumExecution.EmitImage(root),
            ("Sample.Application", producerImage)));
    }

    [Fact]
    public async Task DuplicateImportedAndLocalCommand_Reportscsm002AndOmitsOnlyThatRoute()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class DuplicateCommand : ICommand { }
            public sealed class ImportedHandler : ICommandHandler<DuplicateCommand>
            {
                public ValueTask Handle(DuplicateCommand command, CancellationToken cancellationToken) => default;
            }
            """);
        var rootCompilation = MediumTestCompilation.Create(
            "Sample.WebApi",
            [MediumExecution.EmitMetadataReference(producer)],
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class LocalHandler : ICommandHandler<DuplicateCommand>
            {
                public ValueTask Handle(DuplicateCommand command, CancellationToken cancellationToken) => default;
            }
            public sealed class ValidCommand : ICommand<int> { }
            public sealed class ValidHandler : ICommandHandler<ValidCommand, int>
            {
                public ValueTask<int> Handle(ValidCommand command, CancellationToken cancellationToken) => new(1);
            }
            """);

        var diagnostic = Assert.Single(await Analyze(rootCompilation));
        Assert.Equal("CSM002", diagnostic.Id);
        var output = MediumExecution.RunGenerator(rootCompilation, out var generatorDiagnostics);
        Assert.Empty(generatorDiagnostics);
        var mediator = Assert.Single(
            MediumExecution.GeneratedSources(output),
            source => source.Contains("class GeneratedMedium"));
        Assert.DoesNotContain("typeof(global::DuplicateCommand)", mediator);
        Assert.Contains("typeof(global::ValidCommand)", mediator);
    }

    [Fact]
    public async Task InaccessibleImportedHandler_Reportscsm006AndIsNotEmitted()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class ImportedCommand : ICommand { }
            internal sealed class ImportedHandler : ICommandHandler<ImportedCommand>
            {
                public ValueTask Handle(ImportedCommand command, CancellationToken cancellationToken) => default;
            }
            """);
        var rootCompilation = MediumTestCompilation.Create(
            "Sample.WebApi",
            [MediumExecution.EmitMetadataReference(producer)],
            "using CSteroid.Medium; [assembly: MediumCompositionRoot]");

        var diagnostic = Assert.Single(await Analyze(rootCompilation));
        Assert.Equal("CSM006", diagnostic.Id);
        var output = MediumExecution.RunGenerator(rootCompilation, out var generatorDiagnostics);
        Assert.Empty(generatorDiagnostics);
        var mediator = Assert.Single(
            MediumExecution.GeneratedSources(output),
            source => source.Contains("class GeneratedMedium"));
        Assert.DoesNotContain("ImportedHandler", mediator);
        Assert.DoesNotContain("ImportedCommand", mediator);
    }

    [Fact]
    public void TwoRoots_CanImportTheSameProducerWithIndependentInternalSurfaces()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """);
        var reference = MediumExecution.EmitMetadataReference(producer);
        const string rootSource = "using CSteroid.Medium; [assembly: MediumCompositionRoot]";

        var first = Generate("First.Root", [reference], rootSource);
        var second = Generate("Second.Root", [reference], rootSource);

        foreach (var root in new[] { first, second })
        {
            var generated = string.Join("\n", MediumExecution.GeneratedSources(root));
            Assert.Contains("internal sealed class GeneratedMedium", generated);
            Assert.Contains("AddMedium", generated);
            Assert.Contains("global::Handler", generated);
        }
    }

    private static Compilation Generate(
        string assemblyName,
        MetadataReference[] references,
        string source)
    {
        var output = MediumExecution.RunGenerator(
            MediumTestCompilation.Create(assemblyName, references, source),
            out var diagnostics);
        Assert.Empty(diagnostics);
        return output;
    }

}
