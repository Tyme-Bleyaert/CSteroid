using CSteroid.Medium;

[assembly: MediumCompositionRoot]
[assembly: MediumBehavior(typeof(Migration.After.CreateOrderBehavior), Order = 100)]

namespace Migration.After
{
    public sealed record CreateOrder(string Sku) : ICommand<OrderReceipt>;

    public sealed record OrderReceipt(Guid OrderId, string Sku);

    public sealed record RecordOrder(Guid OrderId) : ICommand;

    public record OrderCreated(Guid OrderId) : IMessage;


    public sealed class SliceState
    {
        public List<string> Calls { get; } = new();
    }

    public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, OrderReceipt>
    {
        private readonly SliceState _state;

        public CreateOrderHandler(SliceState state)
        {
            _state = state;
        }

        public ValueTask<OrderReceipt> Handle(
            CreateOrder command,
            CancellationToken cancellationToken)
        {
            _state.Calls.Add("response:handler");
            return new ValueTask<OrderReceipt>(new OrderReceipt(Guid.NewGuid(), command.Sku));
        }
    }

    public sealed class RecordOrderHandler : ICommandHandler<RecordOrder>
    {
        private readonly SliceState _state;

        public RecordOrderHandler(SliceState state)
        {
            _state = state;
        }

        public ValueTask Handle(RecordOrder command, CancellationToken cancellationToken)
        {
            _state.Calls.Add("command:handler");
            return default;
        }
    }

    public sealed class CreateOrderBehavior : IPipelineBehavior<CreateOrder, OrderReceipt>
    {
        private readonly SliceState _state;

        public CreateOrderBehavior(SliceState state)
        {
            _state = state;
        }

        public async ValueTask<OrderReceipt> Handle(
            CreateOrder message,
            MediumNext<CreateOrder, OrderReceipt> next,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(message.Sku))
                throw new ArgumentException("SKU is required.", nameof(message));

            _state.Calls.Add("behavior:validate");
            _state.Calls.Add("behavior:before");
            var response = await next(message, cancellationToken).ConfigureAwait(false);
            _state.Calls.Add("behavior:after");
            return response;
        }
    }

    public sealed class FirstOrderCreatedHandler : IMessageHandler<OrderCreated>
    {
        private readonly SliceState _state;

        public FirstOrderCreatedHandler(SliceState state)
        {
            _state = state;
        }

        public async ValueTask Handle(OrderCreated message, CancellationToken cancellationToken)
        {
            _state.Calls.Add("message:first:start");
            await Task.Yield();
            _state.Calls.Add("message:first:end");
        }
    }

    public sealed class SecondOrderCreatedHandler : IMessageHandler<OrderCreated>
    {
        private readonly SliceState _state;

        public SecondOrderCreatedHandler(SliceState state)
        {
            _state = state;
        }

        public ValueTask Handle(OrderCreated message, CancellationToken cancellationToken)
        {
            _state.Calls.Add("message:second");
            return default;
        }
    }

    public sealed class OrderWorkflow
    {
        private readonly IMedium _mediator;
        private readonly ISender _sender;
        private readonly IPublisher _publisher;

        public OrderWorkflow(
            IMedium mediator,
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

        public ValueTask<OrderReceipt> Create(string sku, CancellationToken cancellationToken)
            => _sender.Send(new CreateOrder(sku), cancellationToken);

        public ValueTask Record(Guid orderId, CancellationToken cancellationToken)
            => _mediator.Send(new RecordOrder(orderId), cancellationToken);

        public ValueTask Publish(Guid orderId, CancellationToken cancellationToken)
            => _publisher.Publish(new OrderCreated(orderId), cancellationToken);
    }
}
