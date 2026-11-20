using Microsoft.CodeAnalysis;
using static CSteroid.Generator.AutoMediator.Tests.AutoMediatorExecution;

namespace CSteroid.Generator.AutoMediator.Tests;

public class AutoMediatorCrossAssemblyTests
{
    [Fact]
    public async Task RootImportsExplicitProducerHandlers_WithoutReExportingManifestOrProducerBehaviors()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorBehavior(typeof(ProducerBehavior))]

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
                    AutoMediatorNext<ImportedCommand, int> next,
                    CancellationToken cancellationToken) => throw new System.InvalidOperationException("Must not be imported.");
            }
            """);
        var producerGenerated = string.Join("\n", AutoMediatorExecution.GeneratedSources(producer));
        Assert.Equal(1, Count(producerGenerated, "AutoMediatorManifest("));
        Assert.DoesNotContain("class GeneratedAutoMediator", producerGenerated);
        Assert.DoesNotContain("AddAutoMediator", producerGenerated);

        var producerImage = AutoMediatorExecution.EmitImage(producer);
        var producerReference = MetadataReference.CreateFromImage(producerImage);
        var root = Generate("Sample.WebApi", [producerReference], """
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IAutoMediator>();
                    var response = await mediator.Send(new ImportedCommand());
                    await mediator.Publish(new ImportedNotice());
                    return response + ":" + ProducerState.Calls;
                }
            }
            """);

        var rootGenerated = string.Join("\n", AutoMediatorExecution.GeneratedSources(root));
        Assert.Contains("global::ImportedHandler", rootGenerated);
        Assert.DoesNotContain("AutoMediatorManifest(", rootGenerated);
        Assert.DoesNotContain("ProducerBehavior", rootGenerated);
        Assert.Equal("7:1", await AutoMediatorExecution.RunProgramAsync<string>(
            AutoMediatorExecution.EmitImage(root),
            ("Sample.Application", producerImage)));
    }

    [Fact]
    public async Task DuplicateImportedAndLocalCommand_ReportsCsam002AndOmitsOnlyThatRoute()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class DuplicateCommand : ICommand { }
            public sealed class ImportedHandler : ICommandHandler<DuplicateCommand>
            {
                public ValueTask Handle(DuplicateCommand command, CancellationToken cancellationToken) => default;
            }
            """);
        var rootCompilation = AutoMediatorTestCompilation.Create(
            "Sample.WebApi",
            [AutoMediatorExecution.EmitMetadataReference(producer)],
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

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
        Assert.Equal("CSAM002", diagnostic.Id);
        var output = AutoMediatorExecution.RunGenerator(rootCompilation, out var generatorDiagnostics);
        Assert.Empty(generatorDiagnostics);
        var mediator = Assert.Single(
            AutoMediatorExecution.GeneratedSources(output),
            source => source.Contains("class GeneratedAutoMediator"));
        Assert.DoesNotContain("typeof(global::DuplicateCommand)", mediator);
        Assert.Contains("typeof(global::ValidCommand)", mediator);
    }

    [Fact]
    public async Task InaccessibleImportedHandler_ReportsCsam006AndIsNotEmitted()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class ImportedCommand : ICommand { }
            internal sealed class ImportedHandler : ICommandHandler<ImportedCommand>
            {
                public ValueTask Handle(ImportedCommand command, CancellationToken cancellationToken) => default;
            }
            """);
        var rootCompilation = AutoMediatorTestCompilation.Create(
            "Sample.WebApi",
            [AutoMediatorExecution.EmitMetadataReference(producer)],
            "using CSteroid.AutoMediator; [assembly: AutoMediatorCompositionRoot]");

        var diagnostic = Assert.Single(await Analyze(rootCompilation));
        Assert.Equal("CSAM006", diagnostic.Id);
        var output = AutoMediatorExecution.RunGenerator(rootCompilation, out var generatorDiagnostics);
        Assert.Empty(generatorDiagnostics);
        var mediator = Assert.Single(
            AutoMediatorExecution.GeneratedSources(output),
            source => source.Contains("class GeneratedAutoMediator"));
        Assert.DoesNotContain("ImportedHandler", mediator);
        Assert.DoesNotContain("ImportedCommand", mediator);
    }

    [Fact]
    public void TwoRoots_CanImportTheSameProducerWithIndependentInternalSurfaces()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand { }
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """);
        var reference = AutoMediatorExecution.EmitMetadataReference(producer);
        const string rootSource = "using CSteroid.AutoMediator; [assembly: AutoMediatorCompositionRoot]";

        var first = Generate("First.Root", [reference], rootSource);
        var second = Generate("Second.Root", [reference], rootSource);

        foreach (var root in new[] { first, second })
        {
            var generated = string.Join("\n", AutoMediatorExecution.GeneratedSources(root));
            Assert.Contains("internal sealed class GeneratedAutoMediator", generated);
            Assert.Contains("AddAutoMediator", generated);
            Assert.Contains("global::Handler", generated);
        }
    }

    private static Compilation Generate(
        string assemblyName,
        MetadataReference[] references,
        string source)
    {
        var output = AutoMediatorExecution.RunGenerator(
            AutoMediatorTestCompilation.Create(assemblyName, references, source),
            out var diagnostics);
        Assert.Empty(diagnostics);
        return output;
    }

}
