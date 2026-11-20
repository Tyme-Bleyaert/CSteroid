using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.AutoMediator.Tests;

public class AutoMediatorDiEmissionTests
{
    [Fact]
    public async Task MediatorAliases_ShareOneScopedInstancePerScope()
    {
        var output = Generate("""
            using System;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public static class Program
            {
                public static Task<bool> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateScopes = true,
                        ValidateOnBuild = true,
                    });
                    using var first = provider.CreateScope();
                    var mediator = first.ServiceProvider.GetRequiredService<IAutoMediator>();
                    var sender = first.ServiceProvider.GetRequiredService<ISender>();
                    var publisher = first.ServiceProvider.GetRequiredService<IPublisher>();
                    var aliases = ReferenceEquals(mediator, sender) && ReferenceEquals(mediator, publisher);
                    using var second = provider.CreateScope();
                    var scoped = !ReferenceEquals(mediator, second.ServiceProvider.GetRequiredService<IAutoMediator>());
                    return Task.FromResult(aliases && scoped);
                }
            }
            """);

        Assert.True(await AutoMediatorExecution.RunProgramAsync<bool>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task CustomMediator_IsPreservedAndUsedByDefaultNarrowAliases()
    {
        var output = Generate("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class CustomMediator : IAutoMediator
            {
                public ValueTask Send(ICommand command, CancellationToken cancellationToken = default) => default;
                public ValueTask<T> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default) => new(default(T)!);
                public ValueTask Publish(IMessage message, CancellationToken cancellationToken = default) => default;
            }
            public static class Program
            {
                public static Task<bool> Run()
                {
                    var custom = new CustomMediator();
                    var services = new ServiceCollection();
                    services.AddSingleton<IAutoMediator>(custom);
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    return Task.FromResult(
                        ReferenceEquals(custom, scope.ServiceProvider.GetRequiredService<IAutoMediator>())
                        && ReferenceEquals(custom, scope.ServiceProvider.GetRequiredService<ISender>())
                        && ReferenceEquals(custom, scope.ServiceProvider.GetRequiredService<IPublisher>()));
                }
            }
            """);

        Assert.True(await AutoMediatorExecution.RunProgramAsync<bool>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task IndependentNarrowInterfaceOverride_DoesNotReplaceOtherAliases()
    {
        var output = Generate("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class CustomMediator : IAutoMediator
            {
                public ValueTask Send(ICommand command, CancellationToken cancellationToken = default) => default;
                public ValueTask<T> Send<T>(ICommand<T> command, CancellationToken cancellationToken = default) => new(default(T)!);
                public ValueTask Publish(IMessage message, CancellationToken cancellationToken = default) => default;
            }
            public static class Program
            {
                public static Task<bool> Run()
                {
                    var custom = new CustomMediator();
                    var services = new ServiceCollection();
                    services.AddSingleton<IPublisher>(custom);
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IAutoMediator>();
                    return Task.FromResult(
                        !ReferenceEquals(custom, mediator)
                        && ReferenceEquals(mediator, scope.ServiceProvider.GetRequiredService<ISender>())
                        && ReferenceEquals(custom, scope.ServiceProvider.GetRequiredService<IPublisher>()));
                }
            }
            """);

        Assert.True(await AutoMediatorExecution.RunProgramAsync<bool>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task ScopedSingletonAndTransientHandlers_UseContainerLifetimeSemantics()
    {
        var output = Generate("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class ScopedCommand : ICommand { }
            public sealed class ScopedNotice : IMessage { }
            public sealed class SingletonCommand : ICommand { }
            public sealed class TransientCommand : ICommand { }
            public static class State
            {
                public static Guid Scoped;
                public static Guid Singleton;
                public static Guid FirstTransient;
                public static Guid SecondTransient;
            }
            [HandlerLifetime(ServiceLifetime.Scoped)]
            public sealed class ScopedHandler : ICommandHandler<ScopedCommand>, IMessageHandler<ScopedNotice>
            {
                private readonly Guid _id = Guid.NewGuid();
                public ValueTask Handle(ScopedCommand command, CancellationToken cancellationToken)
                {
                    State.Scoped = _id;
                    return default;
                }
                public ValueTask Handle(ScopedNotice message, CancellationToken cancellationToken)
                {
                    if (State.Scoped != _id) throw new InvalidOperationException("Multi-route scoped handler was not shared.");
                    return default;
                }
            }
            [HandlerLifetime(ServiceLifetime.Singleton)]
            public sealed class SingletonHandler : ICommandHandler<SingletonCommand>
            {
                private readonly Guid _id = Guid.NewGuid();
                public ValueTask Handle(SingletonCommand command, CancellationToken cancellationToken)
                {
                    if (State.Singleton == Guid.Empty) State.Singleton = _id;
                    else if (State.Singleton != _id) throw new InvalidOperationException("Singleton changed.");
                    return default;
                }
            }
            [HandlerLifetime(ServiceLifetime.Transient)]
            public sealed class TransientHandler : ICommandHandler<TransientCommand>
            {
                private readonly Guid _id = Guid.NewGuid();
                public ValueTask Handle(TransientCommand command, CancellationToken cancellationToken)
                {
                    if (State.FirstTransient == Guid.Empty) State.FirstTransient = _id;
                    else State.SecondTransient = _id;
                    return default;
                }
            }
            public static class Program
            {
                public static async Task<bool> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    using (var scope = provider.CreateScope())
                    {
                        var mediator = scope.ServiceProvider.GetRequiredService<IAutoMediator>();
                        await mediator.Send(new ScopedCommand());
                        await mediator.Publish(new ScopedNotice());
                        await mediator.Send(new SingletonCommand());
                        await mediator.Send(new TransientCommand());
                        await mediator.Send(new TransientCommand());
                    }
                    var firstScoped = State.Scoped;
                    using (var scope = provider.CreateScope())
                    {
                        var mediator = scope.ServiceProvider.GetRequiredService<IAutoMediator>();
                        await mediator.Send(new ScopedCommand());
                        await mediator.Send(new SingletonCommand());
                    }
                    return firstScoped != State.Scoped && State.FirstTransient != State.SecondTransient;
                }
            }
            """);

        Assert.True(await AutoMediatorExecution.RunProgramAsync<bool>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task RepeatedRegistration_IsIdempotentAndPreservesCustomConcreteHandler()
    {
        var output = Generate("""
            using System.Linq;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class Command : ICommand<string> { }
            public sealed class Handler : ICommandHandler<Command, string>
            {
                private readonly string _response;
                public Handler() : this("generated") { }
                public Handler(string response) { _response = response; }
                public ValueTask<string> Handle(Command command, CancellationToken cancellationToken) => new(_response);
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddSingleton(new Handler("custom"));
                    services.AddAutoMediator();
                    services.AddAutoMediator();
                    if (services.Count(descriptor => descriptor.ServiceType == typeof(Handler)) != 1) return "duplicate";
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    using var scope = provider.CreateScope();
                    return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new Command());
                }
            }
            """);

        Assert.Equal("custom", await AutoMediatorExecution.RunProgramAsync<string>(AutoMediatorExecution.Emit(output)));
    }

    private static Compilation Generate(string source)
    {
        var output = AutoMediatorExecution.RunGenerator(AutoMediatorTestCompilation.Create(source), out var diagnostics);
        Assert.Empty(diagnostics);
        return output;
    }
}
