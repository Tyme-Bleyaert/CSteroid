using Microsoft.CodeAnalysis;
using static CSteroid.Generator.AutoMediator.Tests.AutoMediatorExecution;

namespace CSteroid.Generator.AutoMediator.Tests;

public class AutoMediatorAnalyzerTests
{
    [Fact]
    public void SupportedDiagnostics_ContainOnlyCurrentRules()
    {
        var analyzer = new global::CSteroid.Generator.AutoMediator.AutoMediatorDiagnosticAnalyzer();
        Assert.Equal(
            new[] { "CSAM001", "CSAM002", "CSAM006", "CSAM011", "CSAM012", "CSAM013", "CSAM014", "CSAM015", "CSAM016", "CSAM017", "CSAM018" },
            analyzer.SupportedDiagnostics.Select(descriptor => descriptor.Id));
        Assert.All(analyzer.SupportedDiagnostics, descriptor => Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity));
    }

    [Fact]
    public void Generator_DoesNotReportCsamDiagnostics()
    {
        var output = AutoMediatorExecution.RunGenerator(AutoMediatorTestCompilation.Create("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class Command : ICommand { }
            public sealed class FirstHandler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            public sealed class SecondHandler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """), out var diagnostics);

        Assert.Empty(diagnostics);
        Assert.DoesNotContain(output.GetDiagnostics(), diagnostic => diagnostic.Id.StartsWith("CSAM", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateCommand_ReportsCsam002OnSecondHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand { }
            public sealed class FirstHandler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            public sealed class SecondHandler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """;

        var diagnostic = Assert.Single(await Analyze(source));
        Assert.Equal("CSAM002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("SecondHandler", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Fact]
    public async Task InaccessibleLocalHandler_ReportsCsam006()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand { }
            public class Container
            {
                private sealed class Handler : ICommandHandler<Command>
                {
                    public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
                }
            }
            """));

        Assert.Equal("CSAM006", diagnostic.Id);
    }

    [Theory]
    [InlineData("public abstract class Handler : ICommandHandler<Command>")]
    [InlineData("public sealed class Handler<T> : ICommandHandler<Command>")]
    public async Task UnsupportedHandlerShape_ReportsCsam011(string declaration)
    {
        var diagnostic = Assert.Single(await Analyze($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand { }
            {{declaration}}
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """));

        Assert.Equal("CSAM011", diagnostic.Id);
    }

    [Fact]
    public async Task HandlerWithoutUsablePublicConstructor_ReportsCsam011()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand { }
            public sealed class Handler : ICommandHandler<Command>
            {
                private Handler() { }
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """));

        Assert.Equal("CSAM011", diagnostic.Id);
        Assert.Contains("constructor", diagnostic.GetMessage());
    }

    [Fact]
    public async Task InheritedOnlyHandler_IsUndiscoveredWithoutCsam011()
    {
        var diagnostics = await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand { }
            public class BaseHandler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            public sealed class DerivedHandler : BaseHandler { }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MultipleResponseContracts_ReportCsam012()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand<string>, ICommand<int> { }
            public sealed class Handler : ICommandHandler<Command, string>
            {
                public ValueTask<string> Handle(Command command, CancellationToken cancellationToken) => new(string.Empty);
            }
            """));

        Assert.Equal("CSAM012", diagnostic.Id);
    }

    [Fact]
    public async Task InvalidHandlerLifetime_ReportsCsam013AtAttribute()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand { }
            [HandlerLifetime((ServiceLifetime)99)]
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """;

        var diagnostic = Assert.Single(await Analyze(source));
        Assert.Equal("CSAM013", diagnostic.Id);
        Assert.Contains("HandlerLifetime", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Fact]
    public async Task UnsupportedBehaviorDeclaration_ReportsCsam014()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using CSteroid.AutoMediator;
            [assembly: AutoMediatorBehavior(typeof(Behavior))]
            public sealed class Behavior { }
            """));

        Assert.Equal("CSAM014", diagnostic.Id);
        Assert.Contains("pipeline", diagnostic.GetMessage(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateBehaviorDeclarations_ReportCsam015()
    {
        var diagnostics = await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            [assembly: AutoMediatorBehavior(typeof(Behavior))]
            [assembly: AutoMediatorBehavior(typeof(Behavior), Order = 10)]
            public sealed class Notice : IMessage { }
            public sealed class Behavior : IPipelineBehavior<Notice>
            {
                public ValueTask Handle(Notice message, AutoMediatorNext<Notice> next, CancellationToken cancellationToken)
                    => next(message, cancellationToken);
            }
            """);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("CSAM015", diagnostic.Id));
    }

    [Fact]
    public async Task SelectedBehaviorWithoutUsableConstructor_ReportsCsam016OncePerMessage()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            [assembly: AutoMediatorCompositionRoot]
            [assembly: AutoMediatorBehavior(typeof(Behavior))]
            public sealed class Notice : IMessage { }
            public sealed class Behavior : IPipelineBehavior<Notice>
            {
                private Behavior() { }
                public ValueTask Handle(Notice message, AutoMediatorNext<Notice> next, CancellationToken cancellationToken)
                    => next(message, cancellationToken);
            }
            public sealed class FirstHandler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken) => default;
            }
            public sealed class SecondHandler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken) => default;
            }
            """));

        Assert.Equal("CSAM016", diagnostic.Id);
    }

    [Fact]
    public async Task UnsupportedImportedManifestVersion_ReportsCsam017()
    {
        var producer = AutoMediatorTestCompilation.Create("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            [assembly: AutoMediatorManifest(99, typeof(Handler))]
            public sealed class Command : ICommand { }
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """);
        var root = AutoMediatorTestCompilation.Create(
            "Sample.WebApi",
            new[] { AutoMediatorExecution.EmitMetadataReference(producer) },
            "using CSteroid.AutoMediator; [assembly: AutoMediatorCompositionRoot]");

        var diagnostic = Assert.Single(await Analyze(root));
        Assert.Equal("CSAM017", diagnostic.Id);
        Assert.Contains("Sample.Application", diagnostic.GetMessage());
    }

    [Fact]
    public async Task GeneratedOutput_DoesNotSuppressSourceSymbolAndInvocationActions()
    {
        var compilation = AutoMediatorTestCompilation.Create("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            [assembly: AutoMediatorCompositionRoot]
            public sealed class Command : ICommand { }
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            public sealed class Missing : ICommand { }
            public sealed class Use
            {
                public ValueTask Call(ISender sender) => sender.Send(new Missing());
            }
            """);
        var generated = AutoMediatorExecution.RunGenerator(compilation, out _);
        var diagnostic = Assert.Single(await Analyze(generated));
        Assert.Equal("CSAM001", diagnostic.Id);
    }

}
