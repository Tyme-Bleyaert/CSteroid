using CSteroid.AutoMediator;

namespace Sample.Application
{
    public sealed record Ping : ICommand<string>;

    public sealed record Record : ICommand;

    public sealed record Pinged : IMessage;

    public sealed class RequestTrace
    {
        public List<string> Entries { get; } = new();
    }

    public sealed class SampleHandler :
        ICommandHandler<Ping, string>,
        ICommandHandler<Record>,
        IMessageHandler<Pinged>
    {
        private readonly RequestTrace _trace;

        public SampleHandler(RequestTrace trace)
        {
            _trace = trace;
        }

        ValueTask<string> ICommandHandler<Ping, string>.Handle(
            Ping command,
            CancellationToken cancellationToken)
        {
            _trace.Entries.Add("response:handler");
            return new ValueTask<string>("pong");
        }

        ValueTask ICommandHandler<Record>.Handle(
            Record command,
            CancellationToken cancellationToken)
        {
            _trace.Entries.Add("command:handler");
            return default;
        }

        ValueTask IMessageHandler<Pinged>.Handle(
            Pinged message,
            CancellationToken cancellationToken)
        {
            _trace.Entries.Add("event:handler");
            return default;
        }
    }
}
