# CSteroid.Medium architecture

Status: current v1 architecture.

## 1. Product direction and scope

Medium is an in-process mediator with typed handlers and a generator-owned
runtime dispatcher. Consumers inject `IMedium`, `ISender`, or `IPublisher`
through ordinary constructors. Handler classes implement closed typed interfaces.
The generator emits one mediator and one registration surface per composition
root; a separate `DiagnosticAnalyzer` owns all CSM diagnostics.

The design priorities are, in order:

1. A small, familiar consumer API and a mechanical migration path from MediatR.
2. Compiler-enforced handler signatures, exact routing, and actionable diagnostics.
3. Ordinary DI lifetimes and straightforward unit testing.
4. Deterministic incremental generation and maintainable compiler code.
5. Static dispatch and closed generic registration, with performance and AOT
   claims established by local evidence rather than assumed from the architecture.

The host model is removed. There are no partial hosts, host marker interfaces,
provider attachment, generated user-class constructors, host-specific route
tables, or host-is-handler self routes. A class that needs to send or publish
injects the narrowest mediator interface. A class that needs to invoke its own
private operation calls that operation directly.

### V1 scope

- Non-generic, closed message and handler implementation types.
- Typed `ValueTask` and `ValueTask<TResponse>` handler contracts only.
- Multiple handler interfaces on one concrete handler class.
- Exact concrete message-type routing; no base-class or interface fallback.
- Exactly one handler for a command; one or more handlers when an event is published.
- Static type-keyed dispatch tables and strongly typed per-message paths.
- Ordinary scoped mediator resolution and concrete handler registration.
- Sequential, deterministic, fail-fast publication.
- Per-message compile-time behavior selection with closed runtime registration.
- Local handlers plus typed-handler manifests imported from referenced assemblies.
- One NuGet containing the runtime and the compiler components.

Streaming, generic messages/handlers, polymorphic dispatch, alternate publish
strategies, method-convention handlers, synchronous/`Task` handler interfaces,
host façades, exact-name MediatR recognition, code fixes, and interceptors are not
part of v1.

## 2. Usage: the caller's contract

Messages and handlers are ordinary types. No handler discovery attribute or
marker interface is required.

```csharp
using CSteroid.Medium;

public sealed record CreateOrder(string Sku) : ICommand<OrderId>;
public sealed record DeleteOrder(OrderId Id) : ICommand;
public sealed record OrderCreated(OrderId Id) : IMessage;

public sealed class OrderHandlers :
    ICommandHandler<CreateOrder, OrderId>,
    ICommandHandler<DeleteOrder>,
    IMessageHandler<OrderCreated>
{
    public ValueTask<OrderId> Handle(
        CreateOrder command,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public ValueTask Handle(
        DeleteOrder command,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public ValueTask Handle(
        OrderCreated message,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

public sealed class AuditHandler : IMessageHandler<OrderCreated>
{
    public ValueTask Handle(
        OrderCreated message,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
```

A synchronous handler returns `default` for a non-response operation or
`new ValueTask<TResponse>(result)` for a response. A handler that uses a
`Task`-based API can be `async ValueTask`/`async ValueTask<TResponse>` or wrap the
returned task explicitly. There is no second handler contract for `Task`, `void`,
or synchronous results.

Consumers remain ordinary, non-partial classes with unrestricted constructors:

```csharp
public sealed class MailService
{
    private readonly IClock _clock;
    private readonly IMedium _mediator;

    public MailService(IClock clock, IMedium mediator)
    {
        _clock = clock;
        _mediator = mediator;
    }

    public async Task Welcome(string sku, CancellationToken cancellationToken)
    {
        OrderId id = await _mediator.Send(new CreateOrder(sku), cancellationToken);
        await _mediator.Publish(new OrderCreated(id), cancellationToken);
        await _mediator.Send(new DeleteOrder(id), cancellationToken);
    }
}
```

Unit tests can construct `MailService` with a fake or mocked `IMedium` and
`IClock`; no generated attachment state or real service provider is required.
Handlers can also be tested directly as plain classes.

The composition root explicitly opts into mediator emission and imports:

```csharp
using CSteroid.Medium;

[assembly: MediumCompositionRoot]
```

Registration is then one call:

```csharp
var services = new ServiceCollection();
services.AddScoped<IClock, SystemClock>();
services.AddScoped<MailService>();
services.AddMedium();

using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateScopes = true });
using var scope = provider.CreateScope();

var mail = scope.ServiceProvider.GetRequiredService<MailService>();
await mail.Welcome("SKU-123", cancellationToken);
```

Medium does not reconstruct or attach user services.

A handler that legitimately emits a follow-up command or event can inject
`ISender` or `IPublisher`. Normal DI captive-dependency rules still apply: a
singleton handler must not capture a scoped mediator or scoped dependency. This
is not a special self-routing feature.

### Behaviors

Behaviors use MediatR-like `Handle(message, next, cancellationToken)` parameter
order. The continuation accepts the message and token so it need not recapture
those values and so a behavior can deliberately replace them.

```csharp
[assembly: CSteroid.Medium.MediumBehavior(
    typeof(App.LoggingBehavior<,>), Order = 100)]

public sealed class LoggingBehavior<TMessage, TResponse>
    : CSteroid.Medium.IPipelineBehavior<TMessage, TResponse>
    where TMessage : CSteroid.Medium.IMessage
{
    public System.Threading.Tasks.ValueTask<TResponse> Handle(
        TMessage message,
        CSteroid.Medium.MediumNext<TMessage, TResponse> next,
        System.Threading.CancellationToken cancellationToken)
        => next(message, cancellationToken);
}
```

The generator selects a behavior only for routes whose concrete message and
response types satisfy its contract and generic constraints.

## 3. Decisions

| Concern | Decision |
| --- | --- |
| Handler contract | Closed typed interfaces with `ValueTask` returns. The C# compiler enforces method shape. |
| Handler discovery | Directly declared supported handler interfaces; no required class attribute or marker interface. |
| Caller contract | Inject `IMedium`, `ISender`, or `IPublisher`. No user type is modified. |
| Mediator ownership | The generator emits one internal mediator per composition-root assembly. |
| Mediator lifetime | Scoped by default. It captures the current scope's provider and resolves handlers lazily. |
| Response variance | `ICommand<TResponse>` is invariant; covariance would invalidate closed response-delegate casts. |
| Message routing | Exact `message.GetType()` lookup. No assignability or variance-based route matching. |
| Send API | One generic response Send and one non-response Send on `ISender`; no public per-message overload family. |
| Dispatch | Static tables plus private strongly typed adapters, pipelines, and terminals. |
| Dispatch threshold | None in v1. Any chain/table hybrid requires BenchmarkDotNet evidence. |
| Handler resolution | Resolve the concrete handler from the current provider on every handler invocation. |
| Handler registration | Register each concrete handler once at its declared lifetime; typed interfaces are implementation contracts, not independent default service instances. |
| Handler fields | None. DI owns scoped/singleton reuse and transient creation. |
| Send cardinality | Exactly one valid handler. Missing concrete uses and duplicate declarations are analyzer errors. |
| Non-response commands | Return non-generic `ValueTask`; response commands cannot be sent through the non-response route. |
| Events | `IMessageHandler<TMessage>` returns non-generic `ValueTask`; no result-discarding handler form. |
| Missing event routes | A concrete Publish use with no valid handler is CSM018. An unknown runtime route throws `InvalidOperationException`; no behavior runs without a declared route. |
| Publish order | Sequential ordinal order by assembly-aware concrete handler identity. |
| Publish failures | Fail fast; the original fault propagates and later handlers do not run. |
| Publishing commands | Reject with `ArgumentException`; commands use Send. |
| Cancellation | Every handler and behavior receives the supplied token. No synthetic cancellation checks. |
| Behaviors | Compile-time applicability/order and closed concrete resolution/registration. |
| Cross-assembly | Versioned per-handler-type manifests; the marked composition root owns merged routing and registration. |
| Diagnostics | A separate analyzer reports every CSM rule. The generator reports none. |
| Registration | `TryAdd` makes generated registration idempotent and preserves compatible custom descriptors. |
| Host façade/self routes | Removed, including from the designed optional v1 surface. Revisit only with demonstrated demand. |

## 4. Public runtime API

The runtime targets `netstandard2.0` and pins the dependencies required for
`ValueTask` and Microsoft DI abstractions. No streaming dependency is added in
v1.

```csharp
namespace CSteroid.Medium
{
    public interface IMessage { }

    public interface ICommand : IMessage { }

    // Deliberately invariant.
    public interface ICommand<TResponse> : ICommand { }

    public interface ISender
    {
        ValueTask Send(
            ICommand command,
            CancellationToken cancellationToken = default);

        ValueTask<TResponse> Send<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default);
    }

    public interface IPublisher
    {
        ValueTask Publish(
            IMessage message,
            CancellationToken cancellationToken = default);
    }

    public interface IMedium : ISender, IPublisher { }

    public interface ICommandHandler<in TCommand>
        where TCommand : ICommand
    {
        ValueTask Handle(TCommand command, CancellationToken cancellationToken);
    }

    public interface ICommandHandler<in TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        ValueTask<TResponse> Handle(
            TCommand command,
            CancellationToken cancellationToken);
    }

    public interface IMessageHandler<in TMessage>
        where TMessage : IMessage
    {
        ValueTask Handle(TMessage message, CancellationToken cancellationToken);
    }

    public delegate ValueTask MediumNext<in TMessage>(
        TMessage message,
        CancellationToken cancellationToken)
        where TMessage : IMessage;

    public delegate ValueTask<TResponse> MediumNext<in TMessage, TResponse>(
        TMessage message,
        CancellationToken cancellationToken)
        where TMessage : IMessage;

    // Invariant: a message-typed continuation makes contravariant behavior
    // interfaces invalid and exact route selection does not use variance.
    public interface IPipelineBehavior<TMessage>
        where TMessage : IMessage
    {
        ValueTask Handle(
            TMessage message,
            MediumNext<TMessage> next,
            CancellationToken cancellationToken);
    }

    public interface IPipelineBehavior<TMessage, TResponse>
        where TMessage : IMessage
    {
        ValueTask<TResponse> Handle(
            TMessage message,
            MediumNext<TMessage, TResponse> next,
            CancellationToken cancellationToken);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HandlerLifetimeAttribute : Attribute
    {
        public HandlerLifetimeAttribute(ServiceLifetime lifetime);
        public ServiceLifetime Lifetime { get; } // Scoped by default without the attribute.
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class MediumCompositionRootAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class MediumBehaviorAttribute : Attribute
    {
        public MediumBehaviorAttribute(Type behaviorType);
        public Type BehaviorType { get; }
        public int Order { get; set; }                 // 0; lower is outermost.
        public ServiceLifetime Lifetime { get; set; }  // Scoped.
    }

    // Emitted by the generator; not normally written by consumers.
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class MediumManifestAttribute : Attribute
    {
        public MediumManifestAttribute(int version, Type handlerType);
        public int Version { get; }
        public Type HandlerType { get; }
    }
}
```

All public contracts ship only in `CSteroid.Medium.dll`. The generator does
not emit duplicate public runtime attributes through post-initialization output.

Handler and behavior interfaces may be implemented explicitly. Dispatch resolves
the concrete class and invokes the exact closed interface so explicit
implementations work. Contravariance on handler interfaces does not broaden
discovery or routing: only an exact declared closed interface argument creates a
route.

## 5. Repository and package layout

```text
src/CSteroid/
  CSteroid.Medium/
    CSteroid.Medium.csproj            netstandard2.0; packable runtime
    Messages.cs
    Handlers.cs
    MediatorContracts.cs
    Attributes.cs
    PipelineBehaviors.cs

  CSteroid.Generator.Medium/
    CSteroid.Generator.Medium.csproj  netstandard2.0; nonpackable analyzer
    MediumGenerator.cs                incremental provider wiring only
    MediumDiagnosticAnalyzer.cs       Roslyn actions and reporting only
    FactsReader.cs                          source/metadata symbol-to-DTO conversion
    RoutePolicy.cs                          message classification and cardinality
    BehaviorPolicy.cs                       closure, constraints, order, lifetime
    Models.cs                               including EquatableArray<T>
    MediatorPlanner.cs
    MediatorEmitter.cs
    ManifestEmitter.cs
    RegistrationEmitter.cs
    Diagnostics.cs                          descriptors; analyzer reporting only

  CSteroid.Generator.Medium.Tests/    net10.0 xUnit

examples/Medium/Sample.WebApi/
examples/Medium/Sample.Application/   cross-assembly handler library
```

There is no separate analyzer package, generator package, extensions package,
Core dependency, or Orm dependency. The analyzer needs no Workspaces reference.
A future code fix would use a separate Workspaces-referencing project.

### One NuGet, two principal assemblies

`CSteroid.Medium.csproj` owns the package and keeps normal build output.
The compiler project has `OutputItemType=Analyzer`, `IncludeBuildOutput=false`,
and `IsPackable=false`. It contains both the incremental generator and diagnostic
analyzer, allowing them to share policy code without another package dependency.

The compiler uses Roslyn `4.10.0` and `Microsoft.CodeAnalysis.Analyzers` `3.3.4`.
It does not binary-reference the runtime project; runtime types are resolved by
exact metadata name.

The package contains:

```text
lib/netstandard2.0/CSteroid.Medium.dll
analyzers/dotnet/cs/CSteroid.Generator.Medium.dll
analyzers/dotnet/cs/CSteroid.Generator.Shared.dll
```

Shared is limited to genuinely reusable output helpers. The existing analyzer
packaging targets remain unchanged. The projects are added to
`src/CSteroid/CSteroid.slnx`; the samples are added to `examples/Examples.slnx`.

## 6. Shared models and rule ownership

Incremental values are immutable and value-equatable. They contain no `ISymbol`,
`SyntaxNode`, `Compilation`, or `Location`. Strings use ordinal equality;
collections use `EquatableArray<T>`. Generator models do not reference runtime
enum types.

```csharp
internal enum RouteKind { Message, Command, CommandWithResponse }
internal enum LifetimeModel { Scoped, Singleton, Transient }

internal sealed class TypeRef : IEquatable<TypeRef>
{
    internal string RouteKey { get; }       // Metadata identity + assembly identity.
    internal string GlobalName { get; }     // Emission-ready global:: spelling.
    internal string AssemblyName { get; }
    internal bool IsValueType { get; }
}

internal sealed class HandlerRouteModel : IEquatable<HandlerRouteModel>
{
    internal RouteKind Kind { get; }
    internal TypeRef Message { get; }
    internal TypeRef Response { get; }      // Present only for CommandWithResponse.
}

internal sealed class HandlerDeclarationModel : IEquatable<HandlerDeclarationModel>
{
    internal TypeRef HandlerType { get; }
    internal LifetimeModel Lifetime { get; }
    internal EquatableArray<HandlerRouteModel> Routes { get; }
    internal EquatableArray<RuleIssue> Issues { get; }
}

internal sealed class ManifestModel : IEquatable<ManifestModel>
{
    internal int Version { get; }
    internal string AssemblyName { get; }
    internal TypeRef HandlerType { get; }
}

internal sealed class CompilationFacts : IEquatable<CompilationFacts>
{
    internal bool IsCompositionRoot { get; }
    internal EquatableArray<BehaviorDefinitionModel> Behaviors { get; }
    internal EquatableArray<HandlerDeclarationModel> ImportedHandlers { get; }
    internal EquatableArray<RuleIssue> ImportIssues { get; }
}

internal sealed class GlobalPlan : IEquatable<GlobalPlan>
{
    internal EquatableArray<RoutePlan> ValidRoutes { get; }
    internal EquatableArray<RegistrationPlan> Registrations { get; }
    internal EquatableArray<string> InvalidRouteKeys { get; }
}
```

`RuleIssue` and `LocationInfo` are structured planning facts, not a second
reporting channel. The generator uses issues to suppress invalid emission and
never calls `ReportDiagnostic`.

Ownership is explicit:

1. `FactsReader` reads directly declared handler interfaces and attributes from
   source or metadata symbols and converts them immediately to DTOs.
2. `RoutePolicy` classifies routes, rejects invalid message contracts, and owns
   command cardinality.
3. `BehaviorPolicy` owns behavior closure, constraint filtering, ordering, and
   lifetime selection.
4. `MediatorPlanner` derives tables, adapters, pipelines, terminals, and concrete
   registrations from one valid route set.
5. Emitters render only. Registration and dispatch cannot maintain separate route
   lists.

Analyzer and generator may repeat planning work. They must call the same policy
entry points; parity is preferable to two divergent implementations.

## 7. Analyzer architecture

`MediumDiagnosticAnalyzer` enables concurrent execution and keeps no mutable
static compilation state. It enables generated-code analysis callbacks so source
symbol and invocation actions still run in generated compilations, but does not
enable diagnostic reporting for generated code.

### Per-compilation flow

1. **Compilation start:** resolve runtime contracts by metadata name and create
   compilation-local concurrent collections for declarations and concrete Send
   uses. If the contracts are absent, do nothing.
2. **Named-type symbol actions:** inspect source classes with directly declared
   supported handler interfaces. Compiler interface-implementation errors are not
   duplicated by CSM diagnostics.
3. **Invocation syntax actions:** recognize calls to the runtime `ISender.Send`
   and `IPublisher.Publish` contracts, including calls through `IMedium`.
   Same-named unrelated methods are not mediator uses.
4. **Compilation end:** read local behavior attributes and referenced manifests,
   run shared policies, evaluate concrete Send and Publish uses, sort diagnostics by stable
   key and location, report them, and release state.

For a concrete command or event expression, the analyzer strips only a compiler
conversion to the message interface and records the concrete operand type. It
does not guess through an interface-held variable, type parameter, or arbitrary
flow. Unknown runtime types retain their runtime guards.

CSM001 applies to concrete recognized Send uses, not every unused command
declaration. CSM018 applies the same rule to concrete Publish uses, including
commands passed to Publish, and does not reject unused event declarations.
Interface-held Send and Publish arguments are not guessed; their runtime guards
remain necessary. CSM002 and malformed handler/behavior declarations are
reported without a composition root. Imported duplicate commands are diagnosed
in the marked root, where the complete merged route set is available.

## 8. Incremental discovery and provider graph

### Handler discovery

Typed interfaces are the discovery key. Use a cheap base-list predicate followed
by an immediate semantic projection:

```csharp
var handlers = context.SyntaxProvider.CreateSyntaxProvider(
    static (node, ct) =>
        node is ClassDeclarationSyntax { BaseList: not null } or
               RecordDeclarationSyntax { BaseList: not null },
    static (ctx, ct) => FactsReader.ReadSourceHandler(ctx));
```

`ReadSourceHandler` inspects directly declared `INamedTypeSymbol.Interfaces`, not
`AllInterfaces`. An inherited-only handler implementation does not silently create
another concrete route. A derived class that is intended to be a separate handler
must directly declare the closed handler interface.

Only the three exact interface definitions are recognized:

- `ICommandHandler<TCommand>`
- `ICommandHandler<TCommand,TResponse>`
- `IMessageHandler<TMessage>`

Partial declarations can produce the same symbol through more than one syntax
candidate. The planner de-duplicates by assembly-aware handler identity before
building routes. Multiple distinct supported interfaces on one class produce
multiple routes but only one concrete registration.

Inherited-only implementations are simply not discovered; they do not receive a
handler-shape diagnostic. A concrete Send use can still receive CSM001 when no
directly declared route exists.

`FactsReader` rejects local handlers, messages, and response types that are
inaccessible to generated namespace-level code, including inaccessible nested
types. Imported types must additionally be visible from the composition root.
Invalid declarations are excluded before manifest or mediator emission.

No attribute is required on handlers. `ForAttributeWithMetadataName` remains
useful for future opt-in declaration attributes, but it is not the sole handler
discovery mechanism. Interface discovery feasibility is supported by peer
generators; its cost in this repository remains an incrementality gate.

### Composition and metadata projection

An isolated `CompilationProvider` projection reads:

- The current assembly's `MediumCompositionRootAttribute`.
- Current-assembly behavior declarations.
- `MediumManifestAttribute` values on referenced assemblies.

The projection resolves imported handler symbols and immediately returns an
equatable `CompilationFacts`, using the same `FactsReader` as source discovery.
`MetadataReferencesProvider` alone cannot inspect assembly attributes. An equal
projection must stop downstream invalidation even though the compilation itself
changed.

### Provider graph

```text
source-local handlers --Collect--> LocalManifestPlan --> manifest emission
        |
        |                              imported handlers (root input only)
        |                              /
        +--> RoutePolicy + BehaviorPolicy +--> GlobalPlan
CompilationFacts (root + behaviors) --------/         |
composition root only:                         mediator + registration
```

Manifest emission is deliberately separate from merged root planning.
`LocalManifestPlan` contains only valid handlers declared in the current source
assembly. Imported handlers are never re-exported, and an imported duplicate or
other root-only failure cannot remove or change a producer's manifest. Imports
feed only mediator and registration emission in a marked composition root.

There is no message-declaration inventory and no Send call-site provider in the
generator. A Send-only edit can change analyzer diagnostics but does not alter
routes. An unrelated message edit does not invalidate generation. Any real route
change can regenerate the single central mediator; equal equatable plans prevent
stale or unnecessary output.

Every provider and planning boundary has a tracking name. Mutable dictionaries
stay inside planners and become sorted equatable arrays at provider boundaries.
Output uses stable hint names, ordinal ordering, `// <auto-generated />`,
`[GeneratedCode]`, `#nullable enable`, classic block namespaces, fully qualified
references, and normalized UTF-8/LF.

## 9. Static dispatch and per-message paths

A composition root emits one `GeneratedMedium : IMedium`. It has a
single readonly `IServiceProvider` field and up to three disjoint static route
families:

- Response commands: `Dictionary<Type, Delegate>` containing closed response
  dispatch delegates.
- Non-response commands: `Dictionary<Type, __MED_CommandDispatch>`.
- Events: `Dictionary<Type, __MED_PublishDispatch>`.

Tables are initialized once per generated mediator type and never mutated.
Lookup uses `GetType()` as the exact key. Static adapters take the current scope's
provider as an argument and capture no mediator instance. Empty families emit no
table and retain their defined fallback.

Always-table is a v1 simplicity choice, not a performance claim. Benchmarks must
compare chains and tables across route counts, hot/cold calls, known/unknown types,
and static-initialization cost before a hybrid mechanism is considered.

### Response Send

```csharp
private delegate global::System.Threading.Tasks.ValueTask<TResponse>
    __MED_ResponseDispatch<TResponse>(
        global::System.IServiceProvider services,
        global::CSteroid.Medium.ICommand<TResponse> command,
        global::System.Threading.CancellationToken cancellationToken);

private static readonly global::System.Collections.Generic.Dictionary<
    global::System.Type, global::System.Delegate> __med_responseRoutes =
    new global::System.Collections.Generic.Dictionary<
        global::System.Type, global::System.Delegate>
    {
        {
            typeof(global::App.CreateOrder),
            new __MED_ResponseDispatch<global::App.OrderId>(
                __MED_Dispatch_CreateOrder)
        }
    };

public global::System.Threading.Tasks.ValueTask<TResponse> Send<TResponse>(
    global::CSteroid.Medium.ICommand<TResponse> command,
    global::System.Threading.CancellationToken cancellationToken = default)
{
    if (command is null)
        throw new global::System.ArgumentNullException(nameof(command));

    if (__med_responseRoutes.TryGetValue(command.GetType(), out var route))
    {
        return ((__MED_ResponseDispatch<TResponse>)route)(
            _services, command, cancellationToken);
    }

    throw new global::System.InvalidOperationException(
        "No exact Medium response-command route for " +
        command.GetType().FullName + ".");
}

private static global::System.Threading.Tasks.ValueTask<global::App.OrderId>
    __MED_Dispatch_CreateOrder(
        global::System.IServiceProvider services,
        global::CSteroid.Medium.ICommand<global::App.OrderId> command,
        global::System.Threading.CancellationToken cancellationToken)
    => __MED_Send_CreateOrder(
        services,
        (global::App.CreateOrder)command,
        cancellationToken);
```

The closed delegate cast is sound because:

1. `ICommand<TResponse>` is invariant.
2. A valid message has exactly one response contract.
3. The table key is the exact concrete runtime command type.
4. The adapter is closed over that same response type.
5. Invalid and duplicate routes have no table entry.

Only the delegate reference and message are cast. No response result passes
through `object`, and there is no `(TResponse)(object)result` cast. A value-type
command can still be boxed at its interface boundary; that separate cost is not
claimed away.

### Non-response Send and Publish

The non-response delegate accepts `(IServiceProvider, ICommand,
CancellationToken)`. A response command erased to `ICommand` has no non-response
entry and throws; its result is never discarded.

Publish uses an equivalent delegate accepting `IMessage`. After null and
`message is ICommand` checks, a missing publish entry throws
`InvalidOperationException`. A concrete call with no valid handler is normally
caught earlier by CSM018; the runtime guard covers interface-held and otherwise
unprovable calls. Dictionary enumeration order is never used for publication
order.

### Strongly typed terminal

Each terminal knows its concrete message, response, and handler types:

```csharp
private static global::System.Threading.Tasks.ValueTask<global::App.OrderId>
    __MED_Send_CreateOrderTerminal(
        global::System.IServiceProvider services,
        global::App.CreateOrder command,
        global::System.Threading.CancellationToken cancellationToken)
{
    var handler =
        (global::CSteroid.Medium.ICommandHandler<
            global::App.CreateOrder, global::App.OrderId>)
        global::Microsoft.Extensions.DependencyInjection
            .ServiceProviderServiceExtensions
            .GetRequiredService<global::App.CreateOrderHandler>(services);

    return handler.Handle(command, cancellationToken);
}
```

The exact closed interface invocation supports public and explicit interface
implementations. Handler resolution remains lazy and uses the provider belonging
to the caller's current scope.

## 10. Execution, publication, and DI

### ValueTask execution

Typed interfaces replace the old six-return-shape normalizer:

- Response terminals return the handler's `ValueTask<TResponse>` directly.
- Non-response command terminals return `ValueTask` directly.
- A single-subscriber publish terminal returns the subscriber's `ValueTask`
  directly.
- Multi-subscriber publication awaits each subscriber once, sequentially, with
  `ConfigureAwait(false)`; the first fault stops the route.

No synchronous `void`/result invocation, `Task` wrapping, result discarding, or
`ValueTask<T>.AsTask()` publication conversion remains in the core emitter.
Every `ValueTask` is consumed exactly once. Synchronous handlers express
completion through their return value rather than a separate method shape.

### Scoped mediator and aliases

The generated registration owns the mediator constructor, so it can construct it
directly without reconstructing user code:

```csharp
services.TryAddScoped(sp => new GeneratedMedium(sp));
services.TryAddScoped<IMedium>(
    sp => sp.GetRequiredService<GeneratedMedium>());
services.TryAddScoped<ISender>(
    sp => sp.GetRequiredService<IMedium>());
services.TryAddScoped<IPublisher>(
    sp => sp.GetRequiredService<IMedium>());
```

By default, the three public interfaces alias the same scoped mediator instance.
A pre-registered custom `IMedium` is preserved and becomes the target of
the generated `ISender`/`IPublisher` aliases. Independently pre-registered
`ISender` or `IPublisher` descriptors are also preserved as intentional overrides;
the single-instance guarantee applies only where those independent overrides do
not exist.

The mediator has no mutable per-handler state. Resolving it from the current scope
gives it that scope's provider; injecting a scoped mediator into a singleton
remains an ordinary captive dependency for `ValidateScopes` to reject.

### Concrete handler registration

Each concrete handler is registered once even when it implements several handler
interfaces:

```csharp
services.TryAddScoped<global::App.OrderHandlers>();
services.TryAddScoped<global::App.AuditHandler>();
```

`HandlerLifetimeAttribute` selects scoped, singleton, or transient registration;
scoped is the default. Dispatch resolves the concrete type for every handler
invocation. Microsoft DI therefore owns all reuse semantics:

- Scoped: one instance per scope, including multiple routes on the same class.
- Singleton: the container-owned singleton.
- Transient: a new resolution per handler-method invocation.

There are no `??=` fields and no exactly-once or concurrency claims to invent.
The generator does not independently register both a concrete handler and its
handler interface as implementation descriptors; doing so could create separate
scoped instances. If direct handler-interface injection is supported later, it
must alias the canonical concrete registration.

For publication, the emitter writes the compile-time ordered concrete handler
list. It does not resolve `IEnumerable<IMessageHandler<T>>`, depend on runtime
registration order, or discover additional runtime subscribers.

`TryAdd` preserves a pre-registered concrete descriptor and makes repeated calls
idempotent. Because the mediator no longer caches handlers or imposes a lifetime
on user hosts, a custom concrete handler lifetime does not invalidate generated
field logic. Consumers remain responsible for normal DI correctness and should
use `ValidateScopes`, and optionally `ValidateOnBuild`, in tests.

Removing a handler removes its route, adapter, pipeline, terminal, manifest entry,
and now-unused registration. Removing one interface from a multi-route handler
removes only that route; the concrete registration remains while any route uses
it.

## 11. Compile-time-selected runtime behaviors

### Selection

`MediumBehaviorAttribute` accepts a supported non-generic closed behavior
class or an open generic class definition of arity one or two.

1. Response Send closes against
   `IPipelineBehavior<TConcreteMessage,TResponse>`. Non-response Send and Publish
   close against `IPipelineBehavior<TConcreteMessage>`.
2. A non-generic behavior is selected only when it implements the exact closed
   interface. Variance does not opt a base-message behavior into another route.
3. An open generic behavior is closed with the concrete route arguments. Roslyn
   checks all substituted constraints. Constraint failure means the behavior is
   not selected for that route; it is not a route error.
4. Abstract/static classes, unsupported generic shapes, open constructed
   leftovers, inaccessible selected closures, and unsupported contracts are
   diagnosed.
5. Duplicate attributes for the same behavior type are CSM015.
6. Order by `Order` ascending, then assembly-aware behavior type identity using
   ordinal comparison. Lower `Order` is outermost. DI registration order does not
   control execution order.

Only composition-root behavior declarations apply to the merged local/imported
route set. Producer-assembly behavior declarations do not implicitly become
global in a consuming root.

### Closed registration and resolution

For a selected open generic behavior, registration contains only the closed
concrete type:

```csharp
services.TryAddScoped<global::App.LoggingBehavior<
    global::App.CreateOrder, global::App.OrderId>>();
```

Each distinct closed behavior type is registered once at its declared lifetime.
Resolution is a closed `GetRequiredService<LoggingBehavior<CreateOrder,OrderId>>`
call on every dispatch. There are no behavior fields, open-generic behavior
descriptors, runtime behavior enumeration, or runtime additions/removals.

### Pipeline shape and honest cost

With no selected behavior, a route calls its terminal directly. Otherwise the
emitter unrolls the fixed chain in reverse order:

```csharp
private static global::System.Threading.Tasks.ValueTask<global::App.OrderId>
    __MED_Send_CreateOrder(
        global::System.IServiceProvider services,
        global::App.CreateOrder command,
        global::System.Threading.CancellationToken cancellationToken)
{
    global::CSteroid.Medium.MediumNext<
        global::App.CreateOrder, global::App.OrderId> terminal =
        (message, ct) => __MED_Send_CreateOrderTerminal(services, message, ct);

    var logging =
        (global::CSteroid.Medium.IPipelineBehavior<
            global::App.CreateOrder, global::App.OrderId>)
        services.GetRequiredService<global::App.LoggingBehavior<
            global::App.CreateOrder, global::App.OrderId>>();

    global::CSteroid.Medium.MediumNext<
        global::App.CreateOrder, global::App.OrderId> step =
        (message, ct) => logging.Handle(message, terminal, ct);

    return step(command, cancellationToken);
}
```

Multiple behaviors are unrolled in reverse so lower `Order` is outermost and no
loop-variable capture bug occurs. Every behavior is invoked through its exact
closed interface, as handlers are, so explicit interface implementations compile.
Non-response Send uses the non-generic continuation. Publish places one behavior
chain around the whole sequential fan-out, not one chain per subscriber.

The message/token-forwarding continuation avoids recapturing the original message
and token and permits their deliberate replacement. It does not prove
allocation-free execution: delegates and closure/display objects can still
allocate for the provider, behavior, and downstream continuation. A struct-based
pipeline requires a separate design and benchmarks.

## 12. Cross-assembly architecture

Cross-assembly support is a v1 release feature, not a claim that ordinary DI
registration can create compile-time route knowledge. The root generator still
needs concrete message, response, and handler types to emit static tables and
closed pipelines.

### Producer manifest

Every assembly with valid source-local handlers emits one versioned assembly
attribute per concrete handler type:

```csharp
[assembly: global::CSteroid.Medium.MediumManifest(
    1, typeof(global::App.CreateOrderHandler))]
```

A multi-route handler appears once. The manifest is a handler-type inventory, not
a serialized route protocol. It contains no method names, return shapes, behavior
selections, executable adapters, or per-route duplicate payloads. The consumer
uses the `Type` constant to obtain the handler symbol and `FactsReader` derives
its exact closed interfaces, message/response types, lifetime, and accessibility.

Manifests contain only source-local handlers. Referenced manifests are never
re-exported, and imported duplicates or other root-only failures do not affect a
producer's exported inventory. Each imported handler is de-duplicated by its
assembly-aware type identity.

This requires handler-producing projects to reference the same NuGet so their
generator emits manifests. A metadata assembly containing typed handlers but no
manifest is not implicitly scanned in v1; an explicit assembly-list scanner can
be designed later if demand justifies its compilation cost.

### Composition root import

`[assembly: MediumCompositionRoot]` opts the current assembly into:

1. Importing manifests from referenced assemblies.
2. Merging local and imported handler declarations through the same policies.
3. Diagnosing unsupported manifest versions, inaccessible imported types, and
   duplicate command routes across the merged set.
4. Emitting the central static mediator and all closed behavior selections.
5. Owning concrete registration for local and imported handlers and behaviors.

Imported handlers and their message/response types must be public or effectively
visible through `InternalsVisibleTo`. The root never imports producer behavior
attributes implicitly. A producer that is not a composition root emits manifests
but no mediator or `AddMedium` surface, avoiding unused dispatch engines in
handler libraries.

Multiple roots can import the same producer; each root owns an independent
mediator and registration plan. The generated `AddMedium` extension is
assembly-local so same-named extensions in referenced roots do not create an
ambiguous public API.

## 13. Diagnostics

All CSM diagnostics belong to the analyzer. Descriptors are static, unique, and
parameterized. Neither component catches cancellation and converts it into a
generator failure. Invalid routes never produce table entries or executable
delegate casts; unrelated valid routes still generate.

IDs retired from the host/method architecture are not reused for unrelated rules.

| ID | Severity | Location | Rule and safe-emission behavior |
| --- | --- | --- | --- |
| CSM001 | Error | Concrete recognized Send use | No valid route for that concrete command and Send family. No diagnostic on unused declarations or unprovable interface-held uses. |
| CSM002 | Error | Second and subsequent handler declarations | More than one valid concrete handler for a command. Omit all routes for that command. |
| CSM006 | Error | Handler/message/response declaration | A local type is inaccessible to generated namespace-level code, or an imported type is invisible to the composition root. Exclude the affected route and any invalid local manifest entry. |
| CSM011 | Error | Handler class | Abstract/static/open-generic handler or no usable DI construction shape. Exclude affected declarations. Inherited-only implementations remain undiscovered without a CSM011 diagnostic. |
| CSM012 | Error | Handler interface or message | Multiple response contracts, open-generic/abstract/interface exact route, response command handled as non-response, or command declared as an event handler. Exclude the route. |
| CSM013 | Error | Handler lifetime attribute | Invalid lifetime value. Exclude the invalid declaration rather than guessing a lifetime. |
| CSM014 | Error | Behavior attribute/type | Invalid behavior declaration, generic shape, or no supported behavior contract. Exclude the declaration. |
| CSM015 | Error | Duplicate behavior attributes | Duplicate declaration for one behavior type. Exclude the ambiguous set. |
| CSM016 | Error | Behavior attribute/type | Selected closed behavior is inaccessible or non-DI-constructible. Omit that selection and retain other valid components. |
| CSM017 | Error | Referenced assembly or root attribute | Unsupported/malformed manifest version or payload. Exclude the affected import. |
| CSM018 | Error | Concrete recognized Publish use | No valid handler for that concrete event, or a command was passed to Publish. Unused event declarations and unprovable interface-held uses are not diagnosed. |

CSM003, CSM004, CSM005, CSM007, CSM008, CSM009, and CSM010 are retired:
partial-host rules, constructor reconstruction, method-signature conventions,
host captive analysis, free-form return matching, member collisions, and
static/generic method rules no longer exist. Ordinary C# interface diagnostics
replace custom handler signature diagnostics. Ordinary DI validation replaces
host-specific lifetime inference.

CSM001 and CSM018 are suppressed when another diagnostic already explains the
same missing route. Warning-only rejection never makes a route valid. Plain
records that implement neither `IMessage` nor `ICommand` are rejected by the C#
compiler because the public contracts have no `object`-based overload.

## 14. MediatR migration

See [Migrating MediatR to CSteroid.Medium](medium-migration.md) for the
before/after sample, mechanical mapping, and semantic boundaries.

## 15. Testing and evidence strategy

### Contracts, analyzer, and shared-policy tests

- Compile all public contracts on `netstandard2.0`; prove response invariance,
  handler variance, and invariant behavior interfaces with positive and negative
  compilation cases.
- Run the analyzer without the generator and verify every ID, severity, location,
  ordering, and suppression rule through `Compilation.WithAnalyzers`.
- Assert the generator driver reports no CSM diagnostics.
- Feed identical source and metadata facts through analyzer and generator policy
  entry points and compare valid/invalid route sets.
- Cover direct interface discovery, partial de-duplication, multiple interfaces
  per class, explicit implementations, inherited-only declarations, inaccessible
  local nested handlers/messages/responses, concrete Send and Publish uses,
  conversions, plain-record compiler errors, and unknown interface-held uses.

### Emission and runtime tests

- Snapshot and compile empty, one-route, and many-route tables for all route
  families.
- Cover reference/value responses, struct messages, synchronous and asynchronous
  `ValueTask` completion, global qualification, stable hints, nullable output, and
  UTF-8/LF determinism.
- Assert response source contains no result-object cast and invokes one exact
  closed delegate.
- Test unknown/derived messages, erased response commands, nulls, and commands
  passed to Publish.
- Test one/many subscriber Publish, missing concrete route diagnostics,
  unknown-route runtime failures, sequential ordering, await-once behavior, and
  fail-fast propagation.
- Test explicit interface invocation and multi-route handlers with one shared
  scoped concrete registration.

### DI and behavior tests

- Resolve all three mediator interfaces and verify their default scoped aliasing,
  custom `IMedium` replacement, and intentional independent narrow-interface
  overrides.
- Use real scopes with `ValidateScopes`; cover scoped/singleton/transient handlers,
  repeated registration, custom concrete descriptors, and handler-to-mediator
  dependencies without claiming singleton captive safety.
- Snapshot closed behavior selections and registrations; assert no open-generic
  descriptor or runtime behavior enumeration.
- Cover constraint inclusion/exclusion, exact interface selection, implicit and
  explicit behavior implementations, duplicate declarations, inaccessible
  closures, and every lifetime.
- Verify numeric order, ordinal tie-breaking, outermost semantics, message/token
  forwarding and replacement, terminal execution exactly once, exception and
  cancellation propagation, and one chain around an entire Publish fan-out.
- Do not assert that a nonempty delegate pipeline is allocation-free.

### Cross-assembly, incremental, migration, and benchmark gates

- Build a two-assembly Application/WebApi slice and prove manifest import, root
  registration, explicit implementations, local/imported accessibility, duplicate
  commands, unsupported versions, behavior ownership, assembly-aware manifest
  de-duplication, and that imported handlers are never re-exported.
- Retain `CSharpGeneratorDriver` tracked-step tests. Unrelated edits and Send-only
  edits do not change generated output; partial candidates de-duplicate; equal
  compilation projections and plans remain cached.
- Repeat runs and shuffle syntax trees to prove byte-identical output and stable
  diagnostic ordering. Measure interface discovery and manifest projection in a
  large synthetic solution.

- Migrate a representative MediatR sample, compile and run both before/after
  projects, and document every semantic boundary rather than estimating migration
  percentages.
- Benchmark tables versus chains, mediator versus handwritten dispatch, sync/async
  completion, response delegate casts, publish fan-out, and behavior chains.
  Include allocations and generator build cost.

The samples enable `EmitCompilerGeneratedFiles` so consumers can inspect the
generated source. CI builds the samples; behavioral assertions remain in the
generator test project.

## 16. Design rationale

The architecture uses typed handlers with an injected mediator as the only core
model. The reasons are consumer-facing, not an assumed dispatch-speed win:

- Typed interfaces make handler signatures compiler-enforced and preserve the
  familiar MediatR handler/caller structure.
- A generator-owned mediator eliminates attachment, partial-type rules, user
  constructor reconstruction, host collisions, and host-is-handler special cases.
- Ordinary interface injection makes consumer unit tests independent of a real
  container.
- One central plan avoids duplicating tables and resolution state per host.
- The useful dispatch, analyzer, behavior, packaging, and verification mechanisms
  survive because they are independent of the caller shell.

The design keeps three constraints explicit:

1. Typed interfaces do not support the old six free-form return shapes, so v1
   deliberately chooses one `ValueTask` contract instead of claiming both.
2. A message-typed continuation requires invariant behavior interfaces; response
   command covariance is also rejected to preserve closed delegate safety.
3. Mediator injection does not eliminate cross-assembly metadata. A small
   versioned handler-type manifest remains, while route serialization and
   executable DI fallback adapters do not.

Quantitative claims—complexity percentages, migration percentages, performance
multipliers, “zero reflection,” “100% AOT safe,” and closure-free pipelines—are
not architecture facts. They are excluded until reproduced by local measurements.

### Red-flag screen

- **Interface depth:** three narrow caller interfaces hide static dispatch,
  scoped resolution, publication, and behavior composition without modifying
  consumer classes.
- **Split ownership:** shared policies own validity; the analyzer reports; the
  planner owns safe emission; Microsoft DI owns service construction and reuse.
- **Two ways to do one task:** one handler model, one caller model, one dispatch
  mechanism, one registration path, and one cross-assembly metadata path.
- **Importable internals:** tables, adapters, and terminals are private; the
  generated mediator is internal; manifests contain metadata only.
- **Hand-synced lists:** local manifests derive from a local-only handler plan;
  root routes, pipelines, and registrations derive from the same merged root
  plan. Imports never feed back into manifest emission.
- **Unsupported certainty:** no unmeasured performance/AOT/allocation claim, no
  hidden exactly-once field semantics, and no invented external DI lifetime
  knowledge.

## 17. Tradeoffs and remaining risks

- Requiring `ValueTask` gives one compiler-checked contract and simple emitters,
  but every MediatR `Task` handler needs a mechanical signature migration.
- Interface-based discovery avoids per-handler attributes but scans more syntax
  candidates than attribute-only discovery. Its IDE cost must be measured.
- A scoped mediator matches common application scopes and lazy handler resolution,
  but normal captive-dependency rules remain; the architecture does not make
  singleton consumers safely capture scoped services.
- Concrete handler resolution preserves fixed routes, deterministic publication,
  and shared scoped instances, but does not by itself make arbitrary runtime
  interface registrations part of a route.
- Static tables accept type-initialization cost at every route count in exchange
  for one deterministic v1 mechanism and no response-result erasure.
- Compile-time behavior immutability trades runtime flexibility for deterministic
  applicability/order and closed registration.
- Manifests require the generator in handler-producing projects and an explicit
  composition root. This is deliberate in exchange for predictable imports and no
  whole-referenced-assembly scan.
- Analyzer and generator repeat shared planning work. Parity tests and realistic
  compilation performance are release gates.

## 18. References

- MediatR: https://github.com/LuckyPennySoftware/MediatR
- martinothamar/Mediator: https://github.com/martinothamar/Mediator
- Immediate.Handlers: https://github.com/ImmediatePlatform/Immediate.Handlers
- Wolverine handlers: https://wolverinefx.net/guide/handlers/
- Foundatio.Mediator: https://github.com/foundatiofx/Foundatio.Mediator
- Roslyn incremental-generator cookbook:
  https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md
- Roslyn analyzer/generator diagnostics guidance:
  https://github.com/dotnet/roslyn/issues/71709
- NativeAOT deployment:
  https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
- AOT-compatible library guidance:
  https://devblogs.microsoft.com/dotnet/creating-aot-compatible-libraries/
