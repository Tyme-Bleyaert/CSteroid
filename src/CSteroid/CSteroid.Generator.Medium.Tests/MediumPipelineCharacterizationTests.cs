namespace CSteroid.Generator.Medium.Tests;

public sealed class MediumPipelineCharacterizationTests
{
    [Fact]
    public async Task ResponseSend_ShortCircuitsAfterEagerReverseBehaviorResolution()
    {
        var result = await Run("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(OuterBehavior), Order = -1, Lifetime = ServiceLifetime.Transient)]
            [assembly: MediumBehavior(typeof(ShortCircuitBehavior), Lifetime = ServiceLifetime.Transient)]
            [assembly: MediumBehavior(typeof(InnerBehavior), Order = 1, Lifetime = ServiceLifetime.Transient)]

            public sealed class Command : ICommand<string>
            {
                public string Value { get; set; } = string.Empty;
            }
            public static class State
            {
                public static string Log = string.Empty;
                public static CancellationToken ReplacementToken;
                public static int OuterResolutions;
                public static int ShortCircuitResolutions;
                public static int InnerResolutions;
                public static int HandlerResolutions;
            }
            public sealed class OuterBehavior : IPipelineBehavior<Command, string>
            {
                public OuterBehavior()
                {
                    State.OuterResolutions++;
                    State.Log += "resolve-outer;";
                }
                public ValueTask<string> Handle(
                    Command message,
                    MediumNext<Command, string> next,
                    CancellationToken cancellationToken)
                {
                    State.Log += "outer;";
                    return next(
                        new Command { Value = message.Value + "-replaced" },
                        State.ReplacementToken);
                }
            }
            public sealed class ShortCircuitBehavior : IPipelineBehavior<Command, string>
            {
                public ShortCircuitBehavior()
                {
                    State.ShortCircuitResolutions++;
                    State.Log += "resolve-short;";
                }
                public ValueTask<string> Handle(
                    Command message,
                    MediumNext<Command, string> next,
                    CancellationToken cancellationToken)
                {
                    State.Log += $"short:{message.Value}:{cancellationToken == State.ReplacementToken};";
                    return new("done");
                }
            }
            public sealed class InnerBehavior : IPipelineBehavior<Command, string>
            {
                public InnerBehavior()
                {
                    State.InnerResolutions++;
                    State.Log += "resolve-inner;";
                }
                public ValueTask<string> Handle(
                    Command message,
                    MediumNext<Command, string> next,
                    CancellationToken cancellationToken)
                {
                    State.Log += "inner;";
                    return next(message, cancellationToken);
                }
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class Handler : ICommandHandler<Command, string>
            {
                public Handler()
                {
                    State.HandlerResolutions++;
                    State.Log += "resolve-handler;";
                }
                public ValueTask<string> Handle(Command command, CancellationToken cancellationToken)
                {
                    State.Log += "handler;";
                    return new("unexpected");
                }
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    using var replacement = new CancellationTokenSource();
                    State.ReplacementToken = replacement.Token;
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var response = await scope.ServiceProvider.GetRequiredService<ISender>()
                        .Send(new Command { Value = "original" });
                    return $"{response}|{State.Log}|{State.OuterResolutions}:{State.ShortCircuitResolutions}:{State.InnerResolutions}:{State.HandlerResolutions}";
                }
            }
            """);

        Assert.Equal(
            "done|resolve-inner;resolve-short;resolve-outer;outer;short:original-replaced:True;|1:1:1:0",
            result);
    }

    [Fact]
    public async Task NonResponseSend_InnerResolutionFailurePreventsOuterExecution()
    {
        var result = await Run("""
            using System.IO;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(OuterBehavior), Order = -1, Lifetime = ServiceLifetime.Transient)]
            [assembly: MediumBehavior(typeof(InnerBehavior), Lifetime = ServiceLifetime.Transient)]

            public sealed class Command : ICommand { }
            public static class State
            {
                public static string Log = string.Empty;
                public static int InnerResolutionAttempts;
                public static int OuterResolutions;
                public static int OuterExecutions;
                public static int HandlerResolutions;
            }
            public sealed class OuterBehavior : IPipelineBehavior<Command>
            {
                public OuterBehavior()
                {
                    State.OuterResolutions++;
                    State.Log += "resolve-outer;";
                }
                public ValueTask Handle(Command message, MediumNext<Command> next, CancellationToken cancellationToken)
                {
                    State.OuterExecutions++;
                    State.Log += "outer;";
                    return next(message, cancellationToken);
                }
            }
            public sealed class InnerBehavior : IPipelineBehavior<Command>
            {
                public InnerBehavior()
                {
                    State.InnerResolutionAttempts++;
                    State.Log += "resolve-inner;";
                    throw new InvalidDataException("inner failed");
                }
                public ValueTask Handle(Command message, MediumNext<Command> next, CancellationToken cancellationToken)
                    => next(message, cancellationToken);
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class Handler : ICommandHandler<Command>
            {
                public Handler()
                {
                    State.HandlerResolutions++;
                    State.Log += "resolve-handler;";
                }
                public ValueTask Handle(Command command, CancellationToken cancellationToken)
                {
                    State.Log += "handler;";
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
                    try
                    {
                        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new Command());
                        return "missing failure";
                    }
                    catch (InvalidDataException exception)
                    {
                        return $"{exception.Message}|{State.Log}|{State.InnerResolutionAttempts}:{State.OuterResolutions}:{State.OuterExecutions}:{State.HandlerResolutions}";
                    }
                }
            }
            """);

        Assert.Equal("inner failed|resolve-inner;|1:0:0:0", result);
    }

    [Fact]
    public async Task NonResponseSend_RepeatedNextResolvesHandlerPerInvocation()
    {
        var result = await Run("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(OuterBehavior), Order = -1, Lifetime = ServiceLifetime.Transient)]
            [assembly: MediumBehavior(typeof(InnerBehavior), Lifetime = ServiceLifetime.Transient)]

            public sealed class Command : ICommand { }
            public static class State
            {
                public static string Log = string.Empty;
                public static int OuterResolutions;
                public static int InnerResolutions;
                public static int HandlerResolutions;
                public static int InnerExecutions;
                public static int HandlerCalls;
            }
            public sealed class OuterBehavior : IPipelineBehavior<Command>
            {
                public OuterBehavior() => State.OuterResolutions++;
                public async ValueTask Handle(
                    Command message,
                    MediumNext<Command> next,
                    CancellationToken cancellationToken)
                {
                    State.Log += "outer>";
                    await next(message, cancellationToken);
                    await next(message, cancellationToken);
                    State.Log += "outer<;";
                }
            }
            public sealed class InnerBehavior : IPipelineBehavior<Command>
            {
                public InnerBehavior() => State.InnerResolutions++;
                public async ValueTask Handle(
                    Command message,
                    MediumNext<Command> next,
                    CancellationToken cancellationToken)
                {
                    State.InnerExecutions++;
                    State.Log += "inner>";
                    await next(message, cancellationToken);
                    State.Log += "inner<;";
                }
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class Handler : ICommandHandler<Command>
            {
                public Handler() => State.HandlerResolutions++;
                public ValueTask Handle(Command command, CancellationToken cancellationToken)
                {
                    State.HandlerCalls++;
                    State.Log += "handler;";
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
                    await scope.ServiceProvider.GetRequiredService<ISender>().Send(new Command());
                    return $"{State.OuterResolutions}:{State.InnerResolutions}:{State.HandlerResolutions}:{State.InnerExecutions}:{State.HandlerCalls}|{State.Log}";
                }
            }
            """);

        Assert.Equal(
            "1:1:2:2:2|outer>inner>handler;inner<;inner>handler;inner<;outer<;",
            result);
    }

    [Fact]
    public async Task ConcurrentColdResponseSends_ResolveEachBehaviorAndHandlerOncePerCall()
    {
        var result = await Run("""
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(Behavior), Lifetime = ServiceLifetime.Transient)]

            public sealed class Command : ICommand<int>
            {
                public int Value { get; set; }
            }
            public static class State
            {
                public const int CallCount = 8;
                public static readonly TaskCompletionSource<bool> AllEntered =
                    new(TaskCreationOptions.RunContinuationsAsynchronously);
                public static int BehaviorResolutions;
                public static int HandlerResolutions;
                public static int Entered;
            }
            public sealed class Behavior : IPipelineBehavior<Command, int>
            {
                public Behavior() => Interlocked.Increment(ref State.BehaviorResolutions);
                public async ValueTask<int> Handle(
                    Command message,
                    MediumNext<Command, int> next,
                    CancellationToken cancellationToken)
                {
                    if (Interlocked.Increment(ref State.Entered) == State.CallCount)
                        State.AllEntered.TrySetResult(true);
                    await State.AllEntered.Task;
                    return await next(message, cancellationToken);
                }
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class Handler : ICommandHandler<Command, int>
            {
                public Handler() => Interlocked.Increment(ref State.HandlerResolutions);
                public ValueTask<int> Handle(Command command, CancellationToken cancellationToken)
                    => new(command.Value);
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                    var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var pending = Enumerable.Range(0, State.CallCount)
                        .Select(value => Task.Run(async () =>
                        {
                            await start.Task;
                            return await sender.Send(new Command { Value = value });
                        }))
                        .ToArray();
                    start.SetResult(true);
                    var responses = await Task.WhenAll(pending);
                    return $"{State.BehaviorResolutions}:{State.HandlerResolutions}:{State.Entered}:{responses.Sum()}";
                }
            }
            """);

        Assert.Equal("8:8:8:28", result);
    }

    [Fact]
    public async Task ResponseSend_SeparateScopesUseTheirOwnServicesAndForwardReplacements()
    {
        var result = await Run("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(Behavior))]

            public sealed class Command : ICommand<string>
            {
                public string Value { get; set; } = string.Empty;
            }
            public static class State
            {
                public static CancellationToken ReplacementToken;
                public static int ScopeIds;
                public static int BehaviorResolutions;
                public static int HandlerResolutions;
            }
            public sealed class ScopeMarker
            {
                public int Id { get; } = Interlocked.Increment(ref State.ScopeIds);
            }
            public sealed class Behavior : IPipelineBehavior<Command, string>
            {
                private readonly ScopeMarker _scope;
                public Behavior(ScopeMarker scope)
                {
                    _scope = scope;
                    State.BehaviorResolutions++;
                }
                public ValueTask<string> Handle(
                    Command message,
                    MediumNext<Command, string> next,
                    CancellationToken cancellationToken)
                    => next(
                        new Command { Value = $"{message.Value}-replaced-{_scope.Id}" },
                        State.ReplacementToken);
            }
            public sealed class Handler : ICommandHandler<Command, string>
            {
                private readonly ScopeMarker _scope;
                public Handler(ScopeMarker scope)
                {
                    _scope = scope;
                    State.HandlerResolutions++;
                }
                public ValueTask<string> Handle(Command command, CancellationToken cancellationToken)
                    => new($"{command.Value}:{_scope.Id}:{cancellationToken == State.ReplacementToken}");
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    using var replacement = new CancellationTokenSource();
                    State.ReplacementToken = replacement.Token;
                    var services = new ServiceCollection();
                    services.AddScoped<ScopeMarker>();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider();
                    string first;
                    using (var scope = provider.CreateScope())
                    {
                        first = await scope.ServiceProvider.GetRequiredService<ISender>()
                            .Send(new Command { Value = "first" });
                    }
                    string second;
                    using (var scope = provider.CreateScope())
                    {
                        second = await scope.ServiceProvider.GetRequiredService<ISender>()
                            .Send(new Command { Value = "second" });
                    }
                    return $"{first}|{second}|{State.BehaviorResolutions}:{State.HandlerResolutions}";
                }
            }
            """);

        Assert.Equal(
            "first-replaced-1:1:True|second-replaced-2:2:True|2:2",
            result);
    }

    [Fact]
    public async Task Publish_SingleHandlerRunsOneChainWithReplacement()
    {
        var result = await Run("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(Behavior), Lifetime = ServiceLifetime.Transient)]

            public sealed class Notice : IMessage
            {
                public string Value { get; set; } = string.Empty;
            }
            public static class State
            {
                public static string Log = string.Empty;
                public static CancellationToken ReplacementToken;
                public static int BehaviorResolutions;
                public static int HandlerResolutions;
                public static int BehaviorExecutions;
                public static int HandlerExecutions;
            }
            public sealed class Behavior : IPipelineBehavior<Notice>
            {
                public Behavior() => State.BehaviorResolutions++;
                public async ValueTask Handle(
                    Notice message,
                    MediumNext<Notice> next,
                    CancellationToken cancellationToken)
                {
                    State.BehaviorExecutions++;
                    State.Log += "behavior>";
                    await next(new Notice { Value = message.Value + "-replaced" }, State.ReplacementToken);
                    State.Log += "behavior<;";
                }
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class Handler : IMessageHandler<Notice>
            {
                public Handler() => State.HandlerResolutions++;
                public ValueTask Handle(Notice message, CancellationToken cancellationToken)
                {
                    State.HandlerExecutions++;
                    State.Log += $"handler:{message.Value}:{cancellationToken == State.ReplacementToken};";
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
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IPublisher>()
                        .Publish(new Notice { Value = "published" });
                    return $"{State.BehaviorResolutions}:{State.HandlerResolutions}:{State.BehaviorExecutions}:{State.HandlerExecutions}|{State.Log}";
                }
            }
            """);

        Assert.Equal(
            "1:1:1:1|behavior>handler:published-replaced:True;behavior<;",
            result);
    }

    [Fact]
    public async Task Publish_FanOutRunsOneChainAroundAllHandlers()
    {
        var result = await Run("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(Behavior), Lifetime = ServiceLifetime.Transient)]

            public sealed class Notice : IMessage
            {
                public string Value { get; set; } = string.Empty;
            }
            public static class State
            {
                public static string Log = string.Empty;
                public static CancellationToken ReplacementToken;
                public static int BehaviorResolutions;
                public static int AlphaResolutions;
                public static int ZetaResolutions;
                public static int BehaviorExecutions;
            }
            public sealed class Behavior : IPipelineBehavior<Notice>
            {
                public Behavior() => State.BehaviorResolutions++;
                public async ValueTask Handle(
                    Notice message,
                    MediumNext<Notice> next,
                    CancellationToken cancellationToken)
                {
                    State.BehaviorExecutions++;
                    State.Log += "behavior>";
                    await next(new Notice { Value = message.Value + "-replaced" }, State.ReplacementToken);
                    State.Log += "behavior<;";
                }
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class AlphaHandler : IMessageHandler<Notice>
            {
                public AlphaHandler() => State.AlphaResolutions++;
                public ValueTask Handle(Notice message, CancellationToken cancellationToken)
                {
                    State.Log += $"A:{message.Value}:{cancellationToken == State.ReplacementToken};";
                    return default;
                }
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class ZetaHandler : IMessageHandler<Notice>
            {
                public ZetaHandler() => State.ZetaResolutions++;
                public ValueTask Handle(Notice message, CancellationToken cancellationToken)
                {
                    State.Log += $"Z:{message.Value}:{cancellationToken == State.ReplacementToken};";
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
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IPublisher>()
                        .Publish(new Notice { Value = "fanout" });
                    return $"{State.BehaviorResolutions}:{State.AlphaResolutions}:{State.ZetaResolutions}:{State.BehaviorExecutions}|{State.Log}";
                }
            }
            """);

        Assert.Equal(
            "1:1:1:1|behavior>A:fanout-replaced:True;Z:fanout-replaced:True;behavior<;",
            result);
    }

    private static Task<string> Run(string source)
    {
        var output = MediumExecution.RunGenerator(MediumTestCompilation.Create(source), out var diagnostics);
        Assert.Empty(diagnostics);
        return MediumExecution.RunProgramAsync<string>(MediumExecution.Emit(output));
    }
}
