using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Medium.Tests;

public class MediumStreamEmissionTests
{
    [Fact]
    public async Task Stream_ResolvesAtInvocationAndDefersHandlerIteration()
    {
        var output = Generate("""
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class Numbers : IStreamCommand<int>
            {
                public Numbers(int count) { Count = count; }
                public int Count { get; }
            }
            public static class State
            {
                public static int Constructed;
                public static int Iterated;
            }
            public sealed class Handler : IStreamCommandHandler<Numbers, int>
            {
                public Handler() { State.Constructed++; }
                public async IAsyncEnumerable<int> Handle(
                    Numbers command,
                    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
                {
                    State.Iterated++;
                    await Task.CompletedTask;
                    for (var value = 1; value <= command.Count; value++)
                        yield return value;
                }
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMedium>();
                    var stream = mediator.Stream(new Numbers(3));
                    var before = State.Constructed + ":" + State.Iterated;
                    var sum = 0;
                    await foreach (var value in stream)
                        sum += value;
                    return before + ":" + sum + ":" + State.Iterated;
                }
            }
            """);

        Assert.Equal("1:0:6:1", await MediumExecution.RunProgramAsync<string>(MediumExecution.Emit(output)));
    }

    [Fact]
    public async Task StreamBehaviors_RunInOrderShareScopeAndForwardCancellation()
    {
        var output = Generate("""
            using System;
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(OuterBehavior<,>), Order = 0)]
            [assembly: MediumBehavior(typeof(InnerBehavior<,>), Order = 1)]

            public sealed class NumberQuery : IStreamCommand<int> { }
            public sealed class ScopeState
            {
                public Guid Id { get; } = Guid.NewGuid();
            }
            public static class State
            {
                public static readonly List<string> Log = new();
                public static CancellationToken Token;
            }
            public sealed class OuterBehavior<TCommand, T> : IStreamPipelineBehavior<TCommand, T>
                where TCommand : IStreamCommand<T>
            {
                public async IAsyncEnumerable<T> Handle(
                    TCommand message,
                    MediumStreamNext<TCommand, T> next,
                    [EnumeratorCancellation] CancellationToken cancellationToken)
                {
                    State.Log.Add("outer-start");
                    await foreach (var item in next(message, cancellationToken).WithCancellation(cancellationToken))
                        yield return item;
                    State.Log.Add("outer-end");
                }
            }
            public sealed class InnerBehavior<TCommand, T> : IStreamPipelineBehavior<TCommand, T>
                where TCommand : IStreamCommand<T>
            {
                private readonly ScopeState _scope;
                public InnerBehavior(ScopeState scope) { _scope = scope; }
                public async IAsyncEnumerable<T> Handle(
                    TCommand message,
                    MediumStreamNext<TCommand, T> next,
                    [EnumeratorCancellation] CancellationToken cancellationToken)
                {
                    State.Log.Add("inner-start:" + _scope.Id);
                    await foreach (var item in next(message, cancellationToken).WithCancellation(cancellationToken))
                        yield return item;
                    State.Log.Add("inner-end:" + _scope.Id);
                }
            }
            public sealed class Handler : IStreamCommandHandler<NumberQuery, int>
            {
                private readonly ScopeState _scope;
                public Handler(ScopeState scope) { _scope = scope; }
                public async IAsyncEnumerable<int> Handle(
                    NumberQuery command,
                    [EnumeratorCancellation] CancellationToken cancellationToken)
                {
                    State.Token = cancellationToken;
                    State.Log.Add("handler:" + _scope.Id);
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.CompletedTask;
                    yield return 1;
                }
            }
            public static class Program
            {
                public static async Task<bool> Run()
                {
                    var services = new ServiceCollection();
                    services.AddScoped<ScopeState>();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    var token = new CancellationTokenSource().Token;
                    Guid firstScope;
                    Guid secondScope;
                    using (var scope = provider.CreateScope())
                    {
                        firstScope = scope.ServiceProvider.GetRequiredService<ScopeState>().Id;
                        var mediator = scope.ServiceProvider.GetRequiredService<IMedium>();
                        await foreach (var item in mediator.Stream(new NumberQuery(), token)) { }
                    }
                    using (var scope = provider.CreateScope())
                    {
                        secondScope = scope.ServiceProvider.GetRequiredService<ScopeState>().Id;
                        var mediator = scope.ServiceProvider.GetRequiredService<IMedium>();
                        await foreach (var item in mediator.Stream(new NumberQuery(), token)) { }
                    }

                    var ordered = State.Log.Count == 10
                        && State.Log[0] == "outer-start"
                        && State.Log[1] == "inner-start:" + firstScope
                        && State.Log[2] == "handler:" + firstScope
                        && State.Log[3] == "inner-end:" + firstScope
                        && State.Log[4] == "outer-end"
                        && State.Log[5] == "outer-start"
                        && State.Log[6] == "inner-start:" + secondScope
                        && State.Log[7] == "handler:" + secondScope
                        && State.Log[8] == "inner-end:" + secondScope
                        && State.Log[9] == "outer-end";
                    var tokenForwarded = State.Token == token;
                    using var cancelled = new CancellationTokenSource();
                    cancelled.Cancel();
                    var cancellationObserved = false;
                    using (var scope = provider.CreateScope())
                    {
                        try
                        {
                            var mediator = scope.ServiceProvider.GetRequiredService<IMedium>();
                            await foreach (var item in mediator.Stream(new NumberQuery(), cancelled.Token)) { }
                        }
                        catch (OperationCanceledException)
                        {
                            cancellationObserved = true;
                        }
                    }
                    return ordered && firstScope != secondScope && tokenForwarded && cancellationObserved;
                }
            }
            """);

        Assert.True(await MediumExecution.RunProgramAsync<bool>(MediumExecution.Emit(output)));
    }

    [Fact]
    public async Task StreamHandlers_HonorSingletonScopedAndTransientLifetimes()
    {
        var output = Generate("""
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class SingletonQuery : IStreamCommand<Guid> { }
            public sealed class ScopedQuery : IStreamCommand<Guid> { }
            public sealed class TransientQuery : IStreamCommand<Guid> { }
            [HandlerLifetime(ServiceLifetime.Singleton)]
            public sealed class SingletonHandler : IStreamCommandHandler<SingletonQuery, Guid>
            {
                private readonly Guid _id = Guid.NewGuid();
                public async IAsyncEnumerable<Guid> Handle(SingletonQuery command, CancellationToken cancellationToken)
                {
                    await Task.CompletedTask;
                    yield return _id;
                }
            }
            [HandlerLifetime(ServiceLifetime.Scoped)]
            public sealed class ScopedHandler : IStreamCommandHandler<ScopedQuery, Guid>
            {
                private readonly Guid _id = Guid.NewGuid();
                public async IAsyncEnumerable<Guid> Handle(ScopedQuery command, CancellationToken cancellationToken)
                {
                    await Task.CompletedTask;
                    yield return _id;
                }
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class TransientHandler : IStreamCommandHandler<TransientQuery, Guid>
            {
                private readonly Guid _id = Guid.NewGuid();
                public async IAsyncEnumerable<Guid> Handle(TransientQuery command, CancellationToken cancellationToken)
                {
                    await Task.CompletedTask;
                    yield return _id;
                }
            }
            public static class Program
            {
                private static async Task<Guid> Read(IMedium mediator, IStreamCommand<Guid> command)
                {
                    var result = Guid.Empty;
                    await foreach (var item in mediator.Stream(command))
                        result = item;
                    return result;
                }
                public static async Task<bool> Run()
                {
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    Guid singleton1;
                    Guid scoped1;
                    Guid transient1;
                    using (var scope = provider.CreateScope())
                    {
                        var mediator = scope.ServiceProvider.GetRequiredService<IMedium>();
                        singleton1 = await Read(mediator, new SingletonQuery());
                        scoped1 = await Read(mediator, new ScopedQuery());
                        transient1 = await Read(mediator, new TransientQuery());
                    }
                    using (var scope = provider.CreateScope())
                    {
                        var mediator = scope.ServiceProvider.GetRequiredService<IMedium>();
                        var singleton2 = await Read(mediator, new SingletonQuery());
                        var scoped2 = await Read(mediator, new ScopedQuery());
                        var transient2 = await Read(mediator, new TransientQuery());
                        return singleton1 == singleton2 && scoped1 != scoped2 && transient1 != transient2;
                    }
                }
            }
            """);

        Assert.True(await MediumExecution.RunProgramAsync<bool>(MediumExecution.Emit(output)));
    }

    [Fact]
    public async Task UnknownNullAndPublishedStreamCommands_AreRejected()
    {
        var output = Generate("""
            using System;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]
            public sealed class Unknown : IStreamCommand<int> { }
            public static class Program
            {
                public static Task<bool> Run()
                {
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMedium>();
                    try { _ = mediator.Stream<int>(null!); return Task.FromResult(false); }
                    catch (ArgumentNullException) { }
                    try { _ = mediator.Stream(new Unknown()); return Task.FromResult(false); }
                    catch (InvalidOperationException) { }
                    try { _ = mediator.Publish(new Unknown()); return Task.FromResult(false); }
                    catch (ArgumentException exception)
                    {
                        return Task.FromResult(exception.Message.Contains("use Stream instead", StringComparison.Ordinal));
                    }
                }
            }
            """);

        Assert.True(await MediumExecution.RunProgramAsync<bool>(MediumExecution.Emit(output)));
    }

    [Fact]
    public async Task MissingStreamRoute_ReportsCsm021WithHandlerFixProperties()
    {
        var compilation = MediumTestCompilation.Create("""
            using System.Collections.Generic;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Query : IStreamCommand<int> { }
            public static class Use
            {
                public static IAsyncEnumerable<int> Call(IStreamSender sender) => sender.Stream(new Query());
            }
            """);

        var diagnostic = Assert.Single(await MediumExecution.Analyze(compilation));
        Assert.Equal("CSM021", diagnostic.Id);
        Assert.Equal("3", diagnostic.Properties["Medium.RouteKind"]);
        Assert.Equal("True", diagnostic.Properties["Medium.HandlerFixEligible"]);
        Assert.Equal("int", diagnostic.Properties["Medium.ResponseGlobalName"]);
    }

    [Fact]
    public async Task DuplicateStreamHandlers_ReportCsm002AndOmitTheRoute()
    {
        var compilation = MediumTestCompilation.Create("""
            using System.Collections.Generic;
            using System.Threading;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Query : IStreamCommand<int> { }
            public sealed class First : IStreamCommandHandler<Query, int>
            {
                public async IAsyncEnumerable<int> Handle(Query command, CancellationToken cancellationToken)
                {
                    await System.Threading.Tasks.Task.CompletedTask;
                    yield return 1;
                }
            }
            public sealed class Second : IStreamCommandHandler<Query, int>
            {
                public async IAsyncEnumerable<int> Handle(Query command, CancellationToken cancellationToken)
                {
                    await System.Threading.Tasks.Task.CompletedTask;
                    yield return 2;
                }
            }
            """);

        var diagnostic = Assert.Single(await MediumExecution.Analyze(compilation));
        Assert.Equal("CSM002", diagnostic.Id);
        var output = MediumExecution.RunGenerator(compilation, out var generatorDiagnostics);
        Assert.Empty(generatorDiagnostics);
        var mediator = Assert.Single(
            MediumExecution.GeneratedSources(output),
            source => source.Contains("class GeneratedMedium"));
        Assert.DoesNotContain("typeof(global::Query)", mediator);
    }

    [Fact]
    public async Task CompositionRoot_ImportsStreamHandlersFromProducerManifest()
    {
        var producer = Generate("Sample.Application", [], """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class ImportedQuery : IStreamCommand<int> { }
            public sealed class ImportedHandler : IStreamCommandHandler<ImportedQuery, int>
            {
                public async IAsyncEnumerable<int> Handle(ImportedQuery command, System.Threading.CancellationToken cancellationToken)
                {
                    await Task.CompletedTask;
                    yield return 4;
                    yield return 2;
                }
            }
            """);
        var producerImage = MediumExecution.EmitImage(producer);
        var root = Generate(
            "Sample.WebApi",
            [MetadataReference.CreateFromImage(producerImage)],
            """
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public static class Program
            {
                public static async Task<int> Run()
                {
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IStreamSender>();
                    var sum = 0;
                    await foreach (var item in mediator.Stream(new ImportedQuery()))
                        sum += item;
                    return sum;
                }
            }
            """);

        Assert.Equal(6, await MediumExecution.RunProgramAsync<int>(
            MediumExecution.EmitImage(root),
            ("Sample.Application", producerImage)));
    }

    private static Compilation Generate(string source) => Generate("MediumTests", [], source);

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
