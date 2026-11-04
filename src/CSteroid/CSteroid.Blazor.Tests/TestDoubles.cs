using CSteroid;

namespace CSteroid.Blazor.Tests
{
    internal sealed class TestModel
    {
        public string Name { get; set; } = string.Empty;

        public string Other { get; set; } = string.Empty;

        public NestedTestModel Child { get; set; } = new NestedTestModel();

        public List<NestedTestModel> Children { get; set; } = new List<NestedTestModel>
        {
            new NestedTestModel()
        };

        public List<string> Tags { get; set; } = new List<string> { "zero", "one" };

        public string this[int index] => Tags[index];
    }

    internal sealed class NestedTestModel
    {
        public string Value { get; set; } = string.Empty;
    }

    internal sealed class StubValidator : IValidator<TestModel>
    {
        public ValidationResult Result { get; set; } = ValidationResult.Success;

        public int CallCount { get; private set; }

        public ValidationResult Validate(TestModel instance)
        {
            CallCount++;
            return Result;
        }
    }

    internal sealed class ControlledAsyncValidator : IAsyncValidator<TestModel>, IDisposable
    {
        private readonly object _sync = new object();
        private readonly Queue<ControlledValidationCall> _unclaimedCalls = new Queue<ControlledValidationCall>();
        private readonly Queue<TaskCompletionSource<ControlledValidationCall>> _waiters =
            new Queue<TaskCompletionSource<ControlledValidationCall>>();
        private int _asyncCallCount;
        private int _syncCallCount;
        private int _disposeCount;

        public int AsyncCallCount => Volatile.Read(ref _asyncCallCount);

        public int SyncCallCount => Volatile.Read(ref _syncCallCount);

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public ValidationResult Validate(TestModel instance)
        {
            Interlocked.Increment(ref _syncCallCount);
            throw new InvalidOperationException("The synchronous validator must not be called.");
        }

        public Task<ValidationResult> ValidateAsync(TestModel instance, CancellationToken cancellationToken = default)
        {
            var call = new ControlledValidationCall(cancellationToken);
            TaskCompletionSource<ControlledValidationCall>? waiter = null;

            lock (_sync)
            {
                Interlocked.Increment(ref _asyncCallCount);
                if (_waiters.Count > 0)
                    waiter = _waiters.Dequeue();
                else
                    _unclaimedCalls.Enqueue(call);
            }

            waiter?.TrySetResult(call);
            return call.Completion;
        }

        public Task<ControlledValidationCall> NextCallAsync()
        {
            lock (_sync)
            {
                if (_unclaimedCalls.Count > 0)
                    return Task.FromResult(_unclaimedCalls.Dequeue());

                var waiter = new TaskCompletionSource<ControlledValidationCall>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Enqueue(waiter);
                return waiter.Task;
            }
        }

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    internal sealed class ControlledValidationCall
    {
        private readonly TaskCompletionSource<ValidationResult> _completion =
            new TaskCompletionSource<ValidationResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ControlledValidationCall(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }

        public Task<ValidationResult> Completion => _completion.Task;

        public void Complete(ValidationResult result) => _completion.SetResult(result);
    }
}
