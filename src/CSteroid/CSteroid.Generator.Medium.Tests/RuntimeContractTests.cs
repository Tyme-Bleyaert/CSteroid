using System.Reflection;
using System.Runtime.Versioning;
using CSteroid.Medium;
using Microsoft.Extensions.DependencyInjection;

namespace CSteroid.Generator.Medium.Tests;

public class RuntimeContractTests
{
    [Fact]
    public void RuntimeTargetsNetStandard20_AndHasNoHostEraPublicContracts()
    {
        var runtime = typeof(IMessage).Assembly;
        var target = runtime.GetCustomAttribute<TargetFrameworkAttribute>();
        Assert.Equal(".NETStandard,Version=v2.0", target!.FrameworkName);

        string[] removedNames =
        [
            "IMediumService", "IMediumHandler", "MediumHostAttribute",
            "MediumHandlerAttribute", "HandlerAttribute", "MediumMemberAccessibility",
        ];
        Assert.DoesNotContain(runtime.GetTypes(), type => removedNames.Contains(type.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void ResponseCommands_AreInvariantAtRuntimeAndCompileTime()
    {
        var response = typeof(ICommand<>).GetGenericArguments()[0];
        Assert.Equal(GenericParameterAttributes.None, response.GenericParameterAttributes & GenericParameterAttributes.VarianceMask);

        var compilation = MediumTestCompilation.Create("""
            using CSteroid.Medium;
            public static class InvalidConversion
            {
                public static ICommand<object> Convert(ICommand<string> command) => command;
            }
            """);
        Assert.Contains(compilation.GetDiagnostics(), diagnostic => diagnostic.Id == "CS0266");
    }

    [Fact]
    public void IMedium_CombinesSenderAndPublisher()
    {
        Assert.True(typeof(ISender).IsAssignableFrom(typeof(IMedium)));
        Assert.True(typeof(IPublisher).IsAssignableFrom(typeof(IMedium)));
    }

    [Fact]
    public void ISender_HasResponseAndNonResponseValueTaskContracts()
    {
        var methods = typeof(ISender).GetMethods();
        Assert.Equal(2, methods.Length);

        var nonResponse = Assert.Single(methods, method => !method.IsGenericMethod);
        Assert.Equal(typeof(ValueTask), nonResponse.ReturnType);
        Assert.Equal(
            new[] { typeof(ICommand), typeof(CancellationToken) },
            nonResponse.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.True(nonResponse.GetParameters()[1].HasDefaultValue);

        var response = Assert.Single(methods, method => method.IsGenericMethod);
        Assert.Equal(typeof(ValueTask<>), response.ReturnType.GetGenericTypeDefinition());
        var parameters = response.GetParameters();
        Assert.Equal(typeof(ICommand<>), parameters[0].ParameterType.GetGenericTypeDefinition());
        Assert.Equal(response.GetGenericArguments()[0], parameters[0].ParameterType.GetGenericArguments()[0]);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue);
    }

    [Fact]
    public void IPublisher_HasValueTaskContract()
    {
        var publish = Assert.Single(typeof(IPublisher).GetMethods());
        Assert.Equal(typeof(ValueTask), publish.ReturnType);
        Assert.Equal(
            new[] { typeof(IMessage), typeof(CancellationToken) },
            publish.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.True(publish.GetParameters()[1].HasDefaultValue);
    }

    [Fact]
    public void PlainRecords_CannotBeSentOrPublished()
    {
        var compilation = MediumTestCompilation.Create("""
            using CSteroid.Medium;
            public sealed record Plain;
            public sealed class Consumer
            {
                public void Call(ISender sender, IPublisher publisher)
                {
                    _ = sender.Send(new Plain());
                    _ = publisher.Publish(new Plain());
                }
            }
            """);

        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToArray();
        Assert.Equal(2, errors.Length);
        Assert.All(errors, diagnostic => Assert.Equal("CS1503", diagnostic.Id));
    }

    [Fact]
    public void HandlerInterfaces_AreContravariantOnMessageAndInvariantOnResponse()
    {
        AssertContravariant(typeof(ICommandHandler<>));
        AssertContravariant(typeof(ICommandHandler<,>));
        AssertContravariant(typeof(IMessageHandler<>));
        Assert.Equal(
            GenericParameterAttributes.None,
            typeof(ICommandHandler<,>).GetGenericArguments()[1].GenericParameterAttributes & GenericParameterAttributes.VarianceMask);

        var commandHandle = typeof(ICommandHandler<>).GetMethod("Handle")!;
        Assert.Equal(typeof(ValueTask), commandHandle.ReturnType);
        Assert.Equal(typeof(CancellationToken), commandHandle.GetParameters()[1].ParameterType);

        var responseHandle = typeof(ICommandHandler<,>).GetMethod("Handle")!;
        Assert.Equal(typeof(ValueTask<>), responseHandle.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(CancellationToken), responseHandle.GetParameters()[1].ParameterType);

        var messageHandle = typeof(IMessageHandler<>).GetMethod("Handle")!;
        Assert.Equal(typeof(ValueTask), messageHandle.ReturnType);
        Assert.Equal(typeof(CancellationToken), messageHandle.GetParameters()[1].ParameterType);

        var compilation = MediumTestCompilation.Create("""
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            public sealed class ResponseCommand : ICommand<string> { }
            public sealed class Notice : IMessage { }
            public static class Variance
            {
                public static ICommandHandler<Command> Command(ICommandHandler<ICommand> handler) => handler;
                public static ICommandHandler<ResponseCommand, string> Response(
                    ICommandHandler<ICommand<string>, string> handler) => handler;
                public static IMessageHandler<Notice> Message(IMessageHandler<IMessage> handler) => handler;
            }
            """);
        MediumExecution.AssertCompiles(compilation);
    }

    [Fact]
    public void BehaviorInterfaces_AreInvariant_AndContinuationsAcceptMessageAndToken()
    {
        AssertInvariant(typeof(IPipelineBehavior<>), 0);
        AssertInvariant(typeof(IPipelineBehavior<,>), 0);
        AssertInvariant(typeof(IPipelineBehavior<,>), 1);

        AssertContravariant(typeof(MediumNext<>));
        AssertContravariant(typeof(MediumNext<,>));
        var next = typeof(MediumNext<>).GetMethod("Invoke")!;
        Assert.Equal(typeof(ValueTask), next.ReturnType);
        Assert.Equal(typeof(CancellationToken), next.GetParameters()[1].ParameterType);
        var responseNext = typeof(MediumNext<,>).GetMethod("Invoke")!;
        Assert.Equal(typeof(ValueTask<>), responseNext.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(CancellationToken), responseNext.GetParameters()[1].ParameterType);

        var handle = typeof(IPipelineBehavior<,>).GetMethod("Handle")!;
        var parameters = handle.GetParameters();
        Assert.Equal("message", parameters[0].Name);
        Assert.Equal("next", parameters[1].Name);
        Assert.Equal("cancellationToken", parameters[2].Name);
        Assert.Equal(typeof(MediumNext<,>), parameters[1].ParameterType.GetGenericTypeDefinition());
    }

    [Fact]
    public void InvariantBehaviorInterfaces_DoNotCompileWithVarianceConversions()
    {
        var compilation = MediumTestCompilation.Create("""
            using CSteroid.Medium;
            public sealed class Notice : IMessage { }
            public static class InvalidConversions
            {
                public static IPipelineBehavior<Notice> Message(IPipelineBehavior<IMessage> behavior) => behavior;
                public static IPipelineBehavior<Notice, int> Response(
                    IPipelineBehavior<IMessage, int> behavior) => behavior;
            }
            """);
        Assert.Equal(2, compilation.GetDiagnostics().Count(diagnostic => diagnostic.Id == "CS0266"));
    }

    [Fact]
    public void TypedContracts_CompileForHandlersAndInjectedConsumers()
    {
        var compilation = MediumTestCompilation.Create(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            public sealed class ResponseCommand : ICommand<int> { }
            public sealed class Command : ICommand { }
            public sealed class Notice : IMessage { }

            public sealed class TypedHandlers :
                ICommandHandler<ResponseCommand, int>,
                ICommandHandler<Command>,
                IMessageHandler<Notice>
            {
                public ValueTask<int> Handle(ResponseCommand command, CancellationToken cancellationToken)
                    => new(1);

                public ValueTask Handle(Command command, CancellationToken cancellationToken)
                    => default;

                ValueTask IMessageHandler<Notice>.Handle(Notice message, CancellationToken cancellationToken)
                    => default;
            }

            public sealed class Consumer
            {
                private readonly ISender _sender;
                private readonly IPublisher _publisher;

                public Consumer(IMedium mediator)
                {
                    _sender = mediator;
                    _publisher = mediator;
                }

                public ValueTask<int> Send(ResponseCommand command, CancellationToken cancellationToken)
                    => _sender.Send(command, cancellationToken);

                public ValueTask Publish(Notice message, CancellationToken cancellationToken)
                    => _publisher.Publish(message, cancellationToken);
            }
            """);

        MediumExecution.AssertCompiles(compilation);
    }

    [Fact]
    public void AttributeContracts_MatchCompositionManifestLifetimeAndBehaviorRoles()
    {
        var root = GetUsage<MediumCompositionRootAttribute>();
        Assert.Equal(AttributeTargets.Assembly, root.ValidOn);
        Assert.False(root.AllowMultiple);
        Assert.True(typeof(MediumCompositionRootAttribute).IsSealed);

        var manifest = new MediumManifestAttribute(1, typeof(RuntimeContractTests));
        Assert.Equal(1, manifest.Version);
        Assert.Equal(typeof(RuntimeContractTests), manifest.HandlerType);
        var manifestUsage = GetUsage<MediumManifestAttribute>();
        Assert.Equal(AttributeTargets.Assembly, manifestUsage.ValidOn);
        Assert.True(manifestUsage.AllowMultiple);
        Assert.True(typeof(MediumManifestAttribute).IsSealed);

        var lifetime = new HandlerLifetimeAttribute(ServiceLifetime.Singleton);
        Assert.Equal(ServiceLifetime.Singleton, lifetime.Lifetime);
        Assert.Equal(AttributeTargets.Class, GetUsage<HandlerLifetimeAttribute>().ValidOn);

        var behavior = new MediumBehaviorAttribute(typeof(RuntimeContractTests));
        Assert.Equal(typeof(RuntimeContractTests), behavior.BehaviorType);
        Assert.Equal(0, behavior.Order);
        Assert.Equal(ServiceLifetime.Scoped, behavior.Lifetime);
        Assert.True(GetUsage<MediumBehaviorAttribute>().AllowMultiple);
    }

    private static void AssertContravariant(Type type)
    {
        var parameter = type.GetGenericArguments()[0];
        Assert.True((parameter.GenericParameterAttributes & GenericParameterAttributes.Contravariant) != 0);
    }

    private static void AssertInvariant(Type type, int index)
    {
        var parameter = type.GetGenericArguments()[index];
        Assert.Equal(GenericParameterAttributes.None, parameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask);
    }

    private static AttributeUsageAttribute GetUsage<T>() => typeof(T).GetCustomAttribute<AttributeUsageAttribute>()!;
}
