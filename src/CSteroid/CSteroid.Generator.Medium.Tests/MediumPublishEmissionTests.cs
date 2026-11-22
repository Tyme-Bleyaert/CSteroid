using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Medium.Tests;

public class MediumPublishEmissionTests
{
    [Fact]
    public async Task Publish_AwaitsExplicitAndImplicitHandlersSequentiallyInOrdinalOrder()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class Notice : IMessage { }
            public static class State { public static string Log = string.Empty; }
            public sealed class ZetaHandler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken)
                {
                    State.Log += "Z";
                    return default;
                }
            }
            public sealed class AlphaHandler : IMessageHandler<Notice>
            {
                async ValueTask IMessageHandler<Notice>.Handle(Notice message, CancellationToken cancellationToken)
                {
                    State.Log += "A-start;";
                    await Task.Yield();
                    State.Log += "A-end;";
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
                    await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new Notice());
                    return State.Log;
                }
            }
            """);

        Assert.Equal("A-start;A-end;Z", await MediumExecution.RunProgramAsync<string>(MediumExecution.Emit(output)));
    }

    [Fact]
    public async Task PublishFailure_PropagatesOriginalFaultAndSkipsLaterHandlers()
    {
        var output = Generate("""
            using System;
            using System.IO;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class Notice : IMessage { }
            public static class State { public static string Log = string.Empty; }
            public sealed class AlphaHandler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken)
                    => throw new InvalidDataException("stop");
            }
            public sealed class ZetaHandler : IMessageHandler<Notice>
            {
                public ValueTask Handle(Notice message, CancellationToken cancellationToken)
                {
                    State.Log += "unexpected";
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
                    try { await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(new Notice()); }
                    catch (Exception exception) { return exception.GetType().Name + ":" + exception.Message + ":" + State.Log; }
                    return "missing fault";
                }
            }
            """);

        Assert.Equal("InvalidDataException:stop:", await MediumExecution.RunProgramAsync<string>(MediumExecution.Emit(output)));
    }

    [Fact]
    public async Task UnknownEvent_Throws_AndNullOrCommandIsRejected()
    {
        var output = Generate("""
            using System;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class UnknownNotice : IMessage { }
            public sealed class Command : ICommand { }
            public static class Program
            {
                public static async Task<bool> Run()
                {
                    var services = new ServiceCollection();
                    services.AddMedium();
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                    try { await publisher.Publish(new UnknownNotice()); return false; }
                    catch (InvalidOperationException exception)
                    {
                        if (!exception.Message.Contains(nameof(UnknownNotice), StringComparison.Ordinal)) return false;
                    }
                    try { await publisher.Publish(new Command()); return false; }
                    catch (ArgumentException) { }
                    try { await publisher.Publish(null!); return false; }
                    catch (ArgumentNullException) { return true; }
                }
            }
            """);

        Assert.True(await MediumExecution.RunProgramAsync<bool>(MediumExecution.Emit(output)));
    }

    private static Compilation Generate(string source)
    {
        var output = MediumExecution.RunGenerator(MediumTestCompilation.Create(source), out var diagnostics);
        Assert.Empty(diagnostics);
        return output;
    }
}
