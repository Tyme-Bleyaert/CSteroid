namespace CSteroid.AutoMediator
{
    /// <summary>Marker for every message routed by AutoMediator.</summary>
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
}
