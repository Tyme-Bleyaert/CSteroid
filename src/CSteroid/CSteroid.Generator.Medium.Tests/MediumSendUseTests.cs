using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static CSteroid.Generator.Medium.Tests.MediumExecution;

namespace CSteroid.Generator.Medium.Tests;

public class MediumSendUseTests
{
    [Fact]
    public async Task ConcreteSenderAndMediatorCalls_RecognizeBothSendFamilies()
    {
        var diagnostics = await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;

            public sealed class Command : ICommand { }
            public sealed class ResponseCommand : ICommand<string> { }
            public sealed class Handler : ICommandHandler<Command>, ICommandHandler<ResponseCommand, string>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
                public ValueTask<string> Handle(ResponseCommand command, CancellationToken cancellationToken) => new(string.Empty);
            }
            public sealed class Consumer
            {
                public void Call(ISender sender, IMedium mediator)
                {
                    _ = sender.Send(new Command());
                    _ = mediator.Send(new ResponseCommand());
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MissingConcreteRoute_Reportscsm001AtInvocation()
    {
        const string source = """
            using CSteroid.Medium;
            public sealed class Missing : ICommand { }
            public sealed class Consumer
            {
                public void Call(ISender sender) => _ = sender.Send(new Missing());
            }
            """;
        var compilation = MediumTestCompilation.Create(source);

        var diagnostic = Assert.Single(await Analyze(compilation));
        Assert.Equal("CSM001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        var invocation = compilation.SyntaxTrees.Single().GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>().Single();
        Assert.Equal(invocation.Span, diagnostic.Location.SourceSpan);
    }

    [Fact]
    public async Task CompilerConversionToCommandInterface_UsesConcreteOperand()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using CSteroid.Medium;
            public sealed class Missing : ICommand { }
            public sealed class Consumer
            {
                public void Call(IMedium mediator) => _ = mediator.Send((ICommand)new Missing());
            }
            """));

        Assert.Equal("CSM001", diagnostic.Id);
    }

    [Fact]
    public async Task InterfaceHeldAndTypeParameterCommands_AreNotGuessed()
    {
        var diagnostics = await Analyze("""
            using CSteroid.Medium;
            public sealed class Consumer
            {
                public void Call(ISender sender, ICommand command) => _ = sender.Send(command);
                public void Call<T>(ISender sender, T command) where T : ICommand => _ = sender.Send(command);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UnrelatedSameNamedSendMethod_IsNotAnalyzedAsMediatorUse()
    {
        var diagnostics = await Analyze("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class Missing : ICommand { }
            public sealed class UnrelatedSender
            {
                public ValueTask Send(ICommand command) => default;
            }
            public sealed class Consumer
            {
                public void Call(UnrelatedSender sender) => _ = sender.Send(new Missing());
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UnusedCommandDeclaration_DoesNotReportcsm001()
    {
        Assert.Empty(await Analyze("using CSteroid.Medium; public sealed class Unused : ICommand { }"));
    }

    [Fact]
    public async Task ErasedResponseCommand_HasNoNonResponseRoute()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class ResponseCommand : ICommand<int> { }
            public sealed class Handler : ICommandHandler<ResponseCommand, int>
            {
                public ValueTask<int> Handle(ResponseCommand command, CancellationToken cancellationToken) => new(1);
            }
            public sealed class Consumer
            {
                public void Call(ISender sender) => _ = sender.Send((ICommand)new ResponseCommand());
            }
            """));

        Assert.Equal("CSM001", diagnostic.Id);
    }

    [Fact]
    public async Task InvalidDeclarationSuppressesCascadecsm001()
    {
        var diagnostic = Assert.Single(await Analyze("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            public abstract class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            public sealed class Consumer
            {
                public void Call(ISender sender) => _ = sender.Send(new Command());
            }
            """));

        Assert.Equal("CSM011", diagnostic.Id);
    }

}
