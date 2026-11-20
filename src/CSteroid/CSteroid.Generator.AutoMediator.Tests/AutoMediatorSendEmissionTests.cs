using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.AutoMediator.Tests;

public class AutoMediatorSendEmissionTests
{
    [Fact]
    public async Task ResponseSend_InvokesExplicitTypedHandlerThroughRealScope()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class Command : ICommand<string> { public string Value { get; set; } = string.Empty; }
            public sealed class Handler : ICommandHandler<Command, string>
            {
                ValueTask<string> ICommandHandler<Command, string>.Handle(Command command, CancellationToken cancellationToken)
                    => new("handled:" + command.Value);
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions
                    {
                        ValidateScopes = true,
                        ValidateOnBuild = true,
                    });
                    using var scope = provider.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                    return await sender.Send(new Command { Value = "ok" });
                }
            }
            """);

        var generated = string.Join("\n", AutoMediatorExecution.GeneratedSources(output));
        Assert.Contains("Dictionary<global::System.Type, global::System.Delegate>", generated);
        Assert.Contains("ICommandHandler<global::Command, string>", generated);
        Assert.Equal("handled:ok", await AutoMediatorExecution.RunProgramAsync<string>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task StructResponseAndNonResponseCommands_ReturnTypedValueTasks()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public struct ResponseCommand : ICommand<int> { }
            public sealed class Command : ICommand { }
            public static class State { public static int Calls; }
            public sealed class Handler : ICommandHandler<ResponseCommand, int>, ICommandHandler<Command>
            {
                public async ValueTask<int> Handle(ResponseCommand command, CancellationToken cancellationToken)
                {
                    await Task.Yield();
                    return 42;
                }
                public ValueTask Handle(Command command, CancellationToken cancellationToken)
                {
                    State.Calls++;
                    return default;
                }
            }
            public static class Program
            {
                public static async Task<int> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IAutoMediator>();
                    var response = await mediator.Send(new ResponseCommand());
                    await mediator.Send(new Command());
                    return response + State.Calls;
                }
            }
            """);

        Assert.Equal(43, await AutoMediatorExecution.RunProgramAsync<int>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task DerivedCommand_ThrowsWithoutAssignableFallback()
    {
        var output = Generate("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public class BaseCommand : ICommand { }
            public sealed class DerivedCommand : BaseCommand { }
            public sealed class Handler : ICommandHandler<BaseCommand>
            {
                public ValueTask Handle(BaseCommand command, CancellationToken cancellationToken) => default;
            }
            public static class Program
            {
                public static async Task<string> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                    try { await sender.Send(new DerivedCommand()); return "unexpected"; }
                    catch (InvalidOperationException exception) { return exception.Message; }
                }
            }
            """);

        var result = await AutoMediatorExecution.RunProgramAsync<string>(AutoMediatorExecution.Emit(output));
        Assert.Contains("No exact", result);
        Assert.Contains("DerivedCommand", result);
    }

    [Fact]
    public async Task ErasedResponseCommand_IsRejectedInsteadOfDiscardingResult()
    {
        var output = Generate("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class ResponseCommand : ICommand<int> { }
            public sealed class Handler : ICommandHandler<ResponseCommand, int>
            {
                public ValueTask<int> Handle(ResponseCommand command, CancellationToken cancellationToken) => new(42);
            }
            public static class Program
            {
                public static async Task<bool> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                    try { await sender.Send((ICommand)new ResponseCommand()); return false; }
                    catch (InvalidOperationException) { return true; }
                }
            }
            """);

        Assert.True(await AutoMediatorExecution.RunProgramAsync<bool>(AutoMediatorExecution.Emit(output)));
    }

    [Fact]
    public async Task NullCommands_ThrowArgumentNullException()
    {
        var output = Generate("""
            using System;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public static class Program
            {
                public static async Task<bool> Run()
                {
                    var services = new ServiceCollection();
                    services.AddAutoMediator();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                    try { await sender.Send((ICommand)null!); return false; }
                    catch (ArgumentNullException) { }
                    try { await sender.Send<string>((ICommand<string>)null!); return false; }
                    catch (ArgumentNullException) { return true; }
                }
            }
            """);

        Assert.True(await AutoMediatorExecution.RunProgramAsync<bool>(AutoMediatorExecution.Emit(output)));
    }

    private static Compilation Generate(string source)
    {
        var output = AutoMediatorExecution.RunGenerator(AutoMediatorTestCompilation.Create(source), out var diagnostics);
        Assert.Empty(diagnostics);
        return output;
    }
}
