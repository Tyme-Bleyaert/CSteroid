using CSteroid.Medium;
using Microsoft.Extensions.DependencyInjection;

namespace CSteroid.Benchmarks
{
    internal sealed class BehaviorResponse : ICommand<int> { }
    internal sealed class BehaviorCommand : ICommand { }
    internal sealed class BehaviorEvent : IMessage { }

    internal sealed class BehaviorSink
    {
        internal int Value { get; set; }
    }

    internal sealed class BehaviorResponseHandler : ICommandHandler<BehaviorResponse, int>
    {
        public ValueTask<int> Handle(BehaviorResponse command, CancellationToken cancellationToken) => new(42);
    }

    internal sealed class BehaviorCommandHandler : ICommandHandler<BehaviorCommand>
    {
        private readonly BehaviorSink _sink;

        internal BehaviorCommandHandler(BehaviorSink sink)
        {
            _sink = sink;
        }

        public ValueTask Handle(BehaviorCommand command, CancellationToken cancellationToken)
        {
            _sink.Value += 1;
            return default;
        }
    }

    internal sealed class FirstBehaviorEventHandler : IMessageHandler<BehaviorEvent>
    {
        private readonly BehaviorSink _sink;

        internal FirstBehaviorEventHandler(BehaviorSink sink)
        {
            _sink = sink;
        }

        public ValueTask Handle(BehaviorEvent message, CancellationToken cancellationToken)
        {
            _sink.Value += 2;
            return default;
        }
    }

    internal sealed class SecondBehaviorEventHandler : IMessageHandler<BehaviorEvent>
    {
        private readonly BehaviorSink _sink;

        internal SecondBehaviorEventHandler(BehaviorSink sink)
        {
            _sink = sink;
        }

        public ValueTask Handle(BehaviorEvent message, CancellationToken cancellationToken)
        {
            _sink.Value += 4;
            return default;
        }
    }

    internal sealed class FirstResponsePassThrough : IPipelineBehavior<BehaviorResponse, int>
    {
        internal int Calls { get; private set; }

        public ValueTask<int> Handle(
            BehaviorResponse message,
            MediumNext<BehaviorResponse, int> next,
            CancellationToken cancellationToken)
        {
            Calls++;
            return next(message, cancellationToken);
        }
    }

    internal sealed class SecondResponsePassThrough : IPipelineBehavior<BehaviorResponse, int>
    {
        internal int Calls { get; private set; }

        public ValueTask<int> Handle(
            BehaviorResponse message,
            MediumNext<BehaviorResponse, int> next,
            CancellationToken cancellationToken)
        {
            Calls++;
            return next(message, cancellationToken);
        }
    }

    internal sealed class FirstCommandPassThrough : IPipelineBehavior<BehaviorCommand>
    {
        internal int Calls { get; private set; }

        public ValueTask Handle(
            BehaviorCommand message,
            MediumNext<BehaviorCommand> next,
            CancellationToken cancellationToken)
        {
            Calls++;
            return next(message, cancellationToken);
        }
    }

    internal sealed class SecondCommandPassThrough : IPipelineBehavior<BehaviorCommand>
    {
        internal int Calls { get; private set; }

        public ValueTask Handle(
            BehaviorCommand message,
            MediumNext<BehaviorCommand> next,
            CancellationToken cancellationToken)
        {
            Calls++;
            return next(message, cancellationToken);
        }
    }

    internal sealed class FirstPublishPassThrough : IPipelineBehavior<BehaviorEvent>
    {
        internal int Calls { get; private set; }

        public ValueTask Handle(
            BehaviorEvent message,
            MediumNext<BehaviorEvent> next,
            CancellationToken cancellationToken)
        {
            Calls++;
            return next(message, cancellationToken);
        }
    }

    internal sealed class SecondPublishPassThrough : IPipelineBehavior<BehaviorEvent>
    {
        internal int Calls { get; private set; }

        public ValueTask Handle(
            BehaviorEvent message,
            MediumNext<BehaviorEvent> next,
            CancellationToken cancellationToken)
        {
            Calls++;
            return next(message, cancellationToken);
        }
    }

    /// <summary>Mirrors generated fixed chains with closed behavior and handler resolution.</summary>
    internal sealed class BehaviorMediator
    {
        private readonly IServiceProvider _services;

        internal BehaviorMediator(IServiceProvider services)
        {
            _services = services;
        }

        internal int Sink => _services.GetRequiredService<BehaviorSink>().Value;
        internal int ResponseBehaviorCalls =>
            _services.GetRequiredService<FirstResponsePassThrough>().Calls
            + _services.GetRequiredService<SecondResponsePassThrough>().Calls;
        internal int CommandBehaviorCalls =>
            _services.GetRequiredService<FirstCommandPassThrough>().Calls
            + _services.GetRequiredService<SecondCommandPassThrough>().Calls;
        internal int PublishBehaviorCalls =>
            _services.GetRequiredService<FirstPublishPassThrough>().Calls
            + _services.GetRequiredService<SecondPublishPassThrough>().Calls;

        internal static BenchmarkServiceProvider CreateScope()
        {
            var sink = new BehaviorSink();
            return new BenchmarkServiceProvider()
                .Add(sink)
                .Add(new BehaviorResponseHandler())
                .Add(new BehaviorCommandHandler(sink))
                .Add(new FirstBehaviorEventHandler(sink))
                .Add(new SecondBehaviorEventHandler(sink))
                .Add(new FirstResponsePassThrough())
                .Add(new SecondResponsePassThrough())
                .Add(new FirstCommandPassThrough())
                .Add(new SecondCommandPassThrough())
                .Add(new FirstPublishPassThrough())
                .Add(new SecondPublishPassThrough());
        }

        internal ValueTask<int> SendWithoutBehaviors(BehaviorResponse command, CancellationToken cancellationToken)
        {
            return ResponseTerminal(_services, command, cancellationToken);
        }

        internal ValueTask<int> SendWithBehaviors(BehaviorResponse command, CancellationToken cancellationToken)
        {
            MediumNext<BehaviorResponse, int> next0 =
                (message, ct) => ResponseTerminal(_services, message, ct);
            var behavior0 = _services.GetRequiredService<SecondResponsePassThrough>();
            MediumNext<BehaviorResponse, int> next1 =
                (message, ct) => behavior0.Handle(message, next0, ct);
            var behavior1 = _services.GetRequiredService<FirstResponsePassThrough>();
            MediumNext<BehaviorResponse, int> next2 =
                (message, ct) => behavior1.Handle(message, next1, ct);
            return next2(command, cancellationToken);
        }

        internal ValueTask SendWithoutBehaviors(BehaviorCommand command, CancellationToken cancellationToken)
        {
            return CommandTerminal(_services, command, cancellationToken);
        }

        internal ValueTask SendWithBehaviors(BehaviorCommand command, CancellationToken cancellationToken)
        {
            MediumNext<BehaviorCommand> next0 =
                (message, ct) => CommandTerminal(_services, message, ct);
            var behavior0 = _services.GetRequiredService<SecondCommandPassThrough>();
            MediumNext<BehaviorCommand> next1 =
                (message, ct) => behavior0.Handle(message, next0, ct);
            var behavior1 = _services.GetRequiredService<FirstCommandPassThrough>();
            MediumNext<BehaviorCommand> next2 =
                (message, ct) => behavior1.Handle(message, next1, ct);
            return next2(command, cancellationToken);
        }

        internal ValueTask PublishWithoutBehaviors(BehaviorEvent message, CancellationToken cancellationToken)
        {
            return PublishTerminal(_services, message, cancellationToken);
        }

        internal ValueTask PublishWithBehaviors(BehaviorEvent message, CancellationToken cancellationToken)
        {
            MediumNext<BehaviorEvent> next0 =
                (nextMessage, ct) => PublishTerminal(_services, nextMessage, ct);
            var behavior0 = _services.GetRequiredService<SecondPublishPassThrough>();
            MediumNext<BehaviorEvent> next1 =
                (nextMessage, ct) => behavior0.Handle(nextMessage, next0, ct);
            var behavior1 = _services.GetRequiredService<FirstPublishPassThrough>();
            MediumNext<BehaviorEvent> next2 =
                (nextMessage, ct) => behavior1.Handle(nextMessage, next1, ct);
            return next2(message, cancellationToken);
        }

        private static ValueTask<int> ResponseTerminal(
            IServiceProvider services,
            BehaviorResponse command,
            CancellationToken cancellationToken)
        {
            var handler = (ICommandHandler<BehaviorResponse, int>)services.GetRequiredService<BehaviorResponseHandler>();
            return handler.Handle(command, cancellationToken);
        }

        private static ValueTask CommandTerminal(
            IServiceProvider services,
            BehaviorCommand command,
            CancellationToken cancellationToken)
        {
            var handler = (ICommandHandler<BehaviorCommand>)services.GetRequiredService<BehaviorCommandHandler>();
            return handler.Handle(command, cancellationToken);
        }

        private static async ValueTask PublishTerminal(
            IServiceProvider services,
            BehaviorEvent message,
            CancellationToken cancellationToken)
        {
            var first = (IMessageHandler<BehaviorEvent>)services.GetRequiredService<FirstBehaviorEventHandler>();
            await first.Handle(message, cancellationToken).ConfigureAwait(false);
            var second = (IMessageHandler<BehaviorEvent>)services.GetRequiredService<SecondBehaviorEventHandler>();
            await second.Handle(message, cancellationToken).ConfigureAwait(false);
        }
    }
}
