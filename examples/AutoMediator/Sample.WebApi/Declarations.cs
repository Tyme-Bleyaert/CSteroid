using CSteroid.AutoMediator;
using Sample.Application;

[assembly: AutoMediatorCompositionRoot]
[assembly: AutoMediatorBehavior(typeof(Sample.WebApi.TraceBehavior<,>))]

namespace Sample.WebApi
{
    public sealed class TraceBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
        where TMessage : IMessage
    {
        private readonly RequestTrace _trace;

        public TraceBehavior(RequestTrace trace)
        {
            _trace = trace;
        }

        public async ValueTask<TResponse> Handle(
            TMessage message,
            AutoMediatorNext<TMessage, TResponse> next,
            CancellationToken cancellationToken)
        {
            _trace.Entries.Add("behavior:before");
            var response = await next(message, cancellationToken).ConfigureAwait(false);
            _trace.Entries.Add("behavior:after");
            return response;
        }
    }

    public sealed class SampleWorkflow
    {
        private readonly IAutoMediator _mediator;

        public SampleWorkflow(IAutoMediator mediator, RequestTrace trace)
        {
            _mediator = mediator;
            Trace = trace;
        }

        public RequestTrace Trace { get; }

        public ValueTask<string> Ping(CancellationToken cancellationToken)
            => _mediator.Send(new Ping(), cancellationToken);

        public ValueTask Record(CancellationToken cancellationToken)
            => _mediator.Send(new Record(), cancellationToken);

        public ValueTask Publish(CancellationToken cancellationToken)
            => _mediator.Publish(new Pinged(), cancellationToken);
    }
}
