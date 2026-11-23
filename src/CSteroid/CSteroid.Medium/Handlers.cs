using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid.Medium
{
    /// <summary>Handles a command that produces no response.</summary>
    public interface ICommandHandler<in TCommand>
        where TCommand : ICommand
    {
        ValueTask Handle(TCommand command, CancellationToken cancellationToken);
    }

    /// <summary>Handles a command and produces its declared response.</summary>
    public interface ICommandHandler<in TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        ValueTask<TResponse> Handle(
            TCommand command,
            CancellationToken cancellationToken);
    }

    /// <summary>Handles a command and produces its declared stream of items.</summary>
    public interface IStreamCommandHandler<in TCommand, T>
        where TCommand : IStreamCommand<T>
    {
        IAsyncEnumerable<T> Handle(TCommand command, CancellationToken cancellationToken);
    }

    /// <summary>Handles a published message.</summary>
    public interface IMessageHandler<in TMessage>
        where TMessage : IMessage
    {
        ValueTask Handle(TMessage message, CancellationToken cancellationToken);
    }
}
