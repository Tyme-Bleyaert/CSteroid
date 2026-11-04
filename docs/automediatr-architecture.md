# CSteroid.AutoMediator architecture

Status: synthesized design and implementation plan. No production implementation is
included in this change.

## 1. Problem and scope

AutoMediator adds mediator behavior to existing partial classes without changing
their constructors or requiring a runtime mediator service. A host implements
`IAutoMediatorService`; the generator discovers messages and handler services,
validates the routes, and emits `Send`, `Publish`, handler resolution, and DI
registration into the host's assembly.

The difficult parts are not the method calls themselves. They are exact-type
routing, command cardinality, six handler return shapes, DI lifetime correctness,
host-as-handler cycles, deterministic incremental generation, and shipping a
runtime library and Roslyn analyzer in one NuGet package.

### V1 scope

- Source-discovered hosts and handlers in one assembly.
- Exact concrete message-type matching; no base-class or interface fallback.
- Non-generic, closed message, host, and handler service types.
- Instance handler methods only.
- Sequential, deterministic, fail-fast publication.
- Runtime pipeline behaviors before the v1 release; compile-time behaviors later.
- Cross-assembly handlers are a separate post-core milestone, not an implicit v1
  capability.

Generic hosts and generic containing types are deferred. Nested non-generic hosts
are supported when every containing type is partial and accessible to generated
registration.

## 2. Usage: the caller's contract

```csharp
using CSteroid.AutoMediator;

public sealed record CreateOrder(string Sku) : ICommand<OrderId>;
public sealed record DeleteOrder(OrderId Id) : ICommand;
public sealed record OrderCreated(OrderId Id) : IMessage;

public sealed class OrderService : IAutoMediatorHandler
{
    public Task<OrderId> Handle(CreateOrder command, CancellationToken ct)
        => throw new NotImplementedException();

    public void Handle(DeleteOrder command)
        => throw new NotImplementedException();

    public ValueTask Handle(OrderCreated message, CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed class AuditService
{
    [Handler]
    public void Record(OrderCreated message)
        => throw new NotImplementedException();
}

public partial class MailService : IAutoMediatorService
{
    private readonly IClock _clock;

    // Existing constructors remain unchanged.
    public MailService(IClock clock) => _clock = clock;

    public async Task Welcome(string sku, CancellationToken ct)
    {
        OrderId id = await Send(new CreateOrder(sku), ct);
        await Publish(new OrderCreated(id), ct);
        await Send(new DeleteOrder(id), ct);
    }
}
```

At the composition root:

```csharp
var services = new ServiceCollection();
services.AddScoped<IClock, SystemClock>();
services.AddAutoMediator();

using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateScopes = true });
using var scope = provider.CreateScope();

var mail = scope.ServiceProvider.GetRequiredService<MailService>();
await mail.Welcome("SKU-123", cancellationToken);
```

A class can be both host and handler:

```csharp
public partial class OrderProjection : IAutoMediatorService, IAutoMediatorHandler
{
    [Handler]
    private void Record(OrderCreated message)
        => throw new NotImplementedException();

    public ValueTask Project(OrderCreated message, CancellationToken ct)
        => Publish(message, ct);
}
```

The `OrderProjection` route calls `this.Record(message)` directly. It never
resolves `OrderProjection` from its own attached provider.

To expose dispatch outside the host:

```csharp
[AutoMediatorHost(Accessibility = AutoMediatorMemberAccessibility.Public)]
public partial class OrdersApi : IAutoMediatorService
{
}

ICommand<OrderId> command = new CreateOrder("SKU-123");
OrderId id = await api.Send(command, cancellationToken);
```

The fixed generic surface still gives a statically typed result. Route existence
is checked by exact runtime type; an unknown concrete command throws rather than
causing an invalid cast or silently selecting a base route.

## 3. Decisions

| Concern | Decision |
| --- | --- |
| Host API | One generic `Send<TResponse>`, one non-generic `Send`, and one `Publish`; no per-message overload family. |
| Host accessibility | `protected` by default; `public` or `internal` through `AutoMediatorHostAttribute`. |
| Injection | One attached `IServiceProvider`; no generated constructor and no required mediator constructor parameter. |
| Handler resolution | Self routes call `this`; scoped/singleton handlers use one thread-safe lazy slot per host; transient handlers resolve per handler-method invocation. |
| Host construction | `ActivatorUtilities.CreateInstance`, followed by internal attach. `AddAutoMediator` is the supported production path. |
| Lifetimes | Hosts and handlers default to scoped. Host lifetime is configured on `AutoMediatorHostAttribute`; handler lifetime on `HandlerLifetimeAttribute`. |
| Captive dependencies | Singleton host to scoped external handler is an error. Singleton to singleton or per-dispatch transient is valid. |
| Send cardinality | Exactly one valid route. Zero is `CSAM001`; multiple is `CSAM002`; neither emits an executable route. |
| Non-generic commands | Handler must return `void`, `Task`, or `ValueTask`; results are not silently discarded. |
| Events | Zero-to-many handlers. Result-bearing handlers are allowed; results are discarded only after synchronous return or asynchronous completion. |
| Zero-handler events | No-op completed `ValueTask`. |
| Publish order | Sequential ordinal order by assembly-aware handler type identity, then stable method signature key. |
| Publish failures | Fail fast. The original exception/fault propagates and later handlers do not run. No `AggregateException` wrapper. |
| Cancellation | Pass the supplied token only to methods that declare it. Do not add synthetic cancellation checks. |
| Publishing commands | Reject with `ArgumentException`; commands must use `Send`. |
| Dynamic dispatch | Always supported through the fixed interface-based surface. There is no `AllowDynamicDispatch` option. |
| Test construction | Use a real `ServiceCollection`, register application dependencies, call `AddAutoMediator`, and resolve the host. Direct `new` remains unattached and fails clearly on dispatch. |
| Registration | One generated, assembly-local `AddAutoMediator`; `TryAdd` avoids duplicate descriptors on repeated calls. |

Do not pre-register a host with another factory. `TryAdd` respects that earlier
registration, but it cannot attach the provider; dispatch then fails with the
unattached-host exception. Custom earlier handler registrations are allowed and
follow normal `TryAdd` replacement behavior.

## 4. Public runtime API

The runtime project targets `netstandard2.0`. It needs explicit, pinned
dependencies for `ValueTask` and Microsoft DI abstractions; neither is assumed to
be inbox on that target.

```csharp
namespace CSteroid.AutoMediator
{
    public interface IMessage { }

    public interface ICommand : IMessage { }

    // Deliberately inherits ICommand so Publish can reject every command,
    // including one held through a non-generic ICommand reference.
    public interface ICommand<TResponse> : ICommand { }

    public interface IAutoMediatorService { }

    public interface IAutoMediatorHandler { }

    public enum AutoMediatorMemberAccessibility
    {
        Protected = 0,
        Public = 1,
        Internal = 2
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AutoMediatorHostAttribute : Attribute
    {
        public AutoMediatorMemberAccessibility Accessibility { get; set; } // Protected
        public ServiceLifetime Lifetime { get; set; }                      // Scoped
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class HandlerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HandlerLifetimeAttribute : Attribute
    {
        public HandlerLifetimeAttribute(ServiceLifetime lifetime);
        public ServiceLifetime Lifetime { get; }
    }
}
```

These types ship only in `CSteroid.AutoMediator.dll`. The generator does **not**
emit them through `RegisterPostInitializationOutput`; doing so would duplicate
the public contracts already supplied by the runtime reference.

### Runtime behavior contracts

These are added in the runtime-behavior milestone before v1 ships. They live in
the main runtime package, not an optional extensions package that generated code
would otherwise have to conditionally reference.

```csharp
namespace CSteroid.AutoMediator
{
    public delegate ValueTask AutoMediatorNext();
    public delegate ValueTask<TResponse> AutoMediatorNext<TResponse>();

    public interface IPipelineBehavior<in TMessage>
        where TMessage : IMessage
    {
        ValueTask Handle(
            TMessage message,
            CancellationToken cancellationToken,
            AutoMediatorNext next);
    }

    public interface IPipelineBehavior<in TMessage, TResponse>
        where TMessage : IMessage
    {
        ValueTask<TResponse> Handle(
            TMessage message,
            CancellationToken cancellationToken,
            AutoMediatorNext<TResponse> next);
    }
}
```

Send wraps its single terminal handler. Publish wraps the whole sequential
fan-out, so a logging behavior observes one publication rather than one behavior
chain per subscriber. Behaviors are resolved per dispatch in DI registration
order; the first registered behavior is outermost. Users register behaviors with
DI; the generator does not invent a second ordering attribute.

Runtime behavior registrations are not visible to generator lifetime analysis.
For singleton hosts, applications must not register scoped behaviors against the
root provider; `ValidateScopes` and runtime tests are the guardrails.

## 5. Repository and package layout

Follow the repository's existing generator naming and co-located test layout:

```text
src/CSteroid/
  CSteroid.AutoMediator/
    CSteroid.AutoMediator.csproj           netstandard2.0; packable runtime
    Messages.cs
    Attributes.cs
    PipelineBehaviors.cs                   added with runtime behaviors

  CSteroid.Generator.AutoMediator/
    CSteroid.Generator.AutoMediator.csproj netstandard2.0; nonpackable analyzer
    AutoMediatorGenerator.cs               provider wiring only
    Models.cs                              equatable DTOs
    EquatableArray.cs
    Discovery.cs                           syntax/symbol-to-DTO boundary
    RouteAnalyzer.cs                       global route invariants
    HostPlanner.cs                         host-specific routes and diagnostics
    HostEmitter.cs
    RegistrationEmitter.cs
    Diagnostics.cs                         static descriptors and DTO reporting

  CSteroid.Generator.AutoMediator.Tests/   net10.0 xUnit
  CSteroid.AutoMediator.IntegrationTests/  packed PackageReference consumer

examples/AutoMediator/Sample.WebApi/
```

There is no separate generator package, `CSteroid.AutoMediator.Extensions`
project, Core dependency, or Orm dependency in v1.

### One NuGet, two assemblies

`CSteroid.AutoMediator.csproj` is the pack owner and keeps normal build output.
The generator project has `OutputItemType=Analyzer`, `IncludeBuildOutput=false`,
and `IsPackable=false`. It uses Roslyn `4.10.0` and
`Microsoft.CodeAnalysis.Analyzers` `3.3.4`, matching this repository rather than
the original proposal's older Roslyn version.

The runtime project has build-order-only, private references to the generator and
`CSteroid.Generator.Shared`, plus package-local evaluation-time `None Pack=true`
items. The resulting package is:

```text
lib/netstandard2.0/CSteroid.AutoMediator.dll
analyzers/dotnet/cs/CSteroid.Generator.AutoMediator.dll
analyzers/dotnet/cs/CSteroid.Generator.Shared.dll
```

The generator references Shared only for genuinely reusable output helpers such
as normalized `SourceText`; it does not reuse the symbol-backed `ModelInfo`
pipeline. It does not binary-reference the runtime project. Runtime marker and
attribute identity is inspected by metadata name, avoiding a runtime/generator
project cycle.

The existing analyzer-only rules in `Directory.Build.targets` remain unchanged.
The hybrid payload is local to the runtime project until another package needs
the same pattern.

Add the runtime, generator, unit tests, and integration tests to
`src/CSteroid/CSteroid.slnx`; add the sample to `examples/Examples.slnx`.

## 6. Internal data model

Every incremental value is immutable and value-equatable. No model contains
`ISymbol`, `SyntaxNode`, `Compilation`, or `Location`. Strings use ordinal
equality. Collections use `EquatableArray<T>`, which wraps `ImmutableArray<T>`
with sequence equality and deterministic enumeration.

```csharp
internal enum MessageKind { Event, Command, CommandWithResponse }
internal enum ReturnShape
{
    Void,
    SynchronousResult,
    Task,
    TaskOfT,
    ValueTask,
    ValueTaskOfT
}

internal enum LifetimeModel { Scoped, Singleton, Transient }
internal enum MemberAccessibilityModel { Protected, Public, Internal }
internal enum HandlerResolution { Self, Lazy, PerDispatch }

internal sealed class TypeRef : IEquatable<TypeRef>
{
    internal string RouteKey { get; }       // Full metadata identity + assembly identity.
    internal string GlobalName { get; }     // Emission-ready global:: spelling.
    internal string AssemblyName { get; }
    internal bool IsValueType { get; }
}

internal sealed class TypeDeclarationModel : IEquatable<TypeDeclarationModel>
{
    internal string Name { get; }
    internal bool IsRecordClass { get; }
    internal bool IsPartial { get; }
    internal AccessibilityModel Accessibility { get; }
}

internal sealed class LocationInfo : IEquatable<LocationInfo>
{
    internal string FilePath { get; }
    internal int Start { get; }
    internal int Length { get; }
    internal int StartLine { get; }
    internal int StartCharacter { get; }
    internal int EndLine { get; }
    internal int EndCharacter { get; }
}

internal sealed class DiagnosticModel : IEquatable<DiagnosticModel>
{
    internal string Id { get; }
    internal string StableKey { get; }
    internal LocationInfo Location { get; }
    internal EquatableArray<string> MessageArguments { get; }
}

internal sealed class MessageModel : IEquatable<MessageModel>
{
    internal TypeRef MessageType { get; }
    internal MessageKind Kind { get; }
    internal TypeRef ResponseType { get; }
    internal LocationInfo DeclarationLocation { get; }
}

internal sealed class HandlerMethodModel : IEquatable<HandlerMethodModel>
{
    internal string MethodKey { get; }      // Type + method + closed parameter signature.
    internal TypeRef HandlerType { get; }
    internal string MethodName { get; }
    internal MessageModel Message { get; }
    internal ReturnShape ReturnShape { get; }
    internal TypeRef UnwrappedReturnType { get; }
    internal bool HasCancellationToken { get; }
    internal bool IsExplicitlyAttributed { get; }
    internal AccessibilityModel MethodAccessibility { get; }
    internal LifetimeModel HandlerLifetime { get; }
    internal bool HasExplicitHandlerLifetime { get; }
    internal LocationInfo MethodLocation { get; }
}

internal sealed class HostDeclarationModel : IEquatable<HostDeclarationModel>
{
    internal TypeRef HostType { get; }
    internal EquatableArray<TypeDeclarationModel> TypeChain { get; }
    internal MemberAccessibilityModel GeneratedAccessibility { get; }
    internal LifetimeModel HostLifetime { get; }
    internal bool HasExplicitHostLifetime { get; }
    internal bool HasUsablePublicConstructor { get; }
    internal EquatableArray<MemberModel> DeclaredMembers { get; }
    internal EquatableArray<MemberModel> AccessibleInheritedMembers { get; }
    internal LocationInfo DeclarationLocation { get; }
}

internal sealed class GlobalPlan : IEquatable<GlobalPlan>
{
    internal EquatableArray<RouteModel> ValidRoutes { get; }
    internal EquatableArray<DiagnosticModel> Diagnostics { get; }
}

internal sealed class HostPlan : IEquatable<HostPlan>
{
    internal HostModel Host { get; }
    internal string HintName { get; }
    internal EquatableArray<RoutePlan> Routes { get; }
    internal EquatableArray<HandlerSlotPlan> HandlerSlots { get; }
    internal EquatableArray<DiagnosticModel> Diagnostics { get; }
    internal bool CanEmit { get; }
}

internal sealed class RegistrationPlan : IEquatable<RegistrationPlan>
{
    internal EquatableArray<ServiceRegistrationModel> Services { get; }
    internal string ExtensionClassName { get; }
}
```

`AccessibilityModel`, `MemberModel`, `RouteModel`, `RoutePlan`,
`HandlerSlotPlan`, `HostModel`, and `ServiceRegistrationModel` follow the same
value-equatable DTO rule. In particular, generator models never reference the
public `AutoMediatorMemberAccessibility` or `ServiceLifetime` types, because the
generator deliberately has no runtime-project reference.

`TypeRef.RouteKey` includes assembly identity for metadata types; `GlobalName` is
used only for emission. Nullable annotations do not create distinct runtime
routes. Closed generic arguments are included even though open-generic message
and service declarations are rejected.

## 7. Discovery and incremental pipeline

### Discovery rules

1. **Messages:** inspect source class, record-class, and struct declarations with
   base lists. Include concrete, closed `IMessage` types even when unused.
   Classify `ICommand<TResponse>` before its non-generic base. Reject multiple
   different response contracts and open-generic messages. Abstract message
   declarations do not produce missing-command diagnostics.
2. **Concrete command uses:** inspect unqualified/`this` `Send` calls inside a
   host and calls whose receiver implements `IAutoMediatorService`. Record a
   concrete first-argument type and location. This catches a referenced concrete
   command without enumerating every metadata type. An interface-typed argument
   cannot reveal its runtime concrete type and retains the runtime no-route
   guard.
3. **Handlers:** one provider considers methods named `Handle` or carrying
   attributes. Keep a method when `[Handler]` is present or its containing class
   implements `IAutoMediatorHandler`. A method selected by both paths has one
   `MethodKey` and one route.
4. **Conventional helpers:** on marker classes, `Handle` methods with no
   message-shaped first parameter are ignored. Once a method is message-shaped,
   malformed signatures are diagnosed. Explicit `[Handler]` methods are always
   diagnosed when unsupported.
5. **Signature:** first parameter is a closed message; optional second parameter
   is `CancellationToken`; no other parameters. Reject generic/static methods,
   `ref`/`out`, pointer/function-pointer forms, and unsupported returns.
6. **Hosts:** discover marker classes and record classes, collect all partial
   declarations, merge members, and validate every containing declaration. Host
   configuration is only read on a marker host.
7. **Handler services:** external services must be concrete, non-generic,
   accessible to the host and registration, and DI-constructible. Self routes may
   call private/protected methods directly; other services support public,
   internal, or protected-internal methods in the same assembly.

### Provider graph

```text
message declarations ----\
handler declarations ------+--> GlobalPlan (routes + global diagnostics)
concrete command uses ----/             |
                                        +--> per-host HostPlan --> source + host diagnostics
host declarations --> HostSetModel -----+--> RegistrationPlan --> AddAutoMediator
```

In signature form:

```csharp
messages.Collect()
    .Combine(handlers.Collect())
    .Combine(commandUses.Collect())
    .Select((inputs, ct) => RouteAnalyzer.AnalyzeGlobal(...));

hostDeclarations.Collect()
    .Select((declarations, ct) => HostPlanner.MergeDeclarations(...))
    .Combine(globalPlan)
    .SelectMany((inputs, ct) => HostPlanner.BuildHosts(...));

hostSet.Combine(globalPlan)
    .Select((inputs, ct) => RegistrationPlanner.Build(...));
```

Global command cardinality and malformed-route diagnostics are reported from the
`GlobalPlan` output, including when there are zero hosts. They are never copied
into every host. Host accessibility, self-route selection, lifetime
compatibility, and member collisions are host-plan concerns.

A host-local edit does not rerun global route analysis. It can rebuild the host
set, but equal unaffected `HostPlan` values stop downstream emission. A real
route-table change intentionally invalidates every host because every host has
the same fixed dispatch surface and direct route cases. Registration changes
only when its derived services, lifetimes, or attach requirements change.

Use `.WithTrackingName(...)` at each discovery, analysis, planning, and emission
boundary. Do not put a mutable `Dictionary<,>` into a pipeline value; use a
dictionary locally during analysis, then convert to sorted equatable arrays.

### Route analysis ownership

`RouteAnalyzer` is the single owner of message classification, return matching,
and global command cardinality. `HostPlanner` is the single owner of host
accessibility, self-versus-external resolution, lifetime checks, and collisions.
Emitters render plans and do not revalidate them.

The command universe is the union of:

- concrete closed source command declarations;
- command types known from handler candidates;
- concrete command types observed at recognized `Send` call sites.

Invalid command routes are omitted. Valid unrelated hosts and routes still emit.
This avoids both arbitrary handler selection and a cascade of duplicate-member or
missing-overload compiler errors.

## 8. Generated host shape

The host partial contains no runtime route table, reflection invocation,
`dynamic`, or separate context object. The fixed methods select exact-type route
helpers. Every valid host has the same three-method surface, subject to configured
accessibility.

```csharp
// <auto-generated />
#nullable enable

partial class MailService
{
    private global::System.IServiceProvider? __am_sp;
    private global::System.Lazy<global::App.OrderService>? __am_orderService;
    private global::System.Lazy<global::App.AuditService>? __am_auditService;

    internal void __AutoMediator_Attach(global::System.IServiceProvider services)
    {
        if (services is null)
            throw new global::System.ArgumentNullException(nameof(services));
        if (__am_sp is not null)
            throw new global::System.InvalidOperationException(
                "This AutoMediator host is already attached.");

        __am_orderService = new global::System.Lazy<global::App.OrderService>(
            () => global::Microsoft.Extensions.DependencyInjection
                .ServiceProviderServiceExtensions
                .GetRequiredService<global::App.OrderService>(services));
        __am_auditService = new global::System.Lazy<global::App.AuditService>(
            () => global::Microsoft.Extensions.DependencyInjection
                .ServiceProviderServiceExtensions
                .GetRequiredService<global::App.AuditService>(services));

        // Publish attached state only after every lazy slot is initialized.
        __am_sp = services;
    }

    // Send, Publish, and route helpers follow.
}
```

`Lazy<T>` uses execution-and-publication mode, so concurrent calls through a
singleton host do not resolve a singleton/scoped slot more than once. Attach is
single-shot and occurs before DI publishes the host; it is not a concurrent
reconfiguration API. Transient handlers deliberately have no lazy field.

### Send with response

```csharp
protected global::System.Threading.Tasks.ValueTask<TResponse> Send<TResponse>(
    global::CSteroid.AutoMediator.ICommand<TResponse> command,
    global::System.Threading.CancellationToken ct = default)
{
    if (command is null)
        throw new global::System.ArgumentNullException(nameof(command));
    if (__am_sp is null)
        throw new global::System.InvalidOperationException(
            "The AutoMediator host is not attached. Resolve it through AddAutoMediator.");

    if (command.GetType() == typeof(global::App.CreateOrder))
    {
        return __AM_Send_CreateOrder<TResponse>(
            (global::App.CreateOrder)(object)command, ct);
    }

    throw new global::System.InvalidOperationException(
        "No exact AutoMediator command route for " + command.GetType().FullName + ".");
}

private async global::System.Threading.Tasks.ValueTask<TResponse>
    __AM_Send_CreateOrder<TResponse>(
        global::App.CreateOrder command,
        global::System.Threading.CancellationToken ct)
{
    global::App.OrderId result = await __am_orderService!.Value
        .Handle(command, ct)
        .ConfigureAwait(false);
    return (TResponse)(object)result;
}
```

The response cast is safe because discovery proved that the concrete command has
exactly one `ICommand<TResponse>` contract and the handler's unwrapped return is
exactly that type. A value-type response can box once; that is an accepted v1
tradeoff for a fixed generic surface.

A synchronous `TResponse` handler returns
`new ValueTask<TResponse>((TResponse)(object)handler.Handle(...))`. `Task<T>` and
`ValueTask<T>` are awaited exactly once. No route uses covariance on `Task<T>`.

### Send without response

```csharp
protected global::System.Threading.Tasks.ValueTask Send(
    global::CSteroid.AutoMediator.ICommand command,
    global::System.Threading.CancellationToken ct = default)
{
    if (command is null)
        throw new global::System.ArgumentNullException(nameof(command));
    if (__am_sp is null)
        throw new global::System.InvalidOperationException(
            "The AutoMediator host is not attached. Resolve it through AddAutoMediator.");

    if (command.GetType() == typeof(global::App.DeleteOrder))
    {
        __am_orderService!.Value.Handle((global::App.DeleteOrder)(object)command);
        return default;
    }

    // This also rejects a response-bearing command erased to ICommand.
    throw new global::System.InvalidOperationException(
        "No exact non-generic AutoMediator command route for " +
        command.GetType().FullName + ".");
}
```

For non-generic commands, `Task` maps to `new ValueTask(handler.Handle(...))` and
`ValueTask` returns directly. Result-bearing handlers are invalid rather than
having their response discarded.

### Publish

```csharp
protected global::System.Threading.Tasks.ValueTask Publish(
    global::CSteroid.AutoMediator.IMessage message,
    global::System.Threading.CancellationToken ct = default)
{
    if (message is null)
        throw new global::System.ArgumentNullException(nameof(message));
    if (__am_sp is null)
        throw new global::System.InvalidOperationException(
            "The AutoMediator host is not attached. Resolve it through AddAutoMediator.");
    if (message is global::CSteroid.AutoMediator.ICommand)
        throw new global::System.ArgumentException(
            "Commands must be dispatched with Send.", nameof(message));

    if (message.GetType() == typeof(global::App.OrderCreated))
    {
        return __AM_Publish_OrderCreated(
            (global::App.OrderCreated)(object)message, ct);
    }

    // Zero-handler, unknown, and derived-only event cases are deliberate no-ops.
    return default;
}

private async global::System.Threading.Tasks.ValueTask __AM_Publish_OrderCreated(
    global::App.OrderCreated message,
    global::System.Threading.CancellationToken ct)
{
    // Ordinal route order: AuditService.Record, then OrderService.Handle.
    __am_auditService!.Value.Record(message); // void: invoke, never await.
    await __am_orderService!.Value.Handle(message, ct).ConfigureAwait(false);
}
```

For all-synchronous publication the helper is non-`async` and returns
`default(ValueTask)`, avoiding CS1998. `Task<T>` and `ValueTask<T>` event results
are awaited once and discarded; synchronous `T` results are discarded by an
ordinary invocation statement. Every fault propagates and stops later handlers.

A self route substitutes `this.Method(...)` or a direct private method call and
emits no lazy slot or `GetRequiredService` for the host. A transient external
handler instead emits `GetRequiredService<T>(__am_sp)` at each handler-method
invocation.

### Runtime behavior insertion

When behaviors ship, the route terminal remains the direct handler/fan-out code
above. A generated wrapper resolves the corresponding closed behavior interfaces,
builds the delegate chain in reverse registration order, and invokes the first
behavior. No behaviors means the terminal runs directly. Send and Publish keep
their public signatures and the attach model does not change.

## 9. Generated DI registration

Registration is emitted once per assembly from the same valid host/route plans.
It is internal and assembly-unique, avoiding public extension-type collisions
when a process references several generated assemblies.

```csharp
// <auto-generated />
#nullable enable

namespace Microsoft.Extensions.DependencyInjection
{
    [global::System.CodeDom.Compiler.GeneratedCode(
        "CSteroid.Generator.AutoMediator", "1.0.0")]
    internal static class CSteroidAutoMediatorRegistration_MyAssembly
    {
        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection
            AddAutoMediator(
                this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            if (services is null)
                throw new global::System.ArgumentNullException(nameof(services));

            global::Microsoft.Extensions.DependencyInjection.Extensions
                .ServiceCollectionDescriptorExtensions
                .TryAddScoped<global::App.OrderService>(services);
            global::Microsoft.Extensions.DependencyInjection.Extensions
                .ServiceCollectionDescriptorExtensions
                .TryAddScoped<global::App.AuditService>(services);

            global::Microsoft.Extensions.DependencyInjection.Extensions
                .ServiceCollectionDescriptorExtensions
                .TryAddScoped<global::App.MailService>(services, sp =>
                {
                    var host = global::Microsoft.Extensions.DependencyInjection
                        .ActivatorUtilities.CreateInstance<global::App.MailService>(sp);
                    host.__AutoMediator_Attach(sp);
                    return host;
                });

            return services;
        }
    }
}
```

Select `TryAddSingleton`, `TryAddScoped`, or `TryAddTransient` from the derived
lifetime model. All extension calls are fully qualified; generated code never
relies on a consumer's `using` directives or global usings.

A host-plus-handler class has one registration, using the host factory so every
resolved instance is attached. If both lifetime attributes are explicitly present
with different values, report `CSAM013`; otherwise one explicitly configured
lifetime applies to the whole class.

Registration is derived only from valid hosts and the routes reachable from
those hosts. Removing a handler method therefore removes its route, any now
unused lazy field, and its registration when no valid host route still needs the
service. The generator is stateless; stale artifacts are not retained.

## 10. Diagnostics

Descriptors are static, unique, and parameterized. `LocationInfo` is converted
back to `Location` only in source-output callbacks. Diagnostics are sorted by ID,
stable key, and location with ordinal comparisons. Cancellation is not caught and
converted into a generator error.

| ID | Severity | Location | Rule and emission behavior |
| --- | --- | --- | --- |
| CSAM001 | Error | Command declaration, concrete Send use, or best available candidate | No valid handler for a concrete closed command. Report even with no hosts; omit the route. |
| CSAM002 | Error | Second and subsequent handler methods in ordinal order | More than one valid command handler. Omit all routes for that command. |
| CSAM003 | Error | Offending host or containing declaration | Host and every containing type must be partial. Skip host source and registration. |
| CSAM004 | Error | Host declaration | Static, abstract, struct, open-generic host, generic container, inaccessible type, or no usable public constructor. Skip host source and registration. |
| CSAM005 | Warning | Handler method | Unsupported signature other than CSAM008/CSAM010. Ignore the candidate. |
| CSAM006 | Error | Handler method; host-specific message | Method/type is not accessible from that host. Omit only that host route; private/protected self routes remain valid. |
| CSAM007 | Error | Singleton host or lifetime attribute | Singleton host would capture a scoped external handler. Omit that host route. Transient per-dispatch routes do not trigger this rule. |
| CSAM008 | Error | Handler return type or method | Wrong unwrapped `ICommand<T>` response, missing required response, or result on non-generic command. Ignore the candidate globally. |
| CSAM009 | Error | Colliding source/inherited member | Accessible source or inherited `Send`/`Publish`, or a reserved generated helper/attach/field name. Skip the host partial and registration. |
| CSAM010 | Warning | Handler method | Generic, static, ref/out, or otherwise specifically unsupported method form. Ignore the candidate. |
| CSAM011 | Error | Handler class | Abstract, static, open-generic, inaccessible, or non-DI-constructible external handler service. Its external routes are invalid. |
| CSAM012 | Error | Message declaration or handler parameter | Multiple response contracts, open-generic message, or exact dispatch can never match an abstract/interface route. Exclude the message/route. |
| CSAM013 | Error | Attribute application | Undefined enum/lifetime value, conflicting host/handler lifetime on one class, or host configuration on a non-host. |

For `CSAM009`, reserve generated implementation names and conservatively reject
any source-declared or accessible inherited member named `Send` or `Publish`.
An inherited overload can otherwise be a better match than the generated
interface-based method and silently hijack dispatch; normal C# hiding rules are
not a sufficient check.

Suppress `CSAM001` when `CSAM008`, `CSAM011`, or `CSAM012` already reports an
error that explains the same missing route. If only warning-level invalid
candidates remain, still report `CSAM001`; a warning must never make a required
command route appear valid.

A code fix for `CSAM003` is post-v1 and requires a separate Workspaces-referencing
code-fix project. It is not part of the generator package.

## 11. Testing strategy

### Unit and snapshot tests

Reuse the existing xUnit/Roslyn harness for basic compilation and runtime tests,
but add strict result APIs rather than relying only on substring checks.

- Verify snapshots for hint names and generated source: Send shapes, Publish
  fan-out, self host, nested host, record host, global namespace, accessibility,
  lifetimes, and behaviors.
- Compile generated output and assert no added errors. Always assert a required
  hint exists before checking its contents; the current helper returns an empty
  string for a missing hint.
- Exercise every return shape for generic Send, non-generic Send, and Publish.
  Include the synchronous-void Publish regression test and await-once checks for
  every `ValueTask`.
- Test exact-type guards with derived messages, null arguments, unknown commands,
  erased response commands, commands passed to Publish, and zero-handler events.
- Test marker plus `[Handler]` de-duplication, overload ordering, conventional
  helper ignoring, explicit malformed methods, and all diagnostic IDs,
  severities, messages, and source locations.
- Test source and inherited `Send`/`Publish` collisions, partial containers,
  stable hint names, global qualification, nullable output, and generated-code
  metadata.
- Add DTO equality tests, including `EquatableArray<T>` default/empty behavior
  and assembly-aware `TypeRef` identity.

### Real DI runtime tests

Use `ServiceCollection` with real scopes:

- Existing host constructor dependencies are supplied without a mediator
  constructor parameter.
- Lazy scoped/singleton handlers resolve at most once per host; DI still controls
  sharing across hosts and scopes.
- Transient handlers resolve per handler-method invocation and are never cached.
- Singleton-to-scoped is rejected; singleton-to-singleton/transient runs.
- Host-handler classes have one attached registration and self routes never
  re-enter the host factory.
- Publish order is sequential and deterministic; a slow first handler completes
  before the second starts; failure stops later handlers.
- Repeated `AddAutoMediator` does not duplicate descriptors; pre-registered
  custom handlers are respected.
- Unattached use and double attach fail with the documented exceptions.
- Runtime behaviors wrap Send once and Publish once, preserve registration order,
  propagate results/faults, and add no behavior-specific public dispatch API.

### Incremental and determinism tests

Extend the harness to retain the updated `CSharpGeneratorDriver` and expose
`GeneratorDriverRunResult` and tracked steps:

- An unrelated syntax edit leaves outputs cached or unchanged according to the
  declared step contract; do not claim every step must be `Cached`.
- A host-local edit does not rerun global route analysis and only changes the
  affected host plan; registration remains equal unless effective registration
  inputs changed.
- A route edit intentionally invalidates all host plans; equal registration is
  retained when possible.
- Removing a handler removes its field, call, switch arm, behavior terminal, and
  now-unused registration.
- Global diagnostics appear once with many hosts and still appear with no hosts.
- Repeated runs and shuffled syntax-tree inputs produce byte-identical UTF-8/LF
  output and identical diagnostic ordering.

### Packed consumer integration test

A ProjectReference sample does not validate the NuGet layout. The integration
fixture must:

1. Pack `CSteroid.AutoMediator` in Release.
2. Inspect the `.nupkg` entries and dependencies.
3. Create a temporary consumer with only a `PackageReference` to the produced
   package and no analyzer/runtime project references.
4. Restore, build, and run a scoped Send/Publish/self-host/behavior scenario.
5. Prove the analyzer loaded from `analyzers/dotnet/cs`, runtime types came from
   `lib/netstandard2.0`, transitive dependencies restored, and public attributes
   were not duplicated.

The Web API sample additionally enables `EmitCompilerGeneratedFiles` for
inspection and provides a small endpoint-level self-check in CI.

## 12. Milestones and acceptance criteria

| # | Deliverable | Done when |
| --- | --- | --- |
| 1 | Runtime contracts, generator skeleton, hybrid package | Both projects target netstandard2.0; solution builds; Release pack contains exactly the intended lib/analyzer payloads and no duplicate attributes. |
| 2 | Equatable discovery and global route validation | Full signature matrix and DTO equality pass; no-host and unused-command diagnostics work; no Roslyn objects remain in models. |
| 3 | Send vertical slice | Generic and non-generic Send compile and run for sync/Task/ValueTask, exact response matching, unknown routes, and single self route. |
| 4 | Publish and removal | Fan-out order, sync/async result handling, fail-fast behavior, zero-handler no-op, and stale-field/call/registration removal tests pass. |
| 5 | DI, attach, lifetimes, nested hosts | Real-scope tests pass; host-handler has one registration; singleton/scoped mismatch and all host-shape rules are enforced. |
| 6 | Diagnostics and incrementality hardening | CSAM001-CSAM013 each have severity/location tests; inherited collision regression passes; retained-driver and byte-determinism tests pass. |
| 7 | Runtime pipeline behaviors | Send and one-Publish-chain semantics, ordering, lifetimes, exceptions, cancellation, snapshots, and real-DI tests pass without changing caller usage. |
| 8 | Sample and shipping validation | Sample.WebApi self-check passes; isolated nupkg consumer restores/builds/runs; full solution build/test and pack pass in CI. |
| 9 | Cross-assembly handlers (post-core) | Versioned manifests import public/internals-visible handlers, command conflicts are diagnosed, root registration imports external handlers, and a multi-project package consumer runs. |
| 10 | V2 backlog | Compile-time behaviors, base/interface route matching, dynamic AOT-safe construction, and CSAM003 code fix are designed separately. |

Milestones 1-8 form the v1 release plan. Milestone 9 is not required to claim
single-assembly v1 correctness.

## 13. Cross-assembly design path

Do not claim that `MetadataReferencesProvider` alone can inspect assembly
attributes: it provides reference objects, not `IAssemblySymbol` attribute data.
The later implementation must use a transform with a `Compilation`, inspect
referenced assembly manifests, and immediately convert them to equatable DTOs.
This recomputes on compilation changes; equal manifest DTOs must prevent
unnecessary downstream emission.

Each handler-producing assembly can emit a versioned, internal assembly-manifest
attribute and route entries. Keeping the manifest attribute internal and
assembly-local avoids turning a serialization format into public runtime API.
The consuming root generator merges local and imported routes, applies the same
command-cardinality and accessibility rules, and registers externally accessible
handlers in the root `AddAutoMediator` plan.

That milestone must explicitly specify manifest versioning, duplicate routes
across assemblies, `InternalsVisibleTo`, and registration ownership. The initial
cross-assembly scope should require a host in the composition-root assembly;
a library-only host with an external composition root needs a separate public
registration design rather than accumulating ambiguous extension methods.

## 14. Synthesis decision

The arena compared direct host dispatch (candidate A) with a private per-host
dispatch context (candidate B). The Kimi cross-judge scored A 28/30 and B 25/30
and selected A. Both candidates independently converged on a fixed generic/
interface-based host surface, attach-based construction, exact runtime-type
checks, and sequential fail-fast Publish.

The synthesized design uses A's direct dispatch, internal assembly-safe
registration, transient handling, and source-plus-call-site command discovery.
It grafts B's finer `GlobalPlan` to `HostPlan`/`RegistrationPlan` pipeline,
assembly-aware type identity, and diagnostic cascade suppression. B's additional
private context was rejected because it adds a type and forwarding layer without
improving the caller contract or an invariant that direct generated helpers do
not already own.

The cross-judge also found four defects now corrected here: generator-local
configuration enums replace unavailable runtime enum references; inherited
`Send`/`Publish` members participate in collision checks; registration is
internal and assembly-unique; singleton hosts may use per-dispatch transient
handlers.

### Red-flag screen

- **Interface depth:** three host operations hide route selection, normalization,
  resolution, publication, and behavior composition. Public options expose only
  caller policy, not compiler stages.
- **Split ownership:** global route invariants, host planning, and runtime DI
  lifetimes each have one owner. Emitters do not maintain parallel route lists.
- **Two ways to do one task:** there is one construction path and one dispatch
  surface. The two required handler discovery forms feed one de-duplicated route
  stream.
- **Importable internals:** generated registration is assembly-local; attach is
  internal; fields and helpers are private; generator DTOs are analyzer-only.
- **Hand-synced lists:** routes, lazy slots, self calls, and registrations derive
  from the same validated plans.
- **Information leakage:** no Roslyn type, route table, manifest, or transport
  representation enters the caller-facing runtime API.

## 15. Tradeoffs and remaining risks

- We accept runtime no-route failures for interface-typed or otherwise unknown
  concrete commands in exchange for a fixed three-method surface and support for
  interface-held messages.
- We accept a checked generic response cast and possible one-time boxing of
  value-type responses in exchange for avoiding per-message host overloads.
- We accept strict errors for every concrete source command without a handler,
  even if unused. If contract-heavy assemblies make this too noisy, relax it only
  through a deliberate later policy, not silent warning downgrade.
- We accept `ActivatorUtilities` and attached provider state in exchange for
  preserving existing constructors. This is not a fully AOT-safe construction
  story; generated constructor-specific factories are future work.
- We accept per-host lazy scoped/singleton fields in exchange for avoiding
  repeated resolution; transient services remain per-dispatch to preserve DI
  semantics.
- We accept same-assembly v1 routing in exchange for avoiding a speculative
  cross-assembly manifest and public registration protocol.

## 16. Next implementation step

Implement milestone 1 as the package and test-harness skeleton, then build the
equatable discovery models and `RouteAnalyzer` against the caller examples before
writing the first host emitter.
