using Bunit;
using CSteroid;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CSteroid.Blazor.Tests
{
    public sealed class CSteroidValidatorTests : BunitContext
    {
        [Fact]
        public async Task Sync_field_and_form_requests_validate_the_whole_model()
        {
            var model = new TestModel();
            var editContext = new EditContext(model);
            var validator = new StubValidator
            {
                Result = Result(Failure(nameof(TestModel.Other), "cross-field"))
            };
            var cut = RenderValidator(editContext, validator);
            var nameField = FieldIdentifier.Create(() => model.Name);
            var otherField = FieldIdentifier.Create(() => model.Other);

            await cut.InvokeAsync(() => editContext.NotifyFieldChanged(nameField));

            Assert.Equal(new[] { "cross-field" }, Messages(editContext, otherField));
            Assert.Empty(Messages(editContext, nameField));

            validator.Result = Result(Failure(nameof(TestModel.Name), "name"));
            var isValid = true;
            await cut.InvokeAsync(() => isValid = editContext.Validate());

            Assert.False(isValid);
            Assert.Equal(new[] { "name" }, Messages(editContext, nameField));
            Assert.Empty(Messages(editContext, otherField));
            Assert.Equal(2, validator.CallCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task EditForm_OnSubmit_runs_once_and_returns_overall_validity(bool expectedValid)
        {
            var model = new TestModel();
            var validator = new StubValidator
            {
                Result = expectedValid
                    ? ValidationResult.Success
                    : Result(Failure(nameof(TestModel.Name), "invalid"))
            };
            bool? actualValid = null;
            var form = RenderForm(model, validator, async (validation, _) =>
            {
                actualValid = await validation.ValidateAsync();
            });

            await form.Find("form").SubmitAsync();

            Assert.True(actualValid.HasValue);
            Assert.Equal(expectedValid, actualValid.Value);
            Assert.Equal(1, validator.CallCount);
            var editContext = form.Instance.EditContext!;
            Assert.Equal(
                expectedValid ? 0 : 1,
                editContext.GetValidationMessages().Count());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Async_submission_waits_for_release_and_publishes_errors(bool expectedValid)
        {
            var model = new TestModel();
            var validator = new ControlledAsyncValidator();
            bool? actualValid = null;
            var form = RenderForm(model, validator, async (validation, _) =>
            {
                actualValid = await validation.ValidateAsync();
            });

            var submission = form.Find("form").SubmitAsync();
            var call = await validator.NextCallAsync();

            Assert.False(submission.IsCompleted);
            Assert.Equal(1, validator.AsyncCallCount);
            Assert.Equal(0, validator.SyncCallCount);

            call.Complete(expectedValid
                ? ValidationResult.Success
                : Result(Failure(nameof(TestModel.Name), "async error")));
            await submission;

            Assert.True(actualValid.HasValue);
            Assert.Equal(expectedValid, actualValid.Value);
            Assert.Equal(0, validator.SyncCallCount);
            var editContext = form.Instance.EditContext!;
            Assert.Equal(
                expectedValid ? Array.Empty<string>() : new[] { "async error" },
                Messages(editContext, FieldIdentifier.Create(() => model.Name)));
        }

        [Fact]
        public async Task Latest_async_run_wins_when_older_work_completes_last()
        {
            var model = new TestModel();
            var editContext = new EditContext(model);
            var validator = new ControlledAsyncValidator();
            var completedResults = new List<ValidationResult>();
            var cut = RenderValidator(editContext, validator, completedResults.Add);
            var nameField = FieldIdentifier.Create(() => model.Name);

            var oldSubmission = cut.Instance.ValidateAsync();
            var oldCall = await validator.NextCallAsync();
            var latestSubmission = cut.Instance.ValidateAsync();
            var latestCall = await validator.NextCallAsync();

            latestCall.Complete(Result(Failure(nameof(TestModel.Name), "latest")));
            Assert.False(await latestSubmission);
            Assert.Equal(new[] { "latest" }, Messages(editContext, nameField));

            oldCall.Complete(Result(Failure(nameof(TestModel.Name), "stale")));
            Assert.False(await oldSubmission);
            await oldCall.Completion;
            await cut.InvokeAsync(() => { });

            Assert.Equal(new[] { "latest" }, Messages(editContext, nameField));
            var completed = Assert.Single(completedResults);
            Assert.Equal("latest", Assert.Single(completed.Failures).ErrorMessage);
            Assert.Equal(0, validator.SyncCallCount);
        }

        [Fact]
        public async Task Validation_state_change_supersedes_the_old_completion_callback()
        {
            var model = new TestModel();
            var editContext = new EditContext(model);
            var validator = new StubValidator
            {
                Result = Result(Failure(nameof(TestModel.Name), "old"))
            };
            var completedResults = new List<ValidationResult>();
            var cut = RenderValidator(editContext, validator, completedResults.Add);
            var superseded = false;
            editContext.OnValidationStateChanged += (_, _) =>
            {
                if (superseded)
                    return;

                superseded = true;
                validator.Result = Result(Failure(nameof(TestModel.Name), "new"));
                editContext.NotifyFieldChanged(FieldIdentifier.Create(() => model.Name));
            };

            await cut.InvokeAsync(() => editContext.Validate());

            Assert.Equal(new[] { "new" }, Messages(
                editContext, FieldIdentifier.Create(() => model.Name)));
            var completed = Assert.Single(completedResults);
            Assert.Equal("new", Assert.Single(completed.Failures).ErrorMessage);
        }

        [Fact]
        public async Task Edit_during_awaited_submission_returns_false_before_old_work_finishes()
        {
            var model = new TestModel();
            var validator = new ControlledAsyncValidator();
            EditContext? editContext = null;
            bool? actualValid = null;
            var currentRunCompleted = new TaskCompletionSource<ValidationResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var form = RenderForm(model, validator, async (validation, context) =>
            {
                editContext = context;
                actualValid = await validation.ValidateAsync();
            }, result => currentRunCompleted.TrySetResult(result));

            var submission = form.Find("form").SubmitAsync();
            var oldCall = await validator.NextCallAsync();
            Assert.NotNull(editContext);

            await form.InvokeAsync(() => editContext.NotifyFieldChanged(
                FieldIdentifier.Create(() => model.Name)));
            var currentCall = await validator.NextCallAsync();

            await submission;

            Assert.False(actualValid);
            Assert.False(oldCall.Completion.IsCompleted);
            Assert.Equal(2, validator.AsyncCallCount);
            Assert.Equal(0, validator.SyncCallCount);

            currentCall.Complete(ValidationResult.Success);
            await currentRunCompleted.Task;
            oldCall.Complete(ValidationResult.Success);
            await oldCall.Completion;
        }

        [Fact]
        public async Task Error_paths_map_to_fields_and_unresolved_paths_fall_back_to_root()
        {
            var model = new TestModel();
            var editContext = new EditContext(model);
            var validator = new StubValidator
            {
                Result = Result(
                    Failure(nameof(TestModel.Name), "simple"),
                    Failure("Child.Value", "nested"),
                    Failure("Children[0].Value", "indexed"),
                    Failure("Tags[1]", "terminal index"),
                    Failure("Tags[01]", "leading zero index"),
                    Failure("Child.", "nested model"),
                    Failure(string.Empty, "root model"),
                    Failure("Missing.Value", "missing path"),
                    Failure("Tags[99]", "missing index"),
                    Failure("Item[0]", "indexer path"))
            };
            var cut = RenderValidator(editContext, validator);

            await cut.InvokeAsync(() => editContext.Validate());

            Assert.Equal(new[] { "simple" }, Messages(
                editContext, FieldIdentifier.Create(() => model.Name)));
            Assert.Equal(new[] { "nested" }, Messages(
                editContext, FieldIdentifier.Create(() => model.Child.Value)));
            Assert.Equal(new[] { "indexed" }, Messages(
                editContext, FieldIdentifier.Create(() => model.Children[0].Value)));
            Assert.Equal(new[] { "nested model" }, Messages(
                editContext, new FieldIdentifier(model.Child, string.Empty)));
            Assert.Equal(
                new[] { "root model", "missing path", "missing index", "indexer path" },
                Messages(editContext, new FieldIdentifier(model, string.Empty)));
            Assert.Equal(10, editContext.GetValidationMessages().Count());
            var terminalTag = FieldIdentifier.Create(() => model.Tags[1]);
            Assert.Equal(new[] { "terminal index", "leading zero index" }, Messages(editContext, terminalTag));
        }

        [Fact]
        public async Task Only_errors_enter_EditContext_and_completion_receives_the_full_result()
        {
            var model = new TestModel();
            var editContext = new EditContext(model);
            var expectedResult = Result(
                Failure(nameof(TestModel.Name), "error", ValidationSeverity.Error),
                Failure(nameof(TestModel.Other), "warning", ValidationSeverity.Warning),
                Failure("Child.Value", "info", ValidationSeverity.Info));
            var validator = new StubValidator { Result = expectedResult };
            ValidationResult? completedResult = null;
            string[]? messagesAtCallback = null;
            var cut = RenderValidator(editContext, validator, result =>
            {
                completedResult = result;
                messagesAtCallback = editContext.GetValidationMessages().ToArray();
            });

            await cut.InvokeAsync(() => editContext.Validate());

            Assert.Equal(new[] { "error" }, Messages(
                editContext, FieldIdentifier.Create(() => model.Name)));
            Assert.Empty(Messages(editContext, FieldIdentifier.Create(() => model.Other)));
            Assert.Empty(Messages(editContext, FieldIdentifier.Create(() => model.Child.Value)));
            Assert.Single(editContext.GetValidationMessages());
            Assert.Same(expectedResult, completedResult);
            Assert.Equal(3, completedResult!.Failures.Count);
            Assert.Equal(new[] { "error" }, messagesAtCallback);
        }

        [Fact]
        public async Task Foreign_messages_survive_revalidation_and_disposal_and_block_submission()
        {
            var model = new TestModel();
            var editContext = new EditContext(model);
            var validator = new StubValidator
            {
                Result = Result(Failure(nameof(TestModel.Name), "own"))
            };
            var cut = RenderValidator(editContext, validator);
            var ownField = FieldIdentifier.Create(() => model.Name);
            var foreignField = FieldIdentifier.Create(() => model.Other);
            var foreignStore = new ValidationMessageStore(editContext);
            foreignStore.Add(foreignField, "foreign");

            Assert.False(await cut.Instance.ValidateAsync());
            Assert.Equal(new[] { "own" }, Messages(editContext, ownField));
            Assert.Equal(new[] { "foreign" }, Messages(editContext, foreignField));

            validator.Result = ValidationResult.Success;
            await cut.InvokeAsync(() => editContext.Validate());

            Assert.Empty(Messages(editContext, ownField));
            Assert.Equal(new[] { "foreign" }, Messages(editContext, foreignField));
            Assert.False(await cut.Instance.ValidateAsync());
            Assert.Equal(new[] { "foreign" }, Messages(editContext, foreignField));

            cut.Instance.Dispose();

            Assert.Equal(new[] { "foreign" }, Messages(editContext, foreignField));
            Assert.Equal(new[] { "foreign" }, editContext.GetValidationMessages().ToArray());
        }

        [Fact]
        public async Task Dispose_detaches_cancels_and_cleans_up_without_disposing_the_validator()
        {
            var model = new TestModel();
            var editContext = new EditContext(model);
            var validator = new ControlledAsyncValidator();
            var completedResults = new List<ValidationResult>();
            var cut = RenderValidator(editContext, validator, completedResults.Add);
            var ownField = FieldIdentifier.Create(() => model.Name);
            var foreignField = FieldIdentifier.Create(() => model.Other);

            var firstSubmission = cut.Instance.ValidateAsync();
            var firstCall = await validator.NextCallAsync();
            firstCall.Complete(Result(Failure(nameof(TestModel.Name), "own")));
            Assert.False(await firstSubmission);
            Assert.Equal(new[] { "own" }, Messages(editContext, ownField));

            var foreignStore = new ValidationMessageStore(editContext);
            foreignStore.Add(foreignField, "foreign");
            var pendingSubmission = cut.Instance.ValidateAsync();
            var pendingCall = await validator.NextCallAsync();

            cut.Instance.Dispose();

            Assert.True(pendingCall.CancellationToken.IsCancellationRequested);
            Assert.False(await pendingSubmission);
            Assert.Empty(Messages(editContext, ownField));
            Assert.Equal(new[] { "foreign" }, Messages(editContext, foreignField));
            Assert.Equal(0, validator.DisposeCount);

            var callCount = validator.AsyncCallCount;
            editContext.NotifyFieldChanged(ownField);
            editContext.Validate();
            Assert.Equal(callCount, validator.AsyncCallCount);

            pendingCall.Complete(Result(Failure(nameof(TestModel.Name), "late")));
            await pendingCall.Completion;
            await cut.InvokeAsync(() => { });

            Assert.Empty(Messages(editContext, ownField));
            Assert.Equal(new[] { "foreign" }, editContext.GetValidationMessages().ToArray());
            Assert.Single(completedResults);
            Assert.Equal(0, validator.DisposeCount);
        }

        private IRenderedComponent<CSteroidValidator<TestModel>> RenderValidator(
            EditContext editContext,
            IValidator<TestModel> validator,
            Action<ValidationResult>? onCompleted = null)
        {
            return Render<CSteroidValidator<TestModel>>(parameters =>
            {
                parameters
                    .AddCascadingValue(editContext)
                    .Add(component => component.Validator, validator);
                if (onCompleted is not null)
                {
                    parameters.Add(
                        component => component.ValidationCompleted,
                        EventCallback.Factory.Create<ValidationResult>(this, onCompleted));
                }
            });
        }

        private IRenderedComponent<EditForm> RenderForm(
            TestModel model,
            IValidator<TestModel> validator,
            Func<CSteroidValidator<TestModel>, EditContext, Task> onSubmit,
            Action<ValidationResult>? onCompleted = null)
        {
            CSteroidValidator<TestModel>? validation = null;
            var form = Render<EditForm>(parameters =>
            {
                parameters
                    .Add(editForm => editForm.Model, model)
                    .Add(
                        editForm => editForm.OnSubmit,
                        EventCallback.Factory.Create<EditContext>(
                            this,
                            context => onSubmit(validation!, context)))
                    .Add(editForm => editForm.ChildContent, context => builder =>
                    {
                        builder.OpenComponent<CSteroidValidator<TestModel>>(0);
                        builder.AddAttribute(1, nameof(CSteroidValidator<TestModel>.Validator), validator);
                        if (onCompleted is not null)
                        {
                            builder.AddAttribute(
                                2,
                                nameof(CSteroidValidator<TestModel>.ValidationCompleted),
                                EventCallback.Factory.Create<ValidationResult>(this, onCompleted));
                        }
                        builder.CloseComponent();
                    });
            });
            validation = form.FindComponent<CSteroidValidator<TestModel>>().Instance;
            return form;
        }

        private static ValidationResult Result(params ValidationFailure[] failures)
            => new ValidationResult(failures);

        private static ValidationFailure Failure(
            string propertyName,
            string message,
            ValidationSeverity severity = ValidationSeverity.Error)
        {
            return new ValidationFailure
            {
                PropertyName = propertyName,
                ErrorMessage = message,
                Severity = severity
            };
        }

        private static string[] Messages(EditContext editContext, FieldIdentifier field)
            => editContext.GetValidationMessages(field).ToArray();
    }
}
