using static CSteroid.Generator.Medium.Tests.MediumExecution;

namespace CSteroid.Generator.Medium.Tests;

public class MediumPublishUseTests
{
    [Fact]
    public async Task ConcretePublisherAndMediatorCalls_RecognizeValidRoutes()
    {
        var diagnostics = await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            public sealed class Notice : IMessage { }
            public sealed class Handler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken) => default;
            }
            public sealed class Consumer
            {
                public void Call(IPublisher publisher, IMedium mediator)
                {
                    _ = publisher.Publish(new Notice());
                    _ = mediator.Publish(new Notice());
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task HandlerlessConcreteMessage_AllowsPublishWithoutDiagnostics()
    {
        Assert.Empty(await Analyze("""
            using CSteroid.Medium;
            public sealed class Missing : IMessage { }
            public sealed class Consumer
            {
                public void Call(IPublisher publisher) => _ = publisher.Publish(new Missing());
            }
            """));
    }

    [Fact]
    public async Task CompilerConversionToMessageInterface_UsesConcreteOperand()
    {
        Assert.Empty(await Analyze("""
            using CSteroid.Medium;
            public sealed class Missing : IMessage { }
            public sealed class Consumer
            {
                public void Call(IMedium mediator) => _ = mediator.Publish((IMessage)new Missing());
            }
            """));
    }

    [Fact]
    public async Task InterfaceHeldAndTypeParameterMessages_AreNotGuessed()
    {
        var diagnostics = await Analyze("""
            using CSteroid.Medium;
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
            using CSteroid.Medium;
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
    public async Task UnusedMessageDeclaration_DoesNotReportcsm018()
    {
        Assert.Empty(await Analyze("using CSteroid.Medium; public sealed class Unused : IMessage { }"));
    }

    [Fact]
    public async Task CommandPassedToPublish_Reportscsm018()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            public sealed class Consumer
            {
                public void Call(IPublisher publisher) => _ = publisher.Publish(new Command());
            }
            """));

        Assert.Equal("CSM018", diagnostic.Id);
    }

    [Fact]
    public async Task InvalidDeclarationSuppressesCascadecsm018()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
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

        Assert.Equal("CSM011", diagnostic.Id);
    }
}
