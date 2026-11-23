using CSteroid.Medium;
using Microsoft.CodeAnalysis;
using static CSteroid.Generator.Medium.Tests.MediumExecution;

namespace CSteroid.Generator.Medium.Tests;

public class MediumGeneratorTests
{
    [Fact]
    public void EmptyUnmarkedCompilation_ProducesNothing()
    {
        var output = MediumExecution.RunGenerator(
            MediumTestCompilation.Create("public class Unrelated { }"),
            out var diagnostics);

        Assert.Empty(diagnostics);
        Assert.Empty(MediumExecution.GeneratedSources(output));
    }

    [Fact]
    public void MarkedRoot_DiscoversDirectInterfacesWithoutHandlerAttributes()
    {
        var output = Generate("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class Command : ICommand { }

            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            """);

        var generated = string.Join("\n", MediumExecution.GeneratedSources(output));
        Assert.Contains("class GeneratedMedium", generated);
        Assert.Contains("AddMedium", generated);
        Assert.Contains("typeof(global::Command)", generated);
        Assert.Contains("MediumManifest(", generated);
    }

    [Fact]
    public void PartialMultiInterfaceHandler_EmitsRoutesOnceAndOneRegistrationAndManifest()
    {
        var output = Generate(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

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
            using CSteroid.Medium;

            public sealed partial class MultiHandler : ICommandHandler<Command>, IMessageHandler<Notice>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
                ValueTask IMessageHandler<Notice>.Handle(Notice message, CancellationToken cancellationToken) => default;
            }
            """);

        var generated = string.Join("\n", MediumExecution.GeneratedSources(output));
        Assert.Equal(1, Count(generated, "MediumManifest("));
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
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public class Command : ICommand { }
            public class BaseHandler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            public sealed class DerivedHandler : BaseHandler { }
            """);

        var generated = string.Join("\n", MediumExecution.GeneratedSources(output));
        Assert.Equal(1, Count(generated, "MediumManifest("));
        Assert.Contains("global::BaseHandler", generated);
        Assert.DoesNotContain("DerivedHandler", generated);
    }

    [Fact]
    public void MethodConventionWithoutSupportedInterface_IsIgnored()
    {
        var output = Generate("""
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

            public sealed class Command : ICommand { }
            public sealed class ConventionalHandler
            {
                public void Handle(Command command) { }
            }
            """);

        var generated = string.Join("\n", MediumExecution.GeneratedSources(output));
        Assert.Contains("class GeneratedMedium", generated);
        Assert.DoesNotContain("ConventionalHandler", generated);
        Assert.DoesNotContain("MediumManifest(", generated);
    }

    [Fact]
    public void InvalidRoute_IsExcludedWithoutSuppressingUnrelatedValidRoute()
    {
        var compilation = MediumTestCompilation.Create("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            [assembly: MediumCompositionRoot]

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
        var output = MediumExecution.RunGenerator(compilation, out var diagnostics);
        var mediator = Assert.Single(
            MediumExecution.GeneratedSources(output),
            source => source.Contains("class GeneratedMedium"));

        Assert.Empty(diagnostics);
        Assert.DoesNotContain("typeof(global::InvalidCommand)", mediator);
        Assert.Contains("typeof(global::ValidCommand)", mediator);
    }

    [Fact]
    public void GeneratedRootAndRegistration_AreAssemblyInternalAndExposeNoHosts()
    {
        var assembly = MediumExecution.Emit(Generate("""
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            """));

        var mediator = Assert.Single(assembly.GetTypes(), type => type.Name == "GeneratedMedium");
        Assert.True(mediator.IsNotPublic);
        Assert.True(typeof(IMedium).IsAssignableFrom(mediator));

        var add = Assert.Single(
            assembly.GetTypes().SelectMany(type => type.GetMethods()),
            method => method.Name == "AddMedium");
        Assert.True(add.DeclaringType!.IsNotPublic);
        Assert.DoesNotContain(assembly.GetTypes(), type => type.Name.Contains("Host", StringComparison.Ordinal));
    }

    private static Compilation Generate(params string[] sources)
    {
        var output = MediumExecution.RunGenerator(MediumTestCompilation.Create(sources), out var diagnostics);
        Assert.Empty(diagnostics);
        return output;
    }

}
