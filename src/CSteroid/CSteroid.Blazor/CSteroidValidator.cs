using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSteroid.Blazor.Internal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CSteroid.Blazor
{
    /// <summary>
    /// Connects a caller-owned CSteroid validator to the cascading EditContext.
    /// Only Error failures enter Blazor's validation message store.
    /// </summary>
    public sealed class CSteroidValidator<TModel> : ComponentBase, IDisposable
        where TModel : class
    {
        private EditContext? _attachedContext;
        private IValidator<TModel>? _attachedValidator;
        private ValidationMessageStore? _messages;
        private ValidationRun? _latestRun;
        private bool _suppressOwnValidationRequest;
        private bool _disposed;

        [CascadingParameter]
        private EditContext? CurrentEditContext { get; set; }

        /// <summary>The validator to use. The caller retains ownership and disposal.</summary>
        [Parameter, EditorRequired]
        public IValidator<TModel> Validator { get; set; } = default!;

        /// <summary>Receives the latest complete result after Blazor messages are updated.</summary>
        [Parameter]
        public EventCallback<ValidationResult> ValidationCompleted { get; set; }

        /// <summary>
        /// Awaits a fresh whole-model validation and returns whether the EditContext
        /// currently has no validation messages from any provider. A superseded run returns false.
        /// </summary>
        public async Task<bool> ValidateAsync()
        {
            ThrowIfUnavailable();

            ValidationRun? run = null;
            await InvokeAsync(() =>
            {
                ThrowIfUnavailable();
                run = BeginRun(awaitedByCaller: true);
                _suppressOwnValidationRequest = true;
                try
                {
                    run.Context.Validate();
                }
                finally
                {
                    _suppressOwnValidationRequest = false;
                }
            });

            var validationRun = run ?? throw new InvalidOperationException("The validation run could not be started.");
            try
            {
                var committed = await validationRun.Completion.WaitAsync(validationRun.Token).ConfigureAwait(false);
                if (!committed)
                    return false;

                var isValid = false;
                await InvokeAsync(() =>
                {
                    isValid = IsCurrent(validationRun) && !validationRun.Context.GetValidationMessages().Any();
                });
                return isValid;
            }
            catch (OperationCanceledException) when (validationRun.Token.IsCancellationRequested)
            {
                return false;
            }
        }

        /// <summary>Detaches validation without disposing the caller-owned validator.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Detach();
        }

        protected override void OnParametersSet()
        {
            if (_disposed)
                return;

            var context = CurrentEditContext
                ?? throw new InvalidOperationException($"{nameof(CSteroidValidator<TModel>)} requires a cascading {nameof(EditContext)}.");
            var validator = Validator
                ?? throw new InvalidOperationException($"{nameof(CSteroidValidator<TModel>)} requires a {nameof(Validator)} parameter.");

            if (context.Model is not TModel)
            {
                throw new InvalidOperationException(
                    $"{nameof(CSteroidValidator<TModel>)} expects EditContext.Model to be assignable to {typeof(TModel).FullName}, " +
                    $"but found {context.Model.GetType().FullName}.");
            }

            if (ReferenceEquals(context, _attachedContext) && ReferenceEquals(validator, _attachedValidator))
                return;

            Detach();

            _attachedContext = context;
            _attachedValidator = validator;
            _messages = new ValidationMessageStore(context);
            context.OnFieldChanged += HandleFieldChanged;
            context.OnValidationRequested += HandleValidationRequested;
        }

        private void HandleFieldChanged(object? sender, FieldChangedEventArgs args)
            => BeginRun(awaitedByCaller: false);

        private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
        {
            if (!_suppressOwnValidationRequest)
                BeginRun(awaitedByCaller: false);
        }

        private ValidationRun BeginRun(bool awaitedByCaller)
        {
            var context = _attachedContext ?? throw new InvalidOperationException("The validator is not attached to an EditContext.");
            var validator = _attachedValidator ?? throw new InvalidOperationException("The validator is not configured.");
            var messages = _messages ?? throw new InvalidOperationException("The validation message store is not initialized.");

            SupersedeLatestRun();

            var run = new ValidationRun(context, validator, (TModel)context.Model, messages, awaitedByCaller);
            _latestRun = run;
            run.Completion = ExecuteRunAsync(run);
            _ = ObserveRunAsync(run).ContinueWith(
                task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return run;
        }

        private async Task<bool> ExecuteRunAsync(ValidationRun run)
        {
            try
            {
                var result = run.Validator is IAsyncValidator<TModel> asyncValidator
                    ? await asyncValidator.ValidateAsync(run.Model, run.Token).ConfigureAwait(false)
                    : run.Validator.Validate(run.Model);

                if (result is null)
                    throw new InvalidOperationException($"{run.Validator.GetType().FullName} returned a null validation result.");

                var committed = false;
                await InvokeAsync(async () =>
                {
                    if (!IsCurrent(run))
                        return;

                    run.Messages.Clear();
                    foreach (var failure in result.Failures.Where(f => f.Severity == ValidationSeverity.Error))
                    {
                        var field = ValidationPathResolver.Resolve(run.Model, failure.PropertyName);
                        run.Messages.Add(field, failure.ErrorMessage);
                    }

                    run.Context.NotifyValidationStateChanged();
                    if (IsCurrent(run))
                        await ValidationCompleted.InvokeAsync(result);
                    committed = IsCurrent(run);
                });
                return committed;
            }
            catch (OperationCanceledException) when (run.Token.IsCancellationRequested)
            {
                return false;
            }
        }

        private async Task ObserveRunAsync(ValidationRun run)
        {
            try
            {
                await run.Completion.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (!run.AwaitedByCaller)
                {
                    await InvokeAsync(async () =>
                    {
                        if (IsCurrent(run))
                            await DispatchExceptionAsync(exception);
                    });
                }
            }
            finally
            {
                run.Cancellation.Dispose();
            }
        }

        private void SupersedeLatestRun()
        {
            var previous = _latestRun;
            _latestRun = null;

            if (previous is not null && !previous.Completion.IsCompleted)
                previous.Cancellation.Cancel();
        }

        private bool IsCurrent(ValidationRun run)
        {
            return !_disposed
                && ReferenceEquals(_latestRun, run)
                && ReferenceEquals(_attachedContext, run.Context)
                && ReferenceEquals(_attachedValidator, run.Validator);
        }

        private void Detach()
        {
            SupersedeLatestRun();

            if (_attachedContext is not null)
            {
                _attachedContext.OnFieldChanged -= HandleFieldChanged;
                _attachedContext.OnValidationRequested -= HandleValidationRequested;
                _messages?.Clear();
                _attachedContext.NotifyValidationStateChanged();
            }

            _attachedContext = null;
            _attachedValidator = null;
            _messages = null;
        }

        private void ThrowIfUnavailable()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
            if (_attachedContext is null)
                throw new InvalidOperationException("The validator is not attached to an EditContext.");
        }

        private sealed class ValidationRun
        {
            internal ValidationRun(
                EditContext context,
                IValidator<TModel> validator,
                TModel model,
                ValidationMessageStore messages,
                bool awaitedByCaller)
            {
                Context = context;
                Validator = validator;
                Model = model;
                Messages = messages;
                AwaitedByCaller = awaitedByCaller;
                Cancellation = new CancellationTokenSource();
                Token = Cancellation.Token;
            }

            internal EditContext Context { get; }
            internal IValidator<TModel> Validator { get; }
            internal TModel Model { get; }
            internal ValidationMessageStore Messages { get; }
            internal bool AwaitedByCaller { get; }
            internal CancellationTokenSource Cancellation { get; }
            internal CancellationToken Token { get; }
            internal Task<bool> Completion { get; set; } = Task.FromResult(false);
        }
    }
}
