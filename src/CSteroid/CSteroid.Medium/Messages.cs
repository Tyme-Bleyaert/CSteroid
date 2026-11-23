namespace CSteroid.Medium
{
    /// <summary>Marker for every message routed by Medium.</summary>
    public interface IMessage
    {
    }

    /// <summary>Marker for a command dispatched with Send; exactly one handler route is required.</summary>
    public interface ICommand : IMessage
    {
    }

    /// <summary>Marker for a command with a response. Deliberately invariant for closed dispatch safety.</summary>
    public interface ICommand<TResponse> : ICommand
    {
    }

    /// <summary>Marker for a command dispatched with Stream; exactly one handler route is required.</summary>
    public interface IStreamCommand : IMessage
    {
    }

    /// <summary>Marker for a command that produces a stream of items.</summary>
    public interface IStreamCommand<T> : IStreamCommand
    {
    }
}
