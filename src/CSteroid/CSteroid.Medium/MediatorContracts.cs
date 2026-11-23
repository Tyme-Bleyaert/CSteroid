using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid.Medium
{
    /// <summary>Sends commands to their exactly one registered handler.</summary>
    public interface ISender
    {
        ValueTask Send(
            ICommand command,
            CancellationToken cancellationToken = default);

        ValueTask<TResponse> Send<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Publishes messages to their ordered handlers.</summary>
    public interface IPublisher
    {
        ValueTask Publish(
            IMessage message,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Sends stream commands to their exactly one registered handler.</summary>
    public interface IStreamSender
    {
        IAsyncEnumerable<T> Stream<T>(
            IStreamCommand<T> command,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Combines command sending, streaming, and message publication.</summary>
    public interface IMedium : ISender, IStreamSender, IPublisher
    {
    }
}
