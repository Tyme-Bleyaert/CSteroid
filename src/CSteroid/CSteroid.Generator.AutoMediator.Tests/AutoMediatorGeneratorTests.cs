using CSteroid.AutoMediator;
using Microsoft.CodeAnalysis;
using static CSteroid.Generator.AutoMediator.Tests.AutoMediatorExecution;

namespace CSteroid.Generator.AutoMediator.Tests;

public class AutoMediatorGeneratorTests
{
    [Fact]
    public void EmptyUnmarkedCompilation_ProducesNothing()
    {
        var output = AutoMediatorExecution.RunGenerator(
            AutoMediatorTestCompilation.Create("public class Unrelated { }"),
            out var diagnostics);

        Assert.Empty(diagnostics);
        Assert.Empty(AutoMediatorExecution.GeneratedSources(output));
    }

    [Fact]
    public void MarkedRoot_DiscoversDirectInterfacesWithoutHandlerAttributes()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class Command : ICommand { }

            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """);

        var generated = string.Join("\n", AutoMediatorExecution.GeneratedSources(output));
        Assert.Contains("class GeneratedAutoMediator", generated);
        Assert.Contains("AddAutoMediator", generated);
        Assert.Contains("typeof(global::Command)", generated);
        Assert.Contains("AutoMediatorManifest(", generated);
    }

    [Fact]
    public void PartialMultiInterfaceHandler_EmitsRoutesOnceAndOneRegistrationAndManifest()
    {
        var output = Generate(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class ResponseCommand : ICommand<int> { }
            public sealed class Command : ICommand { }
            public sealed class Notice : IMessage { }

            public sealed partial class MultiHandler : ICommandHandler<ResponseCommand, int>
            {
                public ValueTask<int> Handle(ResponseCommand command, CancellationToken cancellationToken) => new(1);
            }
            """,
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            public sealed partial class MultiHandler : ICommandHandler<Command>, IMessageHandler<Notice>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
                ValueTask IMessageHandler<Notice>.Handle(Notice message, CancellationToken cancellationToken) => default;
            }
            """);

        var generated = string.Join("\n", AutoMediatorExecution.GeneratedSources(output));
        Assert.Equal(1, Count(generated, "AutoMediatorManifest("));
        Assert.Equal(1, Count(generated, "TryAddScoped<global::MultiHandler>(services)"));
        Assert.Equal(1, Count(generated, "typeof(global::ResponseCommand)"));
        Assert.Equal(1, Count(generated, "typeof(global::Command)"));
        Assert.Equal(1, Count(generated, "typeof(global::Notice)"));
    }

    [Fact]
    public void InheritedOnlyImplementation_DoesNotCreateAnotherRouteOrManifest()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public class Command : ICommand { }
            public class BaseHandler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            public sealed class DerivedHandler : BaseHandler { }
            """);

        var generated = string.Join("\n", AutoMediatorExecution.GeneratedSources(output));
        Assert.Equal(1, Count(generated, "AutoMediatorManifest("));
        Assert.Contains("global::BaseHandler", generated);
        Assert.DoesNotContain("DerivedHandler", generated);
    }

    [Fact]
    public void MethodConventionWithoutSupportedInterface_IsIgnored()
    {
        var output = Generate("""
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public sealed class Command : ICommand { }
            public sealed class ConventionalHandler
            {
                public void Handle(Command command) { }
            }
            """);

        var generated = string.Join("\n", AutoMediatorExecution.GeneratedSources(output));
        Assert.Contains("class GeneratedAutoMediator", generated);
        Assert.DoesNotContain("ConventionalHandler", generated);
        Assert.DoesNotContain("AutoMediatorManifest(", generated);
    }

    [Fact]
    public void InvalidRoute_IsExcludedWithoutSuppressingUnrelatedValidRoute()
    {
        var compilation = AutoMediatorTestCompilation.Create("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.AutoMediator;

            [assembly: AutoMediatorCompositionRoot]

            public abstract class InvalidCommand : ICommand { }
            public sealed class ValidCommand : ICommand { }
            public sealed class InvalidHandler : ICommandHandler<InvalidCommand>
            {
                public ValueTask Handle(InvalidCommand command, CancellationToken cancellationToken) => default;
            }
            public sealed class ValidHandler : ICommandHandler<ValidCommand>
            {
                public ValueTask Handle(ValidCommand command, CancellationToken cancellationToken) => default;
            }
            """);
        var output = AutoMediatorExecution.RunGenerator(compilation, out var diagnostics);
        var mediator = Assert.Single(
            AutoMediatorExecution.GeneratedSources(output),
            source => source.Contains("class GeneratedAutoMediator"));

        Assert.Empty(diagnostics);
        Assert.DoesNotContain("typeof(global::InvalidCommand)", mediator);
        Assert.Contains("typeof(global::ValidCommand)", mediator);
    }

    [Fact]
    public void GeneratedRootAndRegistration_AreAssemblyInternalAndExposeNoHosts()
    {
        var assembly = AutoMediatorExecution.Emit(Generate("""
            using CSteroid.AutoMediator;
            [assembly: AutoMediatorCompositionRoot]
            """));

        var mediator = Assert.Single(assembly.GetTypes(), type => type.Name == "GeneratedAutoMediator");
        Assert.True(mediator.IsNotPublic);
        Assert.True(typeof(IAutoMediator).IsAssignableFrom(mediator));

        var add = Assert.Single(
            assembly.GetTypes().SelectMany(type => type.GetMethods()),
            method => method.Name == "AddAutoMediator");
        Assert.True(add.DeclaringType!.IsNotPublic);
        Assert.DoesNotContain(assembly.GetTypes(), type => type.Name.Contains("Host", StringComparison.Ordinal));
    }

    private static Compilation Generate(params string[] sources)
    {
        var output = AutoMediatorExecution.RunGenerator(AutoMediatorTestCompilation.Create(sources), out var diagnostics);
        Assert.Empty(diagnostics);
        return output;
    }

}
