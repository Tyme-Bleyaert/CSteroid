using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CSteroid.Generator.AutoMediator.Tests.AutoMediatorExecution;

namespace CSteroid.Generator.AutoMediator.Tests;

public class AutoMediatorPublishUseTests
{
    [Fact]
    public async Task ConcretePublisherAndMediatorCalls_RecognizeValidRoutes()
    {
        var diagnostics = await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            public sealed class Notice : IMessage { }
            public sealed class Handler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken) => default;
            }
            public sealed class Consumer
            {
                public void Call(IPublisher publisher, IAutoMediator mediator)
                {
                    _ = publisher.Publish(new Notice());
                    _ = mediator.Publish(new Notice());
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MissingConcreteRoute_ReportsCsam018AtInvocation()
    {
        const string source = """
            using CSteroid.AutoMediator;
            public sealed class Missing : IMessage { }
            public sealed class Consumer
            {
                public void Call(IPublisher publisher) => _ = publisher.Publish(new Missing());
            }
            """;
        var compilation = AutoMediatorTestCompilation.Create(source);

        var diagnostic = Assert.Single(await Analyze(compilation));
        Assert.Equal("CSAM018", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        var invocation = compilation.SyntaxTrees.Single().GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>().Single();
        Assert.Equal(invocation.Span, diagnostic.Location.SourceSpan);
    }

    [Fact]
    public async Task CompilerConversionToMessageInterface_UsesConcreteOperand()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using CSteroid.AutoMediator;
            public sealed class Missing : IMessage { }
            public sealed class Consumer
            {
                public void Call(IAutoMediator mediator) => _ = mediator.Publish((IMessage)new Missing());
            }
            """));

        Assert.Equal("CSAM018", diagnostic.Id);
    }

    [Fact]
    public async Task InterfaceHeldAndTypeParameterMessages_AreNotGuessed()
    {
        var diagnostics = await Analyze("""
            using CSteroid.AutoMediator;
            public sealed class Consumer
            {
                public void Call(IPublisher publisher, IMessage message) => _ = publisher.Publish(message);
                public void Call<T>(IPublisher publisher, T message) where T : IMessage => _ = publisher.Publish(message);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UnrelatedSameNamedPublishMethod_IsNotAnalyzedAsMediatorUse()
    {
        var diagnostics = await Analyze("""
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Missing : IMessage { }
            public sealed class UnrelatedPublisher
            {
                public ValueTask Publish(IMessage message) => default;
            }
            public sealed class Consumer
            {
                public void Call(UnrelatedPublisher publisher) => _ = publisher.Publish(new Missing());
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UnusedMessageDeclaration_DoesNotReportCsam018()
    {
        Assert.Empty(await Analyze("using CSteroid.AutoMediator; public sealed class Unused : IMessage { }"));
    }

    [Fact]
    public async Task CommandPassedToPublish_ReportsCsam018()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using CSteroid.AutoMediator;
            public sealed class Command : ICommand { }
            public sealed class Consumer
            {
                public void Call(IPublisher publisher) => _ = publisher.Publish(new Command());
            }
            """));

        Assert.Equal("CSAM018", diagnostic.Id);
    }

    [Fact]
    public async Task InvalidDeclarationSuppressesCascadeCsam018()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;
            public sealed class Notice : IMessage { }
            public abstract class Handler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken) => default;
            }
            public sealed class Consumer
            {
                public void Call(IPublisher publisher) => _ = publisher.Publish(new Notice());
            }
            """));

        Assert.Equal("CSAM011", diagnostic.Id);
    }
}
