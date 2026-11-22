using MediatR;

namespace Migration.Before
{
    public sealed record CreateOrder(string Sku) : IRequest<OrderReceipt>;

    public sealed record OrderReceipt(Guid OrderId, string Sku);

    public sealed record RecordOrder(Guid OrderId) : IRequest<Unit>;

    public record OrderCreated(Guid OrderId) : INotification;


    public sealed class SliceState
    {
        public List<string> Calls { get; } = new();
    }

    public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, OrderReceipt>
    {
        private readonly SliceState _state;

        public CreateOrderHandler(SliceState state)
        {
            _state = state;
        }

        public Task<OrderReceipt> Handle(
            CreateOrder request,
            CancellationToken cancellationToken)
        {
            _state.Calls.Add("response:handler");
            return Task.FromResult(new OrderReceipt(Guid.NewGuid(), request.Sku));
        }
    }

    public sealed class RecordOrderHandler : IRequestHandler<RecordOrder, Unit>
    {
        private readonly SliceState _state;

        public RecordOrderHandler(SliceState state)
        {
            _state = state;
        }

        public Task<Unit> Handle(RecordOrder request, CancellationToken cancellationToken)
        {
            _state.Calls.Add("command:handler");
            return Task.FromResult(Unit.Value);
        }
    }

    public sealed class CreateOrderBehavior : IPipelineBehavior<CreateOrder, OrderReceipt>
    {
        private readonly SliceState _state;

        public CreateOrderBehavior(SliceState state)
        {
            _state = state;
        }

        public async Task<OrderReceipt> Handle(
            CreateOrder request,
            RequestHandlerDelegate<OrderReceipt> next,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required.", nameof(request));

            _state.Calls.Add("behavior:validate");
            _state.Calls.Add("behavior:before");
            var response = await next().ConfigureAwait(false);
            _state.Calls.Add("behavior:after");
            return response;
        }
    }

    public sealed class FirstOrderCreatedHandler : INotificationHandler<OrderCreated>
    {
        private readonly SliceState _state;

        public FirstOrderCreatedHandler(SliceState state)
        {
            _state = state;
        }

        public async Task Handle(OrderCreated notification, CancellationToken cancellationToken)
        {
            _state.Calls.Add("message:first:start");
            await Task.Yield();
            _state.Calls.Add("message:first:end");
        }
    }

    public sealed class SecondOrderCreatedHandler : INotificationHandler<OrderCreated>
    {
        private readonly SliceState _state;

        public SecondOrderCreatedHandler(SliceState state)
        {
            _state = state;
        }

        public Task Handle(OrderCreated notification, CancellationToken cancellationToken)
        {
            _state.Calls.Add("message:second");
            return Task.CompletedTask;
        }
    }

    public sealed class OrderWorkflow
    {
        private readonly IMediator _mediator;
        private readonly ISender _sender;
        private readonly IPublisher _publisher;

        public OrderWorkflow(
            IMediator mediator,
            ISender sender,
            IPublisher publisher,
            SliceState state)
        {
            _mediator = mediator;
            _sender = sender;
            _publisher = publisher;
            State = state;
        }

        public SliceState State { get; }

        public Task<OrderReceipt> Create(string sku, CancellationToken cancellationToken)
            => _sender.Send(new CreateOrder(sku), cancellationToken);

        public async Task Record(Guid orderId, CancellationToken cancellationToken)
        {
            _ = await _mediator.Send(new RecordOrder(orderId), cancellationToken).ConfigureAwait(false);
        }

        public Task Publish(Guid orderId, CancellationToken cancellationToken)
            => _publisher.Publish(new OrderCreated(orderId), cancellationToken);
    }
}
