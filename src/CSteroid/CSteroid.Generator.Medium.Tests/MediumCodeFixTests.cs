using System.Collections.Immutable;
using CSteroid.Generator.Medium.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSteroid.Generator.Medium.Tests;

public class MediumCodeFixTests
{
    [Fact]
    public async Task MissingRootFix_AddsPersistentAttribute_AndClearsOrdinalFirstDiagnostic()
    {
        const string source = """
            using Microsoft.Extensions.DependencyInjection;
            public static class Use
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddMedium();
                    services.AddMedium();
                }
            }
            """;
        using var fixture = new Fixture(source);
        var initial = await fixture.AnalyzeAsync();
        var diagnostic = Assert.Single(initial.Diagnostics);
        Assert.Equal("CSM019", diagnostic.Id);
        Assert.Equal(source.IndexOf("services.AddMedium()", StringComparison.Ordinal), diagnostic.Location.SourceSpan.Start);

        var action = Assert.Single(await fixture.GetActionsAsync(diagnostic, new MediumAssemblyAttributeCodeFixProvider()));
        await fixture.ApplyAsync(action);

        var output = await fixture.GenerateAndAssertCompilesAsync();
        var after = await MediumExecution.Analyze(output);
        Assert.DoesNotContain(after, item => item.Id == "CSM019");
        Assert.Contains(
            "[assembly: global::CSteroid.Medium.MediumCompositionRoot]",
            await fixture.PersistentTextAsync());
    }

    [Fact]
    public async Task MissingRoot_SuppressesHandlerFix_UntilRootIsAdded()
    {
        using var fixture = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            using Microsoft.Extensions.DependencyInjection;
            public sealed class Command : ICommand { }
            public static class Use
            {
                public static void Register(IServiceCollection services) { services.AddMedium(); }
                public static ValueTask Call(ISender sender) => sender.Send(new Command());
            }
            """);
        var initial = await fixture.AnalyzeAsync();
        var missingRoute = Assert.Single(initial.Diagnostics, item => item.Id == "CSM001");
        Assert.Empty(await fixture.GetActionsAsync(missingRoute, new MediumHandlerCodeFixProvider()));
        var rootAction = Assert.Single(await fixture.GetActionsAsync(
            Assert.Single(initial.Diagnostics, item => item.Id == "CSM019"),
            new MediumAssemblyAttributeCodeFixProvider()));
        await fixture.ApplyAsync(rootAction);

        var rooted = await fixture.AnalyzeAsync();
        missingRoute = Assert.Single(rooted.Diagnostics, item => item.Id == "CSM001");
        var handlerAction = Assert.Single(await fixture.GetActionsAsync(missingRoute, new MediumHandlerCodeFixProvider()));
        await fixture.ApplyAsync(handlerAction);

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id is "CSM001" or "CSM019");
    }

    [Fact]
    public async Task CommandHandlerFix_AddsCompilingSiblingDocument_ForInternalMessage()
    {
        using var fixture = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            namespace Sample
            {
                internal sealed class Command : ICommand { }
                public static class Use
                {
                    public static ValueTask Call(ISender sender) => sender.Send(new Command());
                }
            }
            """);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM001");
        var action = Assert.Single(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider()));
        await fixture.ApplyAsync(action);

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id == "CSM001");
        var source = await fixture.PersistentTextAsync();
        Assert.Contains("internal sealed class MediumSample_CommandHandler", source);
        Assert.Contains("global::CSteroid.Medium.ICommandHandler<global::Sample.Command>", source);
        Assert.Contains(
            "TryAddScoped<global::Sample.MediumSample_CommandHandler>(services)",
            string.Join("\n", MediumExecution.GeneratedSources(output)));
    }

    [Fact]
    public async Task CommandHandlerFix_EscapesKeywordNamespace()
    {
        using var fixture = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            namespace Sample.@event
            {
                public sealed class Command : ICommand { }
                public static class Use
                {
                    public static ValueTask Call(ISender sender) => sender.Send(new Command());
                }
            }
            """);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM001");
        await fixture.ApplyAsync(Assert.Single(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider())));

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id == "CSM001");
        Assert.Contains("namespace Sample.@event", await fixture.PersistentTextAsync());
    }

    [Fact]
    public async Task ResponseCommandHandlerFix_UsesExistingResponseContract()
    {
        using var fixture = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Query : ICommand<string> { }
            public static class Use
            {
                public static ValueTask<string> Call(ISender sender) => sender.Send(new Query());
            }
            """);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM001");
        await fixture.ApplyAsync(Assert.Single(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider())));

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id == "CSM001");
        var source = await fixture.PersistentTextAsync();
        Assert.Contains("global::CSteroid.Medium.ICommandHandler<global::Query, string>", source);
        Assert.Contains("global::System.Threading.Tasks.ValueTask<string> Handle(", source);
    }

    [Fact]
    public async Task StreamHandlerFix_AddsCompilingAsyncIterator()
    {
        using var fixture = new Fixture("""
            using System.Collections.Generic;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Query : IStreamCommand<int> { }
            public static class Use
            {
                public static IAsyncEnumerable<int> Call(IStreamSender sender) => sender.Stream(new Query());
            }
            """);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM021");
        await fixture.ApplyAsync(Assert.Single(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider())));

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id == "CSM021");
        var source = await fixture.PersistentTextAsync();
        Assert.Contains("global::CSteroid.Medium.IStreamCommandHandler<global::Query, int>", source);
        Assert.Contains("async global::System.Collections.Generic.IAsyncEnumerable<int> Handle(", source);
        Assert.Contains("yield break;", source);
    }

    [Fact]
    public async Task StaleStreamCommandContract_DoesNotOfferHandlerFix()
    {
        const string definition = """
            using CSteroid.Medium;
            public sealed class Query : IStreamCommand<int> { }
            """;
        const string usage = """
            using System.Collections.Generic;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public static class Use
            {
                public static IAsyncEnumerable<int> Call(IStreamSender sender) => sender.Stream(new Query());
            }
            """;
        using var fixture = new Fixture(definition, usage);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM021");
        Assert.Single(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider()));

        await fixture.ReplaceDocumentTextAsync("0.cs", definition.Replace(
            "IStreamCommand<int>",
            "IStreamCommand<string>",
            StringComparison.Ordinal));

        Assert.Empty(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider()));
        Assert.Equal(2, fixture.Project.Documents.Count());
    }

    [Fact]
    public async Task StaleResponseCommandContract_DoesNotOfferHandlerFix()
    {
        const string definition = """
            using CSteroid.Medium;
            public sealed class Query : ICommand<string> { }
            """;
        const string usage = """
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public static class Use
            {
                public static ValueTask<string> Call(ISender sender) => sender.Send(new Query());
            }
            """;
        using var fixture = new Fixture(definition, usage);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM001");
        Assert.Single(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider()));

        await fixture.ReplaceDocumentTextAsync("0.cs", definition.Replace(
            "ICommand<string>",
            "ICommand<int>",
            StringComparison.Ordinal));

        Assert.Empty(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider()));
        Assert.Equal(2, fixture.Project.Documents.Count());
    }

    [Fact]
    public async Task HandlerlessMessagePublish_RequiresNoHandlerFix()
    {
        using var fixture = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            namespace Events
            {
                public sealed class Notice : IMessage { }
                public static class Use
                {
                    public static ValueTask Call(IPublisher publisher) => publisher.Publish(new Notice());
                }
            }
            """);

        Assert.DoesNotContain((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM018");
        await fixture.GenerateAndAssertCompilesAsync();
    }

    [Fact]
    public async Task CommandPassedToPublish_DoesNotOfferMessageHandlerFix()
    {
        using var fixture = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Command : ICommand { }
            public static class Use
            {
                public static ValueTask Call(IPublisher publisher) => publisher.Publish(new Command());
            }
            """);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM018");

        Assert.Empty(await fixture.GetActionsAsync(diagnostic, new MediumHandlerCodeFixProvider()));
    }

    [Fact]
    public async Task ClosedBehaviorFix_AddsAttribute_AndProducesScopedRegistration()
    {
        using var fixture = new Fixture(BehaviorSource("Behavior"));
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM020");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        await fixture.ApplyAsync(Assert.Single(await fixture.GetActionsAsync(
            diagnostic,
            new MediumAssemblyAttributeCodeFixProvider())));

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id == "CSM020");
        Assert.Contains(
            "[assembly: global::CSteroid.Medium.MediumBehavior(typeof(global::Behavior))]",
            await fixture.PersistentTextAsync());
        Assert.Contains(
            "TryAddScoped<global::Behavior>(services)",
            string.Join("\n", MediumExecution.GeneratedSources(output)));
    }

    [Fact]
    public async Task OpenBehaviorFix_UsesOpenTypeOf_AndPersistsThroughRegeneration()
    {
        using var fixture = new Fixture("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Command : ICommand<int> { }
            public sealed class LoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
                where TMessage : ICommand<TResponse>
            {
                public ValueTask<TResponse> Handle(
                    TMessage message,
                    MediumNext<TMessage, TResponse> next,
                    CancellationToken cancellationToken) => next(message, cancellationToken);
            }
            public sealed class Handler : ICommandHandler<Command, int>
            {
                public ValueTask<int> Handle(Command command, CancellationToken cancellationToken) => new(0);
            }
            """);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM020");
        await fixture.ApplyAsync(Assert.Single(await fixture.GetActionsAsync(
            diagnostic,
            new MediumAssemblyAttributeCodeFixProvider())));

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id == "CSM020");
        Assert.Contains(
            "[assembly: global::CSteroid.Medium.MediumBehavior(typeof(global::LoggingBehavior<,>))]",
            await fixture.PersistentTextAsync());
        Assert.Contains(
            "TryAddScoped<global::LoggingBehavior<global::Command, int>>(services)",
            string.Join("\n", MediumExecution.GeneratedSources(output)));
    }

    [Fact]
    public async Task OpenStreamBehaviorFix_AddsDeclarationAndClosedRegistration()
    {
        using var fixture = new Fixture("""
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Query : IStreamCommand<int> { }
            public sealed class LoggingBehavior<TMessage, T> : IStreamPipelineBehavior<TMessage, T>
                where TMessage : IStreamCommand<T>
            {
                public async IAsyncEnumerable<T> Handle(
                    TMessage message,
                    MediumStreamNext<TMessage, T> next,
                    [EnumeratorCancellation] CancellationToken cancellationToken)
                {
                    await foreach (var item in next(message, cancellationToken).WithCancellation(cancellationToken))
                        yield return item;
                }
            }
            public sealed class Handler : IStreamCommandHandler<Query, int>
            {
                public async IAsyncEnumerable<int> Handle(Query command, CancellationToken cancellationToken)
                {
                    await Task.CompletedTask;
                    yield return 0;
                }
            }
            """);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM020");
        await fixture.ApplyAsync(Assert.Single(await fixture.GetActionsAsync(
            diagnostic,
            new MediumAssemblyAttributeCodeFixProvider())));

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id == "CSM020");
        Assert.Contains(
            "[assembly: global::CSteroid.Medium.MediumBehavior(typeof(global::LoggingBehavior<,>))]",
            await fixture.PersistentTextAsync());
        Assert.Contains(
            "TryAddScoped<global::LoggingBehavior<global::Query, int>>(services)",
            string.Join("\n", MediumExecution.GeneratedSources(output)));
    }

    [Fact]
    public async Task NestedOpenBehaviorFix_UsesDefinitionNamesAndArity()
    {
        using var fixture = new Fixture("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Command : ICommand<int> { }
            namespace Sample
            {
                public static class BehaviorContainer
                {
                    public sealed class LoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
                        where TMessage : ICommand<TResponse>
                    {
                        public ValueTask<TResponse> Handle(
                            TMessage message,
                            MediumNext<TMessage, TResponse> next,
                            CancellationToken cancellationToken) => next(message, cancellationToken);
                    }
                }
            }
            public sealed class Handler : ICommandHandler<Command, int>
            {
                public ValueTask<int> Handle(Command command, CancellationToken cancellationToken) => new(0);
            }
            """);
        var diagnostic = Assert.Single((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM020");
        await fixture.ApplyAsync(Assert.Single(await fixture.GetActionsAsync(
            diagnostic,
            new MediumAssemblyAttributeCodeFixProvider())));

        var output = await fixture.GenerateAndAssertCompilesAsync();
        Assert.DoesNotContain(await MediumExecution.Analyze(output), item => item.Id == "CSM020");
        Assert.Contains(
            "[assembly: global::CSteroid.Medium.MediumBehavior(typeof(global::Sample.BehaviorContainer.LoggingBehavior<,>))]",
            await fixture.PersistentTextAsync());
        Assert.Contains(
            "TryAddScoped<global::Sample.BehaviorContainer.LoggingBehavior<global::Command, int>>(services)",
            string.Join("\n", MediumExecution.GeneratedSources(output)));
    }

    [Fact]
    public async Task ExistingRootAndBehaviorDeclaration_DoNotOfferDuplicateFixes()
    {
        using var fixture = new Fixture("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            using Microsoft.Extensions.DependencyInjection;
            [assembly: MediumCompositionRoot]
            [assembly: MediumBehavior(typeof(Behavior))]
            public sealed class Command : ICommand { }
            public sealed class Behavior : IPipelineBehavior<Command>
            {
                public ValueTask Handle(Command message, MediumNext<Command> next, CancellationToken cancellationToken)
                    => next(message, cancellationToken);
            }
            public sealed class Handler : ICommandHandler<Command>
            {
                public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
            }
            public static class Use
            {
                public static void Register(IServiceCollection services) { services.AddMedium(); }
            }
            """);
        var output = await fixture.GenerateAndAssertCompilesAsync();
        var diagnostics = await MediumExecution.Analyze(output);
        Assert.DoesNotContain(diagnostics, item => item.Id is "CSM019" or "CSM020");
    }

    [Fact]
    public async Task UserAndUnrelatedAddMediumMethods_AreIgnored()
    {
        using var userExtension = new Fixture("""
            using Microsoft.Extensions.DependencyInjection;
            public static class UserExtensions
            {
                public static IServiceCollection AddMedium(this IServiceCollection services) => services;
            }
            public static class Use
            {
                public static void Register(IServiceCollection services) { services.AddMedium(); }
            }
            """);
        Assert.DoesNotContain((await userExtension.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM019");

        using var unresolvedUserExtension = new Fixture("""
            using Microsoft.Extensions.DependencyInjection;
            public static class UserExtensions
            {
                public static void AddMedium(this IServiceCollection services, int value) { }
            }
            public static class Use
            {
                public static void Register(IServiceCollection services) { services.AddMedium(); }
            }
            """);
        Assert.DoesNotContain((await unresolvedUserExtension.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM019");

        using var unrelated = new Fixture("""
            public sealed class Registry { public void AddMedium() { } }
            public static class Use { public static void Register(Registry registry) { registry.AddMedium(); } }
            """);
        Assert.DoesNotContain((await unrelated.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM019");
    }

    [Fact]
    public async Task UnmarkedRoot_DoesNotReportSourceBehavior()
    {
        using var fixture = new Fixture(BehaviorSource("Behavior").Replace(
            "[assembly: MediumCompositionRoot]",
            string.Empty,
            StringComparison.Ordinal));
        Assert.DoesNotContain((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM020");
    }

    [Fact]
    public async Task ProducerBehavior_DoesNotReportcsm020InConsumerRoot()
    {
        var producer = MediumTestCompilation.Create("Sample.Application", [], """
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            public sealed class Notice : IMessage { }
            public sealed class ProducerBehavior : IPipelineBehavior<Notice>
            {
                public ValueTask Handle(Notice message, MediumNext<Notice> next, CancellationToken cancellationToken)
                    => next(message, cancellationToken);
            }
            """);
        using var fixture = new Fixture(MediumTestCompilation.Create(
            "Sample.WebApi",
            [MediumExecution.EmitMetadataReference(producer)],
            "using CSteroid.Medium; [assembly: MediumCompositionRoot]"));
        Assert.DoesNotContain((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM020");
    }

    [Fact]
    public async Task StaleDiagnostic_AndDuplicateHandlerFix_DoNotAddDocuments()
    {
        const string source = """
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Command : ICommand { }
            public static class Use
            {
                public static ValueTask First(ISender sender) => sender.Send(new Command());
                public static ValueTask Second(ISender sender) => sender.Send(new Command());
            }
            """;
        using var fixture = new Fixture(source);
        var diagnostics = (await fixture.AnalyzeAsync()).Diagnostics.Where(item => item.Id == "CSM001").ToArray();
        Assert.Equal(2, diagnostics.Length);
        await fixture.ApplyAsync(Assert.Single(await fixture.GetActionsAsync(
            diagnostics[0],
            new MediumHandlerCodeFixProvider())));

        Assert.Empty(await fixture.GetActionsAsync(diagnostics[1], new MediumHandlerCodeFixProvider()));
        Assert.Single(fixture.Project.Documents, document => document.Name.StartsWith("Medium", StringComparison.Ordinal));
        Assert.DoesNotContain((await fixture.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM001");

        var stale = diagnostics[1];
        await fixture.ReplaceDocumentTextAsync(stale.Location.SourceTree!.FilePath, source.Replace(
            "public static class Use",
            "public sealed class OtherHandler { }\npublic static class Use",
            StringComparison.Ordinal));
        Assert.Empty(await fixture.GetActionsAsync(stale, new MediumHandlerCodeFixProvider()));
    }

    [Fact]
    public async Task InaccessibleMessage_AndInvalidHandlerShape_DoNotOfferHandlerFixes()
    {
        using var inaccessible = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public static class Container
            {
                private sealed class Command : ICommand { }
                public static ValueTask Call(ISender sender) => sender.Send(new Command());
            }
            """);
        var inaccessibleDiagnostic = Assert.Single((await inaccessible.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM001");
        Assert.Empty(await inaccessible.GetActionsAsync(inaccessibleDiagnostic, new MediumHandlerCodeFixProvider()));

        using var inaccessibleResponse = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public static class Container
            {
                private sealed class Response { }
                internal sealed class Command : ICommand<Response> { }
                private static ValueTask<Response> Call(ISender sender) => sender.Send(new Command());
            }
            """);
        var responseDiagnostic = Assert.Single(
            (await inaccessibleResponse.AnalyzeAsync()).Diagnostics,
            item => item.Id == "CSM001");
        Assert.Empty(await inaccessibleResponse.GetActionsAsync(responseDiagnostic, new MediumHandlerCodeFixProvider()));

        using var fileLocal = new Fixture("""
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            file sealed class Command : ICommand { }
            public static class Use
            {
                public static ValueTask Call(ISender sender) => sender.Send(new Command());
            }
            """);
        var fileLocalDiagnostic = Assert.Single((await fileLocal.AnalyzeAsync()).Diagnostics, item => item.Id == "CSM001");
        Assert.Empty(await fileLocal.GetActionsAsync(fileLocalDiagnostic, new MediumHandlerCodeFixProvider()));

        using var invalid = new Fixture("""
            using System.Threading;
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public sealed class Command : ICommand { }
            public abstract class Handler : ICommandHandler<Command>
            {
                public abstract ValueTask Handle(Command command, CancellationToken cancellationToken);
            }
            public static class Use
            {
                public static ValueTask Call(ISender sender) => sender.Send(new Command());
            }
            """);
        var diagnostics = (await invalid.AnalyzeAsync()).Diagnostics;
        Assert.Contains(diagnostics, item => item.Id == "CSM011");
        Assert.DoesNotContain(diagnostics, item => item.Id == "CSM001");
    }

    [Fact]
    public async Task ExternalCommandWithoutHandler_IncludesProducerManifestGuidance()
    {
        var producer = MediumTestCompilation.Create("Sample.Application", [], """
            using CSteroid.Medium;
            public sealed class Command : ICommand { }
            public sealed class Notice : IMessage { }
            """);
        using var fixture = new Fixture(MediumTestCompilation.Create(
            "Sample.WebApi",
            [MediumExecution.EmitMetadataReference(producer)],
            """
            using System.Threading.Tasks;
            using CSteroid.Medium;
            [assembly: MediumCompositionRoot]
            public static class Use
            {
                public static ValueTask Send(ISender sender) => sender.Send(new Command());
                public static ValueTask Publish(IPublisher publisher) => publisher.Publish(new Notice());
            }
            """));
        var diagnostics = (await fixture.AnalyzeAsync()).Diagnostics;
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "CSM001");
        Assert.DoesNotContain(diagnostics, item => item.Id == "CSM018");
        Assert.Contains("Sample.Application", diagnostic.GetMessage());
        Assert.Contains("references CSteroid.Medium", diagnostic.GetMessage());
        Assert.Contains("Medium.Manifest.g.cs", diagnostic.GetMessage());
    }

    private static string BehaviorSource(string behaviorName) => $$"""
        using System.Threading;
        using System.Threading.Tasks;
        using CSteroid.Medium;
        [assembly: MediumCompositionRoot]
        public sealed class Command : ICommand { }
        public sealed class {{behaviorName}} : IPipelineBehavior<Command>
        {
            public ValueTask Handle(Command message, MediumNext<Command> next, CancellationToken cancellationToken)
                => next(message, cancellationToken);
        }
        public sealed class Handler : ICommandHandler<Command>
        {
            public ValueTask Handle(Command command, CancellationToken cancellationToken) => default;
        }
        """;

    private sealed class Fixture : IDisposable
    {
        private readonly ProjectId _projectId;

        internal Fixture(params string[] sources)
            : this(MediumTestCompilation.Create(sources))
        {
        }

        internal Fixture(Compilation compilation)
        {
            Workspace = new AdhocWorkspace();
            _projectId = ProjectId.CreateNewId();
            var documents = compilation.SyntaxTrees.Select(tree => DocumentInfo.Create(
                DocumentId.CreateNewId(_projectId),
                Path.GetFileName(tree.FilePath),
                loader: TextLoader.From(TextAndVersion.Create(
                                    SourceText.From(tree.ToString()),
                                    VersionStamp.Create())),
                filePath: tree.FilePath));
            var project = ProjectInfo.Create(
                _projectId,
                VersionStamp.Create(),
                compilation.AssemblyName ?? "MediumTests",
                compilation.AssemblyName ?? "MediumTests",
                LanguageNames.CSharp,
                compilationOptions: compilation.Options,
                parseOptions: new CSharpParseOptions(),
                metadataReferences: compilation.References,
                documents: documents);
            Workspace.AddProject(project);
        }

        private AdhocWorkspace Workspace { get; }
        internal Project Project => Workspace.CurrentSolution.GetProject(_projectId)!;

        internal async Task<(Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics)> AnalyzeAsync()
        {
            var compilation = await Project.GetCompilationAsync();
            var output = MediumExecution.RunGenerator(compilation!, out var generatorDiagnostics, assertCompiles: false);
            Assert.Empty(generatorDiagnostics);
            return (output, await MediumExecution.Analyze(output));
        }

        internal async Task<Compilation> GenerateAndAssertCompilesAsync()
        {
            var compilation = await Project.GetCompilationAsync();
            var output = MediumExecution.RunGenerator(compilation!, out var generatorDiagnostics);
            Assert.Empty(generatorDiagnostics);
            return output;
        }

        internal async Task<ImmutableArray<CodeAction>> GetActionsAsync(
            Diagnostic diagnostic,
            CodeFixProvider provider)
        {
            var document = Project.Documents.Single(item => item.FilePath == diagnostic.Location.SourceTree?.FilePath);
            var actions = ImmutableArray.CreateBuilder<CodeAction>();
            var context = new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None);
            await provider.RegisterCodeFixesAsync(context);
            return actions.ToImmutable();
        }

        internal async Task ApplyAsync(CodeAction action)
        {
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
            Assert.True(Workspace.TryApplyChanges(apply.ChangedSolution));
        }

        internal async Task ReplaceDocumentTextAsync(string filePath, string text)
        {
            var document = Project.Documents.Single(item => item.FilePath == filePath);
            Assert.True(Workspace.TryApplyChanges(document.WithText(SourceText.From(text)).Project.Solution));
            await Task.CompletedTask;
        }

        internal async Task<string> PersistentTextAsync()
        {
            var texts = new List<string>();
            foreach (var document in Project.Documents.OrderBy(item => item.FilePath, StringComparer.Ordinal))
                texts.Add((await document.GetTextAsync()).ToString());
            return string.Join("\n", texts);
        }

        public void Dispose() => Workspace.Dispose();
    }
}
