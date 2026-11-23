# Blazor EditContext validation

## Problem

CSteroid validates a whole model and returns Error, Warning, and Info failures with structural property paths. Blazor's `EditContext` has synchronous validation events and treats every `ValidationMessageStore` message as invalidating. The adapter therefore owns more than event forwarding: sync/async dispatch, awaited submission, stale-result suppression, path-to-field translation, severity projection, and cleanup. Core and the generators remain UI-agnostic.

## Usage (caller's view)

Place one component inside `EditForm`, supply the caller-owned validator, and use `OnSubmit` with the component's awaited gate for both sync and async validators.

```razor
<EditForm Model="_model" OnSubmit="SubmitAsync">
    <CSteroidValidator TModel="Customer"
                       Validator="_validator"
                       @ref="_validation" />
    <InputText @bind-Value="_model.Name" />
    <ValidationMessage For="@(() => _model.Name)" />
    <button type="submit">Save</button>
</EditForm>

@code {
    private readonly Customer _model = new();
    private readonly CustomerValidator _validator = new();
    private CSteroidValidator<Customer> _validation = default!;

    private async Task SubmitAsync(EditContext context)
    {
        if (!await _validation.ValidateAsync())
            return;

        await SaveCustomer(_model);
    }
}
```

`OnValidSubmit` and the immediate result of `EditContext.Validate()` are not async submission gates. `ValidationCompleted` optionally receives the latest complete `ValidationResult`, including Warning and Info failures, after Blazor error messages have been updated.

## Shape

```csharp
public sealed class CSteroidValidator<TModel> : ComponentBase, IDisposable
    where TModel : class
{
    [Parameter, EditorRequired]
    public IValidator<TModel> Validator { get; set; }

    [Parameter]
    public EventCallback<ValidationResult> ValidationCompleted { get; set; }

    public Task<bool> ValidateAsync() => throw new NotImplementedException();
    public void Dispose() => throw new NotImplementedException();
}
```

The component is the sole owner of its event subscriptions, one Error-only `ValidationMessageStore`, current run, and cancellation source. An internal `ValidationPathResolver` owns the only path grammar. Run state is private; no registry, service provider, public adapter, store, or cancellation protocol escapes.

Both field and form events validate the whole model because cross-property rules can change messages on unrelated fields. Sync validators commit synchronously when the renderer is available so ordinary `EditContext.Validate()` remains useful. Async validators use `IAsyncValidator<TModel>`; their throwing sync method is never called. Only the latest run may replace messages or invoke the completion callback.

`ValidateAsync` starts one run before issuing the normal full-form request. It suppresses only its own duplicate request event, awaits that run, returns false if a newer edit/request superseded it, and otherwise returns whether any EditContext messages remain. Foreign stores are preserved and included in this final check.

The public surface hides event wiring, execution, message projection, and ordering behind one component and one method. The class constraint encodes Blazor's reference-type owner requirement; value-type nested owners fall back to the root validation summary rather than producing invalid `FieldIdentifier` instances.

## Synthesis decision

The component candidate became the base for its cascading lifecycle, renderer-dispatcher ownership, automatic disposal, severity callback, and Blazor-compatible indexed fields. The cross-judge found its unclaimed-form-run assumption invalid and recommended the attachment candidate's submit protocol as a graft. The graft replaces the claim/retry state completely: self-started run, own-event suppression, and conservative false on supersession. The attachment ownership model, pull-only advisory API, and root-based terminal-index mapping were rejected.

## Tradeoffs accepted

- We accept `OnSubmit` plus `@ref` for sync and async forms in exchange for one submission recipe that cannot silently become unsafe when rules become async.
- We accept whole-model validation on every field notification in exchange for correct cross-property and nested results.
- We accept an optional completion callback for Warning/Info in exchange for preserving CSteroid validity semantics without a parallel severity-aware display framework.
- We accept reflection and collection traversal during path resolution in exchange for correct runtime owner identity without changing Core metadata.
- We accept root-summary fallback for malformed or currently unresolvable paths in exchange for never dropping an Error.
- We accept that the gate cannot await unrelated async validators behind foreign synchronous events; it observes their committed messages only.

## Alternatives considered

A component-free `EditContext` attachment was the main alternative. It has a small, deep API and a sound submit pipeline, but makes consumers own context construction, attachment disposal, and advisory rerendering. Its terminal indexed-path convention also disagrees with Blazor's `FieldIdentifier.Create` identity. Registry-resolving or parameterless components were rejected because registry lookup does not activate internal generated validators and hides exact-type, last-write-wins ownership.

## Open questions and risks

- Is callback-based Warning/Info rendering sufficient, or should a later release add an optional severity-aware display component?
- Should unresolved production paths gain an opt-in diagnostic while retaining summary fallback?
- Should documentation make the foreign-async-validator limitation more prominent for forms mixing validation frameworks?

## Next implementation step

Implement the component and resolver, then prove real `EditForm` submission, controlled async completion, cross-field updates, path identity, foreign-store composition, and disposal through bUnit tests.
