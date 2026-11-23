using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid.Medium
{
    /// <summary>Continues a behavior pipeline that produces no response.</summary>
    public delegate ValueTask MediumNext<in TMessage>(
        TMessage message,
        CancellationToken cancellationToken)
        where TMessage : IMessage;

    /// <summary>Continues a behavior pipeline that produces a response.</summary>
    public delegate ValueTask<TResponse> MediumNext<in TMessage, TResponse>(
        TMessage message,
        CancellationToken cancellationToken)
        where TMessage : IMessage;

    /// <summary>Continues a behavior pipeline that produces a stream of items.</summary>
    public delegate IAsyncEnumerable<T> MediumStreamNext<in TMessage, T>(
        TMessage message,
        CancellationToken cancellationToken)
        where TMessage : IMessage;

    /// <summary>Pipeline behavior without a response. Deliberately invariant.</summary>
    public interface IPipelineBehavior<TMessage>
        where TMessage : IMessage
    {
        ValueTask Handle(
            TMessage message,
            MediumNext<TMessage> next,
            CancellationToken cancellationToken);
    }

    /// <summary>Pipeline behavior with a response. Deliberately invariant.</summary>
    public interface IPipelineBehavior<TMessage, TResponse>
        where TMessage : IMessage
    {
        ValueTask<TResponse> Handle(
            TMessage message,
            MediumNext<TMessage, TResponse> next,
            CancellationToken cancellationToken);
    }

    /// <summary>Pipeline behavior for a stream command. Deliberately invariant.</summary>
    public interface IStreamPipelineBehavior<TMessage, T>
        where TMessage : IMessage
    {
        IAsyncEnumerable<T> Handle(
            TMessage message,
            MediumStreamNext<TMessage, T> next,
            CancellationToken cancellationToken);
    }
}
