using CSteroid.Medium;
using MediatR;

[assembly: MediumCompositionRoot]
[assembly: MediumBehavior(typeof(CSteroid.Benchmarks.MediumFirstResponseBehavior<,>), Order = 0)]
[assembly: MediumBehavior(typeof(CSteroid.Benchmarks.MediumSecondResponseBehavior<,>), Order = 1)]

namespace CSteroid.Benchmarks
{
    internal interface ITracedMessage
    {
    }

    internal sealed class MediumResponse : ICommand<int> { }
    internal sealed class MediumCommand : ICommand { }
    internal sealed class MediumEvent : IMessage { }
    internal sealed class MediumTracedResponse : ICommand<int>, ITracedMessage { }

    internal sealed class MediatRResponse : IRequest<int> { }
    internal sealed class MediatRCommand : IRequest { }
    internal sealed class MediatREvent : INotification { }
    internal sealed class MediatRTracedResponse : IRequest<int>, ITracedMessage { }

    internal sealed class UnknownMediumResponse : ICommand<int> { }
    internal sealed class UnknownMediumCommand : ICommand { }
    internal sealed class UnknownMediumEvent : IMessage { }

    internal sealed class ComparisonSink
    {
        internal int CommandCalls { get; set; }
        internal int PublishCalls { get; set; }
        internal int BehaviorCalls { get; set; }
    }

    internal sealed class MediumResponseHandler : ICommandHandler<MediumResponse, int>
    {
        public ValueTask<int> Handle(MediumResponse command, CancellationToken cancellationToken) => new(42);
    }

    internal sealed class MediumTracedResponseHandler : ICommandHandler<MediumTracedResponse, int>
    {
        public ValueTask<int> Handle(MediumTracedResponse command, CancellationToken cancellationToken) => new(42);
    }

    internal sealed class MediumCommandHandler : ICommandHandler<MediumCommand>
    {
        private readonly ComparisonSink _sink;

        public MediumCommandHandler(ComparisonSink sink)
        {
            _sink = sink;
        }

        public ValueTask Handle(MediumCommand command, CancellationToken cancellationToken)
        {
            _sink.CommandCalls++;
            return default;
        }
    }

    internal sealed class FirstMediumEventHandler : IMessageHandler<MediumEvent>
    {
        private readonly ComparisonSink _sink;

        public FirstMediumEventHandler(ComparisonSink sink)
        {
            _sink = sink;
        }

        public ValueTask Handle(MediumEvent message, CancellationToken cancellationToken)
        {
            _sink.PublishCalls += 2;
            return default;
        }
    }

    internal sealed class SecondMediumEventHandler : IMessageHandler<MediumEvent>
    {
        private readonly ComparisonSink _sink;

        public SecondMediumEventHandler(ComparisonSink sink)
        {
            _sink = sink;
        }

        public ValueTask Handle(MediumEvent message, CancellationToken cancellationToken)
        {
            _sink.PublishCalls += 4;
            return default;
        }
    }

    internal sealed class MediumFirstResponseBehavior<TMessage, TResponse> : CSteroid.Medium.IPipelineBehavior<TMessage, TResponse>
        where TMessage : IMessage, ITracedMessage
    {
        private readonly ComparisonSink _sink;

        public MediumFirstResponseBehavior(ComparisonSink sink)
        {
            _sink = sink;
        }

        public ValueTask<TResponse> Handle(
            TMessage message,
            MediumNext<TMessage, TResponse> next,
            CancellationToken cancellationToken)
        {
            _sink.BehaviorCalls++;
            return next(message, cancellationToken);
        }
    }

    internal sealed class MediumSecondResponseBehavior<TMessage, TResponse> : CSteroid.Medium.IPipelineBehavior<TMessage, TResponse>
        where TMessage : IMessage, ITracedMessage
    {
        private readonly ComparisonSink _sink;

        public MediumSecondResponseBehavior(ComparisonSink sink)
        {
            _sink = sink;
        }

        public ValueTask<TResponse> Handle(
            TMessage message,
            MediumNext<TMessage, TResponse> next,
            CancellationToken cancellationToken)
        {
            _sink.BehaviorCalls++;
            return next(message, cancellationToken);
        }
    }

    internal sealed class MediatRResponseHandler : IRequestHandler<MediatRResponse, int>
    {
        public Task<int> Handle(MediatRResponse request, CancellationToken cancellationToken) => Task.FromResult(42);
    }

    internal sealed class MediatRTracedResponseHandler : IRequestHandler<MediatRTracedResponse, int>
    {
        public Task<int> Handle(MediatRTracedResponse request, CancellationToken cancellationToken) => Task.FromResult(42);
    }

    internal sealed class MediatRCommandHandler : IRequestHandler<MediatRCommand>
    {
        private readonly ComparisonSink _sink;

        public MediatRCommandHandler(ComparisonSink sink)
        {
            _sink = sink;
        }

        public Task Handle(MediatRCommand request, CancellationToken cancellationToken)
        {
            _sink.CommandCalls++;
            return Task.CompletedTask;
        }
    }

    internal sealed class FirstMediatREventHandler : INotificationHandler<MediatREvent>
    {
        private readonly ComparisonSink _sink;

        public FirstMediatREventHandler(ComparisonSink sink)
        {
            _sink = sink;
        }

        public Task Handle(MediatREvent notification, CancellationToken cancellationToken)
        {
            _sink.PublishCalls += 2;
            return Task.CompletedTask;
        }
    }

    internal sealed class SecondMediatREventHandler : INotificationHandler<MediatREvent>
    {
        private readonly ComparisonSink _sink;

        public SecondMediatREventHandler(ComparisonSink sink)
        {
            _sink = sink;
        }

        public Task Handle(MediatREvent notification, CancellationToken cancellationToken)
        {
            _sink.PublishCalls += 4;
            return Task.CompletedTask;
        }
    }

    internal sealed class MediatRFirstResponseBehavior<TMessage, TResponse> : MediatR.IPipelineBehavior<TMessage, TResponse>
        where TMessage : IRequest<TResponse>, ITracedMessage
    {
        private readonly ComparisonSink _sink;

        public MediatRFirstResponseBehavior(ComparisonSink sink)
        {
            _sink = sink;
        }

        public Task<TResponse> Handle(
            TMessage request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            _sink.BehaviorCalls++;
            return next();
        }
    }

    internal sealed class MediatRSecondResponseBehavior<TMessage, TResponse> : MediatR.IPipelineBehavior<TMessage, TResponse>
        where TMessage : IRequest<TResponse>, ITracedMessage
    {
        private readonly ComparisonSink _sink;

        public MediatRSecondResponseBehavior(ComparisonSink sink)
        {
            _sink = sink;
        }

        public Task<TResponse> Handle(
            TMessage request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            _sink.BehaviorCalls++;
            return next();
        }
    }
}
