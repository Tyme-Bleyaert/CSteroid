using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.AutoMediator.Tests;

public class AutoMediatorBehaviorEmissionTests
{
    [Fact]
    public async Task ExplicitResponseBehavior_CanReplaceMessageAndToken_AndTerminalRunsOnce()
    {
        var output = Generate("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]
            [assembly: AutoMediatorBehavior(typeof(Behavior))]

            public sealed class Command : ICommand<int> { public int Value { get; set; } }
            public static class State
            {
                public static CancellationToken ReplacementToken;
                public static int TerminalCalls;
                public static bool TokenForwarded;
            }
            public sealed class Behavior : IPipelineBehavior<Command, int>
            {
                ValueTask<int> IPipelineBehavior<Command, int>.Handle(
                    Command message,
                    AutoMediatorNext<Command, int> next,
                    CancellationToken cancellationToken)
                    => next(new Command { Value = message.Value + 1 }, State.ReplacementToken);
            }
            public sealed class Handler : ICommandHandler<Command, int>
            {
                public ValueTask<int> Handle(Command command, CancellationToken cancellationToken)
                {
                    State.TerminalCalls++;
                    State.TokenForwarded = cancellationToken == State.ReplacementToken;
                    return new(command.Value * 2);
                }
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    using var replacement = new CancellationTokenSource();
                    State.ReplacementToken = replacement.Token;
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                        .Send(new Command { Value = 2 }, new CancellationTokenSource().Token);
                    return $"{result}:{State.TerminalCalls}:{State.TokenForwarded}";
                }
            }
            """);

        Assert.Equal("6:1:True", await AutoMediatorExecution.RunProgramAsync<string>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task NonResponseChain_OrdersByOrderThenOrdinalTypeName_AndUnwindsAroundTerminal()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]
            [assembly: AutoMediatorBehavior(typeof(Zeta), Order = 5)]
            [assembly: AutoMediatorBehavior(typeof(First), Order = -1)]
            [assembly: AutoMediatorBehavior(typeof(Alpha), Order = 5)]

            public sealed class Command : ICommand { }
            public static class State { public static string Log = string.Empty; }
            public sealed class First : IPipelineBehavior<Command>
            {
                public async ValueTask Handle(Command message, AutoMediatorNext<Command> next, CancellationToken cancellationToken)
                {
                    State.Log += "First>"; await next(message, cancellationToken); State.Log += "First<;";
                }
            }
            public sealed class Alpha : IPipelineBehavior<Command>
            {
                public async ValueTask Handle(Command message, AutoMediatorNext<Command> next, CancellationToken cancellationToken)
                {
                    State.Log += "Alpha>"; await next(message, cancellationToken); State.Log += "Alpha<;";
                }
            }
            public sealed class Zeta : IPipelineBehavior<Command>
            {
                public async ValueTask Handle(Command message, AutoMediatorNext<Command> next, CancellationToken cancellationToken)
                {
                    State.Log += "Zeta>"; await next(message, cancellationToken); State.Log += "Zeta<;";
                }
            }
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken)
                {
                    State.Log += "terminal;";
                    return default;
                }
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ISender>().Send(new Command());
                    return State.Log;
                }
            }
            """);

        Assert.Equal(
            "First>Alpha>Zeta>terminal;Zeta<;Alpha<;First<;",
            await AutoMediatorExecution.RunProgramAsync<string>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task PublishBehavior_WrapsWholeFanOutOnce_AndForwardsReplacementMessageAndToken()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]
            [assembly: AutoMediatorBehavior(typeof(Behavior))]

            public sealed class Notice : IMessage { public string Value { get; set; } = string.Empty; }
            public static class State
            {
                public static string Log = string.Empty;
                public static CancellationToken ReplacementToken;
            }
            public sealed class Behavior : IPipelineBehavior<Notice>
            {
                public async ValueTask Handle(Notice message, AutoMediatorNext<Notice> next, CancellationToken cancellationToken)
                {
                    State.Log += "behavior>";
                    await next(new Notice { Value = message.Value + "!" }, State.ReplacementToken);
                    State.Log += "behavior<;";
                }
            }
            public sealed class AlphaHandler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken)
                {
                    State.Log += "A:" + message.Value + ":" + (cancellationToken == State.ReplacementToken) + ";";
                    return default;
                }
            }
            public sealed class ZetaHandler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken)
                {
                    State.Log += "Z:" + message.Value + ":" + (cancellationToken == State.ReplacementToken) + ";";
                    return default;
                }
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    using var replacement = new CancellationTokenSource();
                    State.ReplacementToken = replacement.Token;
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new Notice { Value = "event" });
                    return State.Log;
                }
            }
            """);

        Assert.Equal(
            "behavior>A:event!:True;Z:event!:True;behavior<;",
            await AutoMediatorExecution.RunProgramAsync<string>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task UnknownPublishRoute_SkipsGenericBehavior()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]
            [assembly: AutoMediatorBehavior(typeof(Behavior<>))]

            public sealed class Unknown : IMessage { }
            public static class State { public static int Calls; }
            public sealed class Behavior<TMessage> : IPipelineBehavior<TMessage> where TMessage : IMessage
            {
                public ValueTask Handle(TMessage message, AutoMediatorNext<TMessage> next, CancellationToken cancellationToken)
                {
                    State.Calls++;
                    return next(message, cancellationToken);
                }
            }
            public static class Program
            {
                public static async Task<int> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new Unknown());
                    return State.Calls;
                }
            }
            """);

        Assert.Equal(0, await AutoMediatorExecution.RunProgramAsync<int>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public void GenericConstraints_FilterClosedSelection_AndRegistrationUsesOnlyClosedTypes()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]
            [assembly: AutoMediatorBehavior(typeof(ClassOnlyBehavior<>))]

            public sealed class ClassCommand : ICommand { }
            public struct StructCommand : ICommand { }
            public sealed class ClassOnlyBehavior<TMessage> : IPipelineBehavior<TMessage>
                where TMessage : class, IMessage
            {
                public ValueTask Handle(TMessage message, AutoMediatorNext<TMessage> next, CancellationToken cancellationToken)
                    => next(message, cancellationToken);
            }
            public sealed class ClassHandler : ICommandHandler<ClassCommand>
            {
                public ValueTask Handle(ClassCommand command, CancellationToken cancellationToken) => default;
            }
            public sealed class StructHandler : ICommandHandler<StructCommand>
            {
                public ValueTask Handle(StructCommand command, CancellationToken cancellationToken) => default;
            }
            """);

        var generated = string.Join("\n", AutoMediatorExecution.GeneratedSources(output));
        Assert.Contains("ClassOnlyBehavior<global::ClassCommand>", generated);
        Assert.DoesNotContain("ClassOnlyBehavior<global::StructCommand>", generated);
        Assert.Contains("TryAddScoped<global::ClassOnlyBehavior<global::ClassCommand>>(services)", generated);
        Assert.DoesNotContain("typeof(global::ClassOnlyBehavior<>)", generated);
        Assert.DoesNotContain("IEnumerable<global::CSteroid.AutoMediator.IPipelineBehavior", generated);
    }

    [Fact]
    public async Task InvalidBehaviorSelection_RetainsRouteAndValidBehavior()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]
            [assembly: AutoMediatorBehavior(typeof(InvalidBehavior), Order = -1)]
            [assembly: AutoMediatorBehavior(typeof(ValidBehavior))]

            public sealed class Command : ICommand<int> { public int Value { get; set; } }
            public sealed class InvalidBehavior : IPipelineBehavior<Command, int>
            {
                private InvalidBehavior() { }
                public ValueTask<int> Handle(Command message, AutoMediatorNext<Command, int> next, CancellationToken cancellationToken)
                    => next(message, cancellationToken);
            }
            public sealed class ValidBehavior : IPipelineBehavior<Command, int>
            {
                public ValueTask<int> Handle(Command message, AutoMediatorNext<Command, int> next, CancellationToken cancellationToken)
                    => next(new Command { Value = message.Value + 1 }, cancellationToken);
            }
            public sealed class Handler : ICommandHandler<Command, int>
            {
                public ValueTask<int> Handle(Command command, CancellationToken cancellationToken) => new(command.Value);
            }
            public static class Program
            {
                public static async Task<int> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new Command { Value = 4 });
                }
            }
            """);

        var generated = string.Join("\n", AutoMediatorExecution.GeneratedSources(output));
        Assert.DoesNotContain("InvalidBehavior", generated);
        Assert.Contains("global::ValidBehavior", generated);
        Assert.Contains("typeof(global::Command)", generated);
        Assert.Equal(5, await AutoMediatorExecution.RunProgramAsync<int>(AutoMediatorExecution.Emit(output)));
    }

    private static Compilation Generate(string source)
    {
        var output = AutoMediatorExecution.RunGenerator(AutoMediatorTestCompilation.Create(source), out var diagnostics);
        Assert.Empty(diagnostics);
        return output;
    }
}
