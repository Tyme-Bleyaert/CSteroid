# Migrating MediatR to CSteroid.Medium

Medium v1 supports a mechanical migration for closed request/command and
notification/message handlers. It is not a drop-in MediatR compatibility layer:
unsupported dispatch and processing features must be redesigned or left on
MediatR during a staged migration.

The runnable counterpart to this guide is a matched pair of console projects:

- [`Migration.Before`](../examples/Medium/Migration.Before/) uses MediatR
  12.5.0, `AddMediatR`, and a closed pipeline behavior.
- [`Migration.After`](../examples/Medium/Migration.After/) uses the
  Medium runtime, source generator/analyzer, and generated DI registration.

Both demonstrate a response request, a non-response request, two notification
handlers, constructor-injected mediator/sender/publisher interfaces, a closed
logging/validation behavior with scoped state, and real DI scopes. Running either
project executes the same order workflow and prints the resulting receipt and call
sequence.

```bash
dotnet run --project examples/Medium/Migration.Before/Migration.Before.csproj -c Release
dotnet run --project examples/Medium/Migration.After/Migration.After.csproj -c Release
```

These projects are usage examples, not test harnesses. Behavioral assertions live
in the generator test project.

## Messages and handlers

The sample declarations are in
[`Migration.Before/VerticalSlice.cs`](../examples/Medium/Migration.Before/VerticalSlice.cs)
and
[`Migration.After/VerticalSlice.cs`](../examples/Medium/Migration.After/VerticalSlice.cs).
The before sample deliberately uses `IRequest<Unit>` so the removal of `Unit` is
visible.

**Before: MediatR**

```csharp
public sealed record CreateOrder(string Sku) : IRequest<OrderReceipt>;
public sealed record OrderReceipt(Guid OrderId, string Sku);
public sealed record RecordOrder(Guid OrderId) : IRequest<Unit>;
public record OrderCreated(Guid OrderId) : INotification;
```

**After: Medium**

```csharp
public sealed record CreateOrder(string Sku) : ICommand<OrderReceipt>;
public sealed record OrderReceipt(Guid OrderId, string Sku);
public sealed record RecordOrder(Guid OrderId) : ICommand;
public record OrderCreated(Guid OrderId) : IMessage;
```

A response handler keeps its shape apart from the closed interface and return
type:

**Before**

```csharp
public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, OrderReceipt>
{
    public Task<OrderReceipt> Handle(
        CreateOrder request,
        CancellationToken cancellationToken)
    {
        // Scoped state logging omitted here; see VerticalSlice.cs.
        return Task.FromResult(new OrderReceipt(Guid.NewGuid(), request.Sku));
    }
}
```

**After**

```csharp
public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, OrderReceipt>
{
    public ValueTask<OrderReceipt> Handle(
        CreateOrder command,
        CancellationToken cancellationToken)
    {
        // Scoped state logging omitted here; see VerticalSlice.cs.
        return new ValueTask<OrderReceipt>(new OrderReceipt(Guid.NewGuid(), command.Sku));
    }
}
```

The complete files retain their scoped-state dependencies. The omitted logging
above is only to keep the guide focused on the contract change.

The non-response migration removes both the response generic argument and the
`Unit` result:

**Before**

```csharp
public sealed class RecordOrderHandler : IRequestHandler<RecordOrder, Unit>
{
    public Task<Unit> Handle(RecordOrder request, CancellationToken cancellationToken)
    {
        // Work omitted here.
        return Task.FromResult(Unit.Value);
    }
}
```

**After**

```csharp
public sealed class RecordOrderHandler : ICommandHandler<RecordOrder>
{
    public ValueTask Handle(RecordOrder command, CancellationToken cancellationToken)
    {
        // Work omitted here.
        return default;
    }
}
```

For genuinely asynchronous work, change `async Task`/`async Task<T>` to
`async ValueTask`/`async ValueTask<T>`. Do not add `Unit.Value` or a response cast.
`INotificationHandler<OrderCreated>` similarly becomes
`IMessageHandler<OrderCreated>`; the sample's `FirstOrderCreatedHandler` and
`SecondOrderCreatedHandler` show the `Task`-to-`ValueTask` change.

## Pipeline behavior

Both samples use one behavior closed over `CreateOrder` and `OrderReceipt`. It
validates the SKU and records calls immediately before and after the handler.
This is a closed behavior in the source and in DI; Medium also supports
assembly-declared open generic behavior definitions, which it selects and closes
at generation time.

**Before: `Task`, a parameterless continuation, and DI registration**

```csharp
public async Task<OrderReceipt> Handle(
    CreateOrder request,
    RequestHandlerDelegate<OrderReceipt> next,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.Sku))
        throw new ArgumentException("SKU is required.", nameof(request));

    var response = await next().ConfigureAwait(false);
    return response;
}
```

```csharp
services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(OrderWorkflow).Assembly));
services.AddTransient<IPipelineBehavior<CreateOrder, OrderReceipt>, CreateOrderBehavior>();
```

**After: `ValueTask`, explicit message/token forwarding, and an assembly declaration**

```csharp
public async ValueTask<OrderReceipt> Handle(
    CreateOrder message,
    MediumNext<CreateOrder, OrderReceipt> next,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(message.Sku))
        throw new ArgumentException("SKU is required.", nameof(message));

    var response = await next(message, cancellationToken).ConfigureAwait(false);
    return response;
}
```

```csharp
using CSteroid.Medium;

[assembly: MediumCompositionRoot]
[assembly: MediumBehavior(typeof(Migration.After.CreateOrderBehavior), Order = 100)]
```

The continuation accepts the message and token so behaviors can forward or
 deliberately replace either value. There is no parameterless `next()` overload.
Behavior interfaces are invariant; applicability is decided for each closed
route during generation.

## Composition root and constructor injection

Ordinary application-service registration remains ordinary DI registration.
Replace scanning plus individual behavior registration with the root assembly
attribute, behavior attributes, and one generated registration call:

**Before**

```csharp
var services = new ServiceCollection();
services.AddScoped<SliceState>();
services.AddScoped<OrderWorkflow>();
services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(OrderWorkflow).Assembly));
services.AddTransient<IPipelineBehavior<CreateOrder, OrderReceipt>, CreateOrderBehavior>();
```

**After**

```csharp
var services = new ServiceCollection();
services.AddScoped<SliceState>();
services.AddScoped<OrderWorkflow>();
services.AddMedium();
```

`Migration.After.csproj` references `CSteroid.Medium` normally and references
`CSteroid.Generator.Medium` and `CSteroid.Generator.Shared` as analyzers,
matching `Sample.Application` and `Sample.WebApi`. The composition-root assembly
owns generated routing and registration. Referenced handler libraries are
 imported through their generated manifests rather than runtime assembly scans.

Inject the narrowest interface the consumer needs. The sample deliberately takes
all three to show the mechanical rename:

**Before**

```csharp
public OrderWorkflow(
    IMediator mediator,
    ISender sender,
    IPublisher publisher,
    SliceState state)
```

**After**

```csharp
public OrderWorkflow(
    IMedium mediator,
    ISender sender,
    IPublisher publisher,
    SliceState state)
```

`ISender` and `IPublisher` keep their names but move to the
`CSteroid.Medium` namespace. `Send` and `Publish` calls remain awaitable.
For the non-response command, the after workflow simply awaits the non-generic
`ValueTask`; it no longer receives or discards `Unit`.

The scoped mediator resolves concrete handlers from the current scope. Both
programs enable `ValidateScopes` and share scoped state within one scope.

## Mechanical mapping

| MediatR | Medium | Required change |
| --- | --- | --- |
| `IRequest<TResponse>` | `ICommand<TResponse>` | Rename the interface; response commands are invariant. |
| Non-response `IRequest` or `IRequest<Unit>` | `ICommand` | Rename the interface and remove the response argument. |
| `INotification` | `IMessage` | Rename the interface. |
| `IStreamRequest<T>` | `IStreamCommand<T>` | Rename the interface and keep the item type as `T`. |
| `IRequestHandler<TRequest,TResponse>` | `ICommandHandler<TCommand,TResponse>` | Rename and return `ValueTask<TResponse>`. |
| `IRequestHandler<TRequest>` or `IRequestHandler<TRequest,Unit>` | `ICommandHandler<TCommand>` | Rename, return non-generic `ValueTask`, and remove `Unit`. |
| `INotificationHandler<T>` | `IMessageHandler<T>` | Rename and return non-generic `ValueTask`. |
| `IStreamRequestHandler<TRequest,T>` | `IStreamCommandHandler<TCommand,T>` | Rename the interface and keep the `IAsyncEnumerable<T>` return type. |
| `Task` / `Task<T>` handler methods | `ValueTask` / `ValueTask<T>` | Return `default`/`new ValueTask<T>(result)` for synchronous completion or use `async ValueTask`. |
| `Unit.Value` | No value | Return `default` from a synchronous non-response handler; remove caller-side `Unit` handling. |
| `IPipelineBehavior<TRequest,TResponse>` | `IPipelineBehavior<TMessage,TResponse>` | Change the namespace, return `ValueTask<TResponse>`, and use the Medium continuation. |
| `RequestHandlerDelegate<TResponse>` and `next()` | `MediumNext<TMessage,TResponse>` and `next(message, ct)` | Forward or deliberately replace both message and token. Non-response pipelines use `MediumNext<TMessage>`. |
| `IStreamPipelineBehavior<TRequest,T>` | `IStreamPipelineBehavior<TMessage,T>` | Change the namespace and use `MediumStreamNext<TMessage,T>`. Forward the message and token to `next(message, ct)`. |
| `CreateStream(...)` | `Stream(...)` | Rename the call. Inject `IStreamSender` when the consumer only creates streams. |
| Open-generic `IPipelineBehavior<,>` service registration | `[assembly: MediumBehavior(typeof(MyBehavior<,>))]` | Move selection/registration to an assembly declaration; the generator emits closed registrations. Closed behaviors use `typeof(MyBehavior)`, as in the sample. |
| `AddMediatR(...RegisterServicesFromAssembly...)` | `[assembly: MediumCompositionRoot]` plus `services.AddMedium()` | Replace runtime scanning with generated handler and behavior registration in the root assembly. |
| `IMediator` | `IMedium` | Rename the injected contract and field. |
| `ISender` / `IPublisher` | `ISender` / `IPublisher` | Change the namespace/package; call shape stays familiar. Medium also has `IStreamSender` for stream-only consumers. |
| Mocked `IMediator`, `ISender`, or `IPublisher` | Mocked `IMedium`, `ISender`, or `IPublisher` | Rename the mocked contract and configure `ValueTask` results; no real container is required. |
| Directly constructed handler tests | Directly constructed handler tests | Keep the test structure; update only constructor arguments and handler signatures as needed. |

## Dispatch and publication boundaries

### Exact runtime-type routing and invariance

Medium looks up the exact concrete `message.GetType()`. It never falls back
to a base-class or interface route. For example, if an application has handlers
for `OrderCreated` but none for a derived message:

```csharp
public sealed record DerivedOrderCreated(Guid OrderId) : OrderCreated(OrderId);

await publisher.Publish(new DerivedOrderCreated(Guid.NewGuid()), CancellationToken.None);
// No IMessageHandler<OrderCreated> runs.
```

No base-message handler or behavior runs. A concrete Publish use with no valid
handler produces CSM018. An unknown route reached through an interface-held or
otherwise unprovable message throws `InvalidOperationException`. A command sent
through `Send` or `Stream` still requires exactly one handler for its exact concrete command
type; do not expect a base command handler to absorb derived commands. A concrete
Stream use without a valid handler produces CSM021.

`ICommand<TResponse>` is deliberately invariant. For example, an
`ICommand<DerivedResponse>` is not an `ICommand<BaseResponse>`. Contravariance on
a handler's command parameter also does not create additional routes. Migrate
covariant response assumptions to one exact response contract.

### Generic and object-based Send

The generic **response** API is supported:

```csharp
ValueTask<TResponse> Send<TResponse>(
    ICommand<TResponse> command,
    CancellationToken cancellationToken = default);

ValueTask Send(
    ICommand command,
    CancellationToken cancellationToken = default);
```

Generic **message or handler implementation types** are not supported in v1.
MediatR's object-based `Send(object request)` escape hatch has no Medium
equivalent. Introduce closed command contracts instead of relying on object-based
request discovery or generic handler definitions.

### Sequential ordinal Publish, failure, and cancellation

Medium publishes sequentially in deterministic ordinal order by
assembly-aware concrete handler identity. It awaits one handler before resolving
and invoking the next. The sample names its handlers `FirstOrderCreatedHandler`
and `SecondOrderCreatedHandler`; the first yields asynchronously, and the success
trace shows that the second runs only after the first completes.

If the first handler throws, generator execution tests verify all of the following:

- the original exception propagates, without an `AggregateException` wrapper;
- the second handler is not invoked;
- publication is fail-fast rather than best-effort or parallel.

MediatR 12.5.0's default publisher also runs the before sample sequentially and
fails fast. That does not make custom publication strategies portable. Parallel
publishers, custom `INotificationPublisher` implementations, and
`Task.WhenAll`-style fan-out have no v1 equivalent and require an explicit design
decision.

Every selected behavior and handler receives the supplied cancellation token.
`next(message, ct)` forwards the token through the chain. Medium inserts no
synthetic `ThrowIfCancellationRequested` checks: passing an already-canceled token
does not interrupt code that never observes it. Handlers must observe the token
or pass it to cancellation-aware APIs. A fault stops later sequential handlers;
a message behavior, when declared, wraps the entire fan-out once rather than each
subscriber separately.

## Features that do not migrate transparently

- **Pre/post processors and exception plugins:** MediatR pre-processors,
  post-processors, exception handlers, and exception actions have no automatic
  equivalent. Put required logic in explicit pipeline behaviors or application
  code, or retain MediatR for that slice during a staged migration.
- **Polymorphic dispatch:** base-class/interface fallback and covariance-based
  subscriber fan-out are intentionally absent. Publish the exact message type or
  invoke the required routes explicitly.
- **Zero-handler notifications:** unlike MediatR's no-op publication, a concrete
  Publish use without a valid handler is a compile-time CSM018 error. Unknown
  runtime routes throw rather than silently completing.
- **Parallel/custom publication:** only sequential fail-fast publication is
  supported. Do not preserve a custom publisher behind `IPublisher` and assume its
  ordering, exception aggregation, or concurrency survives.
- **Generic handlers and object Send:** closed message and handler types are
  required; there is no object-based Send compatibility surface.

These are semantic boundaries, not mechanical rename steps. No migration-effort
percentage can be inferred from this sample.

## Unit tests after migration

Handlers remain plain classes. A direct handler test needs neither the generated
mediator nor a service provider:

```csharp
var state = new SliceState();
var handler = new CreateOrderHandler(state);

var receipt = await handler.Handle(
    new CreateOrder("SKU-123"),
    CancellationToken.None);

if (receipt.Sku != "SKU-123")
    throw new InvalidOperationException("Unexpected receipt.");
```

Constructor-injected consumers can still use mocks. In a separate test project
using Moq, for example:

```csharp
var mediator = new Mock<IMedium>();
var sender = new Mock<ISender>();
var publisher = new Mock<IPublisher>();

var workflow = new OrderWorkflow(
    mediator.Object,
    sender.Object,
    publisher.Object,
    new SliceState());
```

The before test differs only by mocking `MediatR.IMediator` instead of
`IMedium`; narrow `ISender` and `IPublisher` mocks retain their interface
names. Configure handler-independent consumer tests with `ValueTask` or
`ValueTask<T>` results after migration. Direct handler tests and narrow mocks do
not need a composition root; integration tests that exercise generated routing
should use `AddMedium` and real scopes as the console samples do.
