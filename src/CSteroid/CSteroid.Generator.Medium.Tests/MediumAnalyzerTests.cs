using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Medium.Tests.MediumExecution;

namespace CSteroid.Generator.Medium.Tests;

public class MediumAnalyzerTests
{
    [Fact]
    public void SupportedDiagnostics_ContainOnlyCurrentRules()
    {
        var analyzer = new global::CSteroid.Generator.Medium.MediumDiagnosticAnalyzer();
        Assert.Equal(
            new[] { "CSM001", "CSM002", "CSM006", "CSM011", "CSM012", "CSM013", "CSM014", "CSM015", "CSM016", "CSM017", "CSM018", "CSM019", "CSM020", "CSM021" },
            analyzer.SupportedDiagnostics.Select(descriptor => descriptor.Id));
        Assert.All(
            analyzer.SupportedDiagnostics.Where(descriptor => descriptor.Id != "CSM020"),
            descriptor => Assert.Equal(DiagnosticSeverity.Error, descriptor.DefaultSeverity));
        Assert.Equal(
            DiagnosticSeverity.Warning,
            Assert.Single(analyzer.SupportedDiagnostics, descriptor => descriptor.Id == "CSM020").DefaultSeverity);
    }

    [Fact]
    public void Generator_DoesNotReportcsmDiagnostics()
    {
        var output = MediumExecution.RunGenerator(MediumTestCompilation.Create("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

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
        Assert.DoesNotContain(output.GetDiagnostics(), diagnostic => diagnostic.Id.StartsWith("CSM", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateCommand_Reportscsm002OnSecondHandler()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
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
        Assert.Equal("CSM002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("SecondHandler", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Fact]
    public async Task InaccessibleLocalHandler_Reportscsm006()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            public class Container
            {
                private sealed class Handler : ICommandHandler<Command>
                {
                    public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
                }
            }
            """));

        Assert.Equal("CSM006", diagnostic.Id);
    }

    [Theory]
    [InlineData("public abstract class Handler : ICommandHandler<Command>")]
    [InlineData("public sealed class Handler<T> : ICommandHandler<Command>")]
    public async Task UnsupportedHandlerShape_Reportscsm011(string declaration)
    {
        var diagnostic = Assert.Single(await Analyze($$"""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            {{declaration}}
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """));

        Assert.Equal("CSM011", diagnostic.Id);
    }

    [Fact]
    public async Task HandlerWithoutUsablePublicConstructor_Reportscsm011()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            public sealed class Handler : ICommandHandler<Command>
            {
                private Handler() { }
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """));

        Assert.Equal("CSM011", diagnostic.Id);
        Assert.Contains("constructor", diagnostic.GetMessage());
    }

    [Fact]
    public async Task InheritedOnlyHandler_IsUndiscoveredWithoutcsm011()
    {
        var diagnostics = await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
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
    public async Task MultipleResponseContracts_Reportcsm012()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class Command : ICommand<string>, ICommand<int> { }
            public sealed class Handler : ICommandHandler<Command, string>
            {
                public ValueTask<string> Handle(Command command, CancellationToken cancellationToken) => new(string.Empty);
            }
            """));

        Assert.Equal("CSM012", diagnostic.Id);
    }

    [Fact]
    public async Task InvalidHandlerLifetime_Reportscsm013AtAttribute()
    {
        const string source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            [HandlerLifetime((ServiceLifetime)99)]
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """;

        var diagnostic = Assert.Single(await Analyze(source));
        Assert.Equal("CSM013", diagnostic.Id);
        Assert.Contains("HandlerLifetime", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Fact]
    public async Task UnsupportedBehaviorDeclaration_Reportscsm014()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using CSteroid.Medium;
            [assembly: MediumBehavior(typeof(Behavior))]
            public sealed class Behavior { }
            """));

        Assert.Equal("CSM014", diagnostic.Id);
        Assert.Contains("pipeline", diagnostic.GetMessage(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateBehaviorDeclarations_Reportcsm015()
    {
        var diagnostics = await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumBehavior(typeof(Behavior))]
            [assembly: MediumBehavior(typeof(Behavior), Order = 10)]
            public sealed class Notice : IMessage { }
            public sealed class Behavior : IPipelineBehavior<Notice>
            {
                public ValueTask Handle(Notice message, MediumNext<Notice> next, CancellationToken cancellationToken)
                    => next(message, cancellationToken);
            }
            """);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("CSM015", diagnostic.Id));
    }

    [Fact]
    public async Task SelectedBehaviorWithoutUsableConstructor_Reportscsm016OncePerMessage()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(Behavior))]
            public sealed class Notice : IMessage { }
            public sealed class Behavior : IPipelineBehavior<Notice>
            {
                private Behavior() { }
                public ValueTask Handle(Notice message, MediumNext<Notice> next, CancellationToken cancellationToken)
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

        Assert.Equal("CSM016", diagnostic.Id);
    }

    [Fact]
    public async Task UnsupportedImportedManifestVersion_Reportscsm017()
    {
        var producer = MediumTestCompilation.Create("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumManifest(99, typeof(Handler))]
            public sealed class Command : ICommand { }
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """);
        var root = MediumTestCompilation.Create(
            "Sample.WebApi",
            new[] { MediumExecution.EmitMetadataReference(producer) },
            "using CSteroid.Medium; [assembly: MediumCompositionRoot]");

        var diagnostic = Assert.Single(await Analyze(root));
        Assert.Equal("CSM017", diagnostic.Id);
        Assert.Contains("Sample.Application", diagnostic.GetMessage());
        Assert.Contains("rebuilt", diagnostic.GetMessage());
        Assert.Contains("Medium.Manifest.g.cs", diagnostic.GetMessage());
    }

    [Fact]
    public async Task GeneratedOutput_DoesNotSuppressSourceSymbolAndInvocationActions()
    {
        var compilation = MediumTestCompilation.Create("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
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
        var generated = MediumExecution.RunGenerator(compilation, out _);
        var diagnostic = Assert.Single(await Analyze(generated));
        Assert.Equal("CSM001", diagnostic.Id);
    }

}
