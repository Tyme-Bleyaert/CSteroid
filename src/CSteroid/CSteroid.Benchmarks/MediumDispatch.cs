using CSteroid.Medium;
using Microsoft.Extensions.DependencyInjection;

namespace CSteroid.Benchmarks
{
    internal interface IRouteSet
    {
        static abstract int Count { get; }
    }

    internal readonly struct OneRoute : IRouteSet
    {
        public static int Count => 1;
    }

    internal readonly struct EightRoutes : IRouteSet
    {
        public static int Count => 8;
    }

    internal readonly struct ThirtyTwoRoutes : IRouteSet
    {
        public static int Count => 32;
    }

    internal sealed record Response00(int Index) : ICommand<int>;
    internal sealed record Response01(int Index) : ICommand<int>;
    internal sealed record Response02(int Index) : ICommand<int>;
    internal sealed record Response03(int Index) : ICommand<int>;
    internal sealed record Response04(int Index) : ICommand<int>;
    internal sealed record Response05(int Index) : ICommand<int>;
    internal sealed record Response06(int Index) : ICommand<int>;
    internal sealed record Response07(int Index) : ICommand<int>;
    internal sealed record Response08(int Index) : ICommand<int>;
    internal sealed record Response09(int Index) : ICommand<int>;
    internal sealed record Response10(int Index) : ICommand<int>;
    internal sealed record Response11(int Index) : ICommand<int>;
    internal sealed record Response12(int Index) : ICommand<int>;
    internal sealed record Response13(int Index) : ICommand<int>;
    internal sealed record Response14(int Index) : ICommand<int>;
    internal sealed record Response15(int Index) : ICommand<int>;
    internal sealed record Response16(int Index) : ICommand<int>;
    internal sealed record Response17(int Index) : ICommand<int>;
    internal sealed record Response18(int Index) : ICommand<int>;
    internal sealed record Response19(int Index) : ICommand<int>;
    internal sealed record Response20(int Index) : ICommand<int>;
    internal sealed record Response21(int Index) : ICommand<int>;
    internal sealed record Response22(int Index) : ICommand<int>;
    internal sealed record Response23(int Index) : ICommand<int>;
    internal sealed record Response24(int Index) : ICommand<int>;
    internal sealed record Response25(int Index) : ICommand<int>;
    internal sealed record Response26(int Index) : ICommand<int>;
    internal sealed record Response27(int Index) : ICommand<int>;
    internal sealed record Response28(int Index) : ICommand<int>;
    internal sealed record Response29(int Index) : ICommand<int>;
    internal sealed record Response30(int Index) : ICommand<int>;
    internal sealed record Response31(int Index) : ICommand<int>;

    internal sealed record Command00(int Index) : ICommand;
    internal sealed record Command01(int Index) : ICommand;
    internal sealed record Command02(int Index) : ICommand;
    internal sealed record Command03(int Index) : ICommand;
    internal sealed record Command04(int Index) : ICommand;
    internal sealed record Command05(int Index) : ICommand;
    internal sealed record Command06(int Index) : ICommand;
    internal sealed record Command07(int Index) : ICommand;
    internal sealed record Command08(int Index) : ICommand;
    internal sealed record Command09(int Index) : ICommand;
    internal sealed record Command10(int Index) : ICommand;
    internal sealed record Command11(int Index) : ICommand;
    internal sealed record Command12(int Index) : ICommand;
    internal sealed record Command13(int Index) : ICommand;
    internal sealed record Command14(int Index) : ICommand;
    internal sealed record Command15(int Index) : ICommand;
    internal sealed record Command16(int Index) : ICommand;
    internal sealed record Command17(int Index) : ICommand;
    internal sealed record Command18(int Index) : ICommand;
    internal sealed record Command19(int Index) : ICommand;
    internal sealed record Command20(int Index) : ICommand;
    internal sealed record Command21(int Index) : ICommand;
    internal sealed record Command22(int Index) : ICommand;
    internal sealed record Command23(int Index) : ICommand;
    internal sealed record Command24(int Index) : ICommand;
    internal sealed record Command25(int Index) : ICommand;
    internal sealed record Command26(int Index) : ICommand;
    internal sealed record Command27(int Index) : ICommand;
    internal sealed record Command28(int Index) : ICommand;
    internal sealed record Command29(int Index) : ICommand;
    internal sealed record Command30(int Index) : ICommand;
    internal sealed record Command31(int Index) : ICommand;

    internal sealed record Event00(int Index) : IMessage;
    internal sealed record Event01(int Index) : IMessage;
    internal sealed record Event02(int Index) : IMessage;
    internal sealed record Event03(int Index) : IMessage;
    internal sealed record Event04(int Index) : IMessage;
    internal sealed record Event05(int Index) : IMessage;
    internal sealed record Event06(int Index) : IMessage;
    internal sealed record Event07(int Index) : IMessage;
    internal sealed record Event08(int Index) : IMessage;
    internal sealed record Event09(int Index) : IMessage;
    internal sealed record Event10(int Index) : IMessage;
    internal sealed record Event11(int Index) : IMessage;
    internal sealed record Event12(int Index) : IMessage;
    internal sealed record Event13(int Index) : IMessage;
    internal sealed record Event14(int Index) : IMessage;
    internal sealed record Event15(int Index) : IMessage;
    internal sealed record Event16(int Index) : IMessage;
    internal sealed record Event17(int Index) : IMessage;
    internal sealed record Event18(int Index) : IMessage;
    internal sealed record Event19(int Index) : IMessage;
    internal sealed record Event20(int Index) : IMessage;
    internal sealed record Event21(int Index) : IMessage;
    internal sealed record Event22(int Index) : IMessage;
    internal sealed record Event23(int Index) : IMessage;
    internal sealed record Event24(int Index) : IMessage;
    internal sealed record Event25(int Index) : IMessage;
    internal sealed record Event26(int Index) : IMessage;
    internal sealed record Event27(int Index) : IMessage;
    internal sealed record Event28(int Index) : IMessage;
    internal sealed record Event29(int Index) : IMessage;
    internal sealed record Event30(int Index) : IMessage;
    internal sealed record Event31(int Index) : IMessage;

    internal sealed class UnknownResponse : ICommand<int>
    {
    }

    internal sealed class UnknownCommand : ICommand
    {
    }

    internal sealed class UnknownEvent : IMessage
    {
    }

    /// <summary>A minimal per-mediator scope; container construction is outside the measured path.</summary>
    internal sealed class BenchmarkServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        internal BenchmarkServiceProvider Add<T>(T service)
            where T : class
        {
            _services.Add(typeof(T), service);
            return this;
        }

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service) ? service : null;
        }

        internal static BenchmarkServiceProvider CreateHandlerScope()
        {
            return new BenchmarkServiceProvider().Add(new RouteHandlers());
        }
    }

    internal abstract class DispatchMediator
    {
        protected DispatchMediator(IServiceProvider services)
        {
            Services = services;
        }

        protected IServiceProvider Services { get; }

        internal int Sink => Services.GetRequiredService<RouteHandlers>().Sink;
    }

    internal sealed class MediatorConsumer
    {
        private readonly IMedium _mediator;

        internal MediatorConsumer(IMedium mediator)
        {
            _mediator = mediator;
        }

        internal ValueTask<int> Send(ICommand<int> command, CancellationToken cancellationToken = default)
        {
            return _mediator.Send(command, cancellationToken);
        }

        internal ValueTask Send(ICommand command, CancellationToken cancellationToken = default)
        {
            return _mediator.Send(command, cancellationToken);
        }

        internal ValueTask Publish(IMessage message, CancellationToken cancellationToken = default)
        {
            return _mediator.Publish(message, cancellationToken);
        }
    }

    internal sealed class RouteHandlers :
        ICommandHandler<Response00, int>, ICommandHandler<Command00>, IMessageHandler<Event00>,
        ICommandHandler<Response01, int>, ICommandHandler<Command01>, IMessageHandler<Event01>,
        ICommandHandler<Response02, int>, ICommandHandler<Command02>, IMessageHandler<Event02>,
        ICommandHandler<Response03, int>, ICommandHandler<Command03>, IMessageHandler<Event03>,
        ICommandHandler<Response04, int>, ICommandHandler<Command04>, IMessageHandler<Event04>,
        ICommandHandler<Response05, int>, ICommandHandler<Command05>, IMessageHandler<Event05>,
        ICommandHandler<Response06, int>, ICommandHandler<Command06>, IMessageHandler<Event06>,
        ICommandHandler<Response07, int>, ICommandHandler<Command07>, IMessageHandler<Event07>,
        ICommandHandler<Response08, int>, ICommandHandler<Command08>, IMessageHandler<Event08>,
        ICommandHandler<Response09, int>, ICommandHandler<Command09>, IMessageHandler<Event09>,
        ICommandHandler<Response10, int>, ICommandHandler<Command10>, IMessageHandler<Event10>,
        ICommandHandler<Response11, int>, ICommandHandler<Command11>, IMessageHandler<Event11>,
        ICommandHandler<Response12, int>, ICommandHandler<Command12>, IMessageHandler<Event12>,
        ICommandHandler<Response13, int>, ICommandHandler<Command13>, IMessageHandler<Event13>,
        ICommandHandler<Response14, int>, ICommandHandler<Command14>, IMessageHandler<Event14>,
        ICommandHandler<Response15, int>, ICommandHandler<Command15>, IMessageHandler<Event15>,
        ICommandHandler<Response16, int>, ICommandHandler<Command16>, IMessageHandler<Event16>,
        ICommandHandler<Response17, int>, ICommandHandler<Command17>, IMessageHandler<Event17>,
        ICommandHandler<Response18, int>, ICommandHandler<Command18>, IMessageHandler<Event18>,
        ICommandHandler<Response19, int>, ICommandHandler<Command19>, IMessageHandler<Event19>,
        ICommandHandler<Response20, int>, ICommandHandler<Command20>, IMessageHandler<Event20>,
        ICommandHandler<Response21, int>, ICommandHandler<Command21>, IMessageHandler<Event21>,
        ICommandHandler<Response22, int>, ICommandHandler<Command22>, IMessageHandler<Event22>,
        ICommandHandler<Response23, int>, ICommandHandler<Command23>, IMessageHandler<Event23>,
        ICommandHandler<Response24, int>, ICommandHandler<Command24>, IMessageHandler<Event24>,
        ICommandHandler<Response25, int>, ICommandHandler<Command25>, IMessageHandler<Event25>,
        ICommandHandler<Response26, int>, ICommandHandler<Command26>, IMessageHandler<Event26>,
        ICommandHandler<Response27, int>, ICommandHandler<Command27>, IMessageHandler<Event27>,
        ICommandHandler<Response28, int>, ICommandHandler<Command28>, IMessageHandler<Event28>,
        ICommandHandler<Response29, int>, ICommandHandler<Command29>, IMessageHandler<Event29>,
        ICommandHandler<Response30, int>, ICommandHandler<Command30>, IMessageHandler<Event30>,
        ICommandHandler<Response31, int>, ICommandHandler<Command31>, IMessageHandler<Event31>
    {
        private int _sink;

        internal int Sink => _sink;

        public ValueTask<int> Handle(Response00 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command00 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event00 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response01 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command01 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event01 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response02 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command02 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event02 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response03 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command03 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event03 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response04 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command04 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event04 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response05 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command05 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event05 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response06 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command06 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event06 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response07 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command07 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event07 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response08 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command08 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event08 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response09 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command09 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event09 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response10 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command10 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event10 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response11 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command11 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event11 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response12 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command12 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event12 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response13 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command13 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event13 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response14 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command14 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event14 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response15 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command15 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event15 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response16 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command16 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event16 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response17 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command17 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event17 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response18 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command18 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event18 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response19 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command19 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event19 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response20 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command20 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event20 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response21 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command21 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event21 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response22 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command22 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event22 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response23 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command23 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event23 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response24 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command24 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event24 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response25 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command25 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event25 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response26 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command26 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event26 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response27 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command27 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event27 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response28 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command28 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event28 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response29 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command29 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event29 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response30 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command30 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event30 message, CancellationToken cancellationToken) => Add(message.Index);
        public ValueTask<int> Handle(Response31 command, CancellationToken cancellationToken) => new(command.Index);
        public ValueTask Handle(Command31 command, CancellationToken cancellationToken) => Add(command.Index);
        public ValueTask Handle(Event31 message, CancellationToken cancellationToken) => Add(message.Index);

        private ValueTask Add(int index)
        {
            _sink += index + 1;
            return default;
        }
    }

    /// <summary>Mirrors generated static tables and exact closed-interface handler invocation.</summary>
    internal sealed class DispatchTables<TSize> : DispatchMediator, IMedium
        where TSize : IRouteSet
    {
        private delegate ValueTask<TResponse> ResponseDispatch<TResponse>(
            IServiceProvider services,
            ICommand<TResponse> command,
            CancellationToken cancellationToken);

        private delegate ValueTask CommandDispatch(
            IServiceProvider services,
            ICommand command,
            CancellationToken cancellationToken);

        private delegate ValueTask PublishDispatch(
            IServiceProvider services,
            IMessage message,
            CancellationToken cancellationToken);

        private static readonly RouteTables s_routes = new();

        internal DispatchTables(IServiceProvider services)
            : base(services)
        {
        }

        internal static int ResponseCount => s_routes.Responses.Count;
        internal static int CommandCount => s_routes.Commands.Count;
        internal static int PublishCount => s_routes.Published.Count;

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            if (s_routes.Responses.TryGetValue(command.GetType(), out var route))
                return ((ResponseDispatch<TResponse>)route)(Services, command, cancellationToken);

            throw new InvalidOperationException(
                "No exact Medium response-command route for " + command.GetType().FullName + ".");
        }

        public ValueTask Send(ICommand command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            if (s_routes.Commands.TryGetValue(command.GetType(), out var route))
                return route(Services, command, cancellationToken);

            throw new InvalidOperationException(
                "No exact Medium command route for " + command.GetType().FullName + ".");
        }

        public ValueTask Publish(IMessage message, CancellationToken cancellationToken = default)
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));
            if (message is ICommand)
                throw new ArgumentException("Commands must use Send.", nameof(message));

            if (s_routes.Published.TryGetValue(message.GetType(), out var route))
                return route(Services, message, cancellationToken);

            return default;
        }

        private static ValueTask<int> DispatchResponse<TMessage>(
            IServiceProvider services,
            ICommand<int> command,
            CancellationToken cancellationToken)
            where TMessage : ICommand<int>
        {
            var handler = (ICommandHandler<TMessage, int>)(object)services.GetRequiredService<RouteHandlers>();
            return handler.Handle((TMessage)command, cancellationToken);
        }

        private static ValueTask DispatchCommand<TMessage>(
            IServiceProvider services,
            ICommand command,
            CancellationToken cancellationToken)
            where TMessage : ICommand
        {
            var handler = (ICommandHandler<TMessage>)(object)services.GetRequiredService<RouteHandlers>();
            return handler.Handle((TMessage)command, cancellationToken);
        }

        private static ValueTask DispatchPublish<TMessage>(
            IServiceProvider services,
            IMessage message,
            CancellationToken cancellationToken)
            where TMessage : IMessage
        {
            var handler = (IMessageHandler<TMessage>)(object)services.GetRequiredService<RouteHandlers>();
            return handler.Handle((TMessage)message, cancellationToken);
        }

        private sealed class RouteTables
        {
            internal readonly Dictionary<Type, Delegate> Responses = new();
            internal readonly Dictionary<Type, CommandDispatch> Commands = new();
            internal readonly Dictionary<Type, PublishDispatch> Published = new();

            internal RouteTables()
            {
                Add<Response00, Command00, Event00>();
                if (TSize.Count > 1) Add<Response01, Command01, Event01>();
                if (TSize.Count > 2) Add<Response02, Command02, Event02>();
                if (TSize.Count > 3) Add<Response03, Command03, Event03>();
                if (TSize.Count > 4) Add<Response04, Command04, Event04>();
                if (TSize.Count > 5) Add<Response05, Command05, Event05>();
                if (TSize.Count > 6) Add<Response06, Command06, Event06>();
                if (TSize.Count > 7) Add<Response07, Command07, Event07>();
                if (TSize.Count > 8) Add<Response08, Command08, Event08>();
                if (TSize.Count > 9) Add<Response09, Command09, Event09>();
                if (TSize.Count > 10) Add<Response10, Command10, Event10>();
                if (TSize.Count > 11) Add<Response11, Command11, Event11>();
                if (TSize.Count > 12) Add<Response12, Command12, Event12>();
                if (TSize.Count > 13) Add<Response13, Command13, Event13>();
                if (TSize.Count > 14) Add<Response14, Command14, Event14>();
                if (TSize.Count > 15) Add<Response15, Command15, Event15>();
                if (TSize.Count > 16) Add<Response16, Command16, Event16>();
                if (TSize.Count > 17) Add<Response17, Command17, Event17>();
                if (TSize.Count > 18) Add<Response18, Command18, Event18>();
                if (TSize.Count > 19) Add<Response19, Command19, Event19>();
                if (TSize.Count > 20) Add<Response20, Command20, Event20>();
                if (TSize.Count > 21) Add<Response21, Command21, Event21>();
                if (TSize.Count > 22) Add<Response22, Command22, Event22>();
                if (TSize.Count > 23) Add<Response23, Command23, Event23>();
                if (TSize.Count > 24) Add<Response24, Command24, Event24>();
                if (TSize.Count > 25) Add<Response25, Command25, Event25>();
                if (TSize.Count > 26) Add<Response26, Command26, Event26>();
                if (TSize.Count > 27) Add<Response27, Command27, Event27>();
                if (TSize.Count > 28) Add<Response28, Command28, Event28>();
                if (TSize.Count > 29) Add<Response29, Command29, Event29>();
                if (TSize.Count > 30) Add<Response30, Command30, Event30>();
                if (TSize.Count > 31) Add<Response31, Command31, Event31>();
            }

            private void Add<TResponseMessage, TCommandMessage, TEventMessage>()
                where TResponseMessage : ICommand<int>
                where TCommandMessage : ICommand
                where TEventMessage : IMessage
            {
                Responses.Add(
                    typeof(TResponseMessage),
                    new ResponseDispatch<int>(DispatchResponse<TResponseMessage>));
                Commands.Add(
                    typeof(TCommandMessage),
                    new CommandDispatch(DispatchCommand<TCommandMessage>));
                Published.Add(
                    typeof(TEventMessage),
                    new PublishDispatch(DispatchPublish<TEventMessage>));
            }
        }
    }

    /// <summary>Uses the same typed terminals as DispatchTables, replacing only lookup with if-chains.</summary>
    internal sealed class DispatchChains<TSize> : DispatchMediator, IMedium
        where TSize : IRouteSet
    {
        private delegate ValueTask<TResponse> ResponseDispatch<TResponse>(
            IServiceProvider services,
            ICommand<TResponse> command,
            CancellationToken cancellationToken);

        private delegate ValueTask CommandDispatch(
            IServiceProvider services,
            ICommand command,
            CancellationToken cancellationToken);

        private delegate ValueTask PublishDispatch(
            IServiceProvider services,
            IMessage message,
            CancellationToken cancellationToken);

        internal DispatchChains(IServiceProvider services)
            : base(services)
        {
        }

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            var messageType = command.GetType();
            if (messageType == typeof(Response00)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response00>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 1 && messageType == typeof(Response01)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response01>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 2 && messageType == typeof(Response02)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response02>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 3 && messageType == typeof(Response03)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response03>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 4 && messageType == typeof(Response04)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response04>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 5 && messageType == typeof(Response05)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response05>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 6 && messageType == typeof(Response06)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response06>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 7 && messageType == typeof(Response07)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response07>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 8 && messageType == typeof(Response08)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response08>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 9 && messageType == typeof(Response09)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response09>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 10 && messageType == typeof(Response10)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response10>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 11 && messageType == typeof(Response11)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response11>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 12 && messageType == typeof(Response12)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response12>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 13 && messageType == typeof(Response13)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response13>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 14 && messageType == typeof(Response14)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response14>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 15 && messageType == typeof(Response15)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response15>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 16 && messageType == typeof(Response16)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response16>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 17 && messageType == typeof(Response17)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response17>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 18 && messageType == typeof(Response18)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response18>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 19 && messageType == typeof(Response19)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response19>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 20 && messageType == typeof(Response20)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response20>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 21 && messageType == typeof(Response21)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response21>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 22 && messageType == typeof(Response22)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response22>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 23 && messageType == typeof(Response23)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response23>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 24 && messageType == typeof(Response24)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response24>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 25 && messageType == typeof(Response25)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response25>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 26 && messageType == typeof(Response26)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response26>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 27 && messageType == typeof(Response27)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response27>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 28 && messageType == typeof(Response28)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response28>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 29 && messageType == typeof(Response29)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response29>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 30 && messageType == typeof(Response30)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response30>.Adapter)(Services, command, cancellationToken);
            if (TSize.Count > 31 && messageType == typeof(Response31)) return ((ResponseDispatch<TResponse>)ResponseRoute<Response31>.Adapter)(Services, command, cancellationToken);

            throw new InvalidOperationException(
                "No exact Medium response-command route for " + command.GetType().FullName + ".");
        }

        public ValueTask Send(ICommand command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            var messageType = command.GetType();
            if (messageType == typeof(Command00)) return CommandRoute<Command00>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 1 && messageType == typeof(Command01)) return CommandRoute<Command01>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 2 && messageType == typeof(Command02)) return CommandRoute<Command02>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 3 && messageType == typeof(Command03)) return CommandRoute<Command03>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 4 && messageType == typeof(Command04)) return CommandRoute<Command04>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 5 && messageType == typeof(Command05)) return CommandRoute<Command05>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 6 && messageType == typeof(Command06)) return CommandRoute<Command06>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 7 && messageType == typeof(Command07)) return CommandRoute<Command07>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 8 && messageType == typeof(Command08)) return CommandRoute<Command08>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 9 && messageType == typeof(Command09)) return CommandRoute<Command09>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 10 && messageType == typeof(Command10)) return CommandRoute<Command10>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 11 && messageType == typeof(Command11)) return CommandRoute<Command11>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 12 && messageType == typeof(Command12)) return CommandRoute<Command12>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 13 && messageType == typeof(Command13)) return CommandRoute<Command13>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 14 && messageType == typeof(Command14)) return CommandRoute<Command14>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 15 && messageType == typeof(Command15)) return CommandRoute<Command15>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 16 && messageType == typeof(Command16)) return CommandRoute<Command16>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 17 && messageType == typeof(Command17)) return CommandRoute<Command17>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 18 && messageType == typeof(Command18)) return CommandRoute<Command18>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 19 && messageType == typeof(Command19)) return CommandRoute<Command19>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 20 && messageType == typeof(Command20)) return CommandRoute<Command20>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 21 && messageType == typeof(Command21)) return CommandRoute<Command21>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 22 && messageType == typeof(Command22)) return CommandRoute<Command22>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 23 && messageType == typeof(Command23)) return CommandRoute<Command23>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 24 && messageType == typeof(Command24)) return CommandRoute<Command24>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 25 && messageType == typeof(Command25)) return CommandRoute<Command25>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 26 && messageType == typeof(Command26)) return CommandRoute<Command26>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 27 && messageType == typeof(Command27)) return CommandRoute<Command27>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 28 && messageType == typeof(Command28)) return CommandRoute<Command28>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 29 && messageType == typeof(Command29)) return CommandRoute<Command29>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 30 && messageType == typeof(Command30)) return CommandRoute<Command30>.Adapter(Services, command, cancellationToken);
            if (TSize.Count > 31 && messageType == typeof(Command31)) return CommandRoute<Command31>.Adapter(Services, command, cancellationToken);

            throw new InvalidOperationException(
                "No exact Medium command route for " + command.GetType().FullName + ".");
        }

        public ValueTask Publish(IMessage message, CancellationToken cancellationToken = default)
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));
            if (message is ICommand)
                throw new ArgumentException("Commands must use Send.", nameof(message));

            var messageType = message.GetType();
            if (messageType == typeof(Event00)) return PublishRoute<Event00>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 1 && messageType == typeof(Event01)) return PublishRoute<Event01>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 2 && messageType == typeof(Event02)) return PublishRoute<Event02>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 3 && messageType == typeof(Event03)) return PublishRoute<Event03>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 4 && messageType == typeof(Event04)) return PublishRoute<Event04>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 5 && messageType == typeof(Event05)) return PublishRoute<Event05>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 6 && messageType == typeof(Event06)) return PublishRoute<Event06>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 7 && messageType == typeof(Event07)) return PublishRoute<Event07>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 8 && messageType == typeof(Event08)) return PublishRoute<Event08>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 9 && messageType == typeof(Event09)) return PublishRoute<Event09>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 10 && messageType == typeof(Event10)) return PublishRoute<Event10>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 11 && messageType == typeof(Event11)) return PublishRoute<Event11>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 12 && messageType == typeof(Event12)) return PublishRoute<Event12>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 13 && messageType == typeof(Event13)) return PublishRoute<Event13>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 14 && messageType == typeof(Event14)) return PublishRoute<Event14>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 15 && messageType == typeof(Event15)) return PublishRoute<Event15>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 16 && messageType == typeof(Event16)) return PublishRoute<Event16>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 17 && messageType == typeof(Event17)) return PublishRoute<Event17>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 18 && messageType == typeof(Event18)) return PublishRoute<Event18>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 19 && messageType == typeof(Event19)) return PublishRoute<Event19>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 20 && messageType == typeof(Event20)) return PublishRoute<Event20>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 21 && messageType == typeof(Event21)) return PublishRoute<Event21>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 22 && messageType == typeof(Event22)) return PublishRoute<Event22>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 23 && messageType == typeof(Event23)) return PublishRoute<Event23>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 24 && messageType == typeof(Event24)) return PublishRoute<Event24>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 25 && messageType == typeof(Event25)) return PublishRoute<Event25>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 26 && messageType == typeof(Event26)) return PublishRoute<Event26>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 27 && messageType == typeof(Event27)) return PublishRoute<Event27>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 28 && messageType == typeof(Event28)) return PublishRoute<Event28>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 29 && messageType == typeof(Event29)) return PublishRoute<Event29>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 30 && messageType == typeof(Event30)) return PublishRoute<Event30>.Adapter(Services, message, cancellationToken);
            if (TSize.Count > 31 && messageType == typeof(Event31)) return PublishRoute<Event31>.Adapter(Services, message, cancellationToken);

            return default;
        }

        private static ValueTask<int> DispatchResponse<TMessage>(
            IServiceProvider services,
            ICommand<int> command,
            CancellationToken cancellationToken)
            where TMessage : ICommand<int>
        {
            var handler = (ICommandHandler<TMessage, int>)(object)services.GetRequiredService<RouteHandlers>();
            return handler.Handle((TMessage)command, cancellationToken);
        }

        private static ValueTask DispatchCommand<TMessage>(
            IServiceProvider services,
            ICommand command,
            CancellationToken cancellationToken)
            where TMessage : ICommand
        {
            var handler = (ICommandHandler<TMessage>)(object)services.GetRequiredService<RouteHandlers>();
            return handler.Handle((TMessage)command, cancellationToken);
        }

        private static ValueTask DispatchPublish<TMessage>(
            IServiceProvider services,
            IMessage message,
            CancellationToken cancellationToken)
            where TMessage : IMessage
        {
            var handler = (IMessageHandler<TMessage>)(object)services.GetRequiredService<RouteHandlers>();
            return handler.Handle((TMessage)message, cancellationToken);
        }

        private static class ResponseRoute<TMessage>
            where TMessage : ICommand<int>
        {
            internal static readonly Delegate Adapter = new ResponseDispatch<int>(DispatchResponse<TMessage>);
        }

        private static class CommandRoute<TMessage>
            where TMessage : ICommand
        {
            internal static readonly CommandDispatch Adapter = new(DispatchCommand<TMessage>);
        }

        private static class PublishRoute<TMessage>
            where TMessage : IMessage
        {
            internal static readonly PublishDispatch Adapter = new(DispatchPublish<TMessage>);
        }
    }

    internal static class RouteMessages
    {
        internal static readonly ICommand<int>[] Responses =
        {
            new Response00(0), new Response01(1), new Response02(2), new Response03(3),
            new Response04(4), new Response05(5), new Response06(6), new Response07(7),
            new Response08(8), new Response09(9), new Response10(10), new Response11(11),
            new Response12(12), new Response13(13), new Response14(14), new Response15(15),
            new Response16(16), new Response17(17), new Response18(18), new Response19(19),
            new Response20(20), new Response21(21), new Response22(22), new Response23(23),
            new Response24(24), new Response25(25), new Response26(26), new Response27(27),
            new Response28(28), new Response29(29), new Response30(30), new Response31(31),
        };

        internal static readonly ICommand[] Commands =
        {
            new Command00(0), new Command01(1), new Command02(2), new Command03(3),
            new Command04(4), new Command05(5), new Command06(6), new Command07(7),
            new Command08(8), new Command09(9), new Command10(10), new Command11(11),
            new Command12(12), new Command13(13), new Command14(14), new Command15(15),
            new Command16(16), new Command17(17), new Command18(18), new Command19(19),
            new Command20(20), new Command21(21), new Command22(22), new Command23(23),
            new Command24(24), new Command25(25), new Command26(26), new Command27(27),
            new Command28(28), new Command29(29), new Command30(30), new Command31(31),
        };

        internal static readonly IMessage[] Events =
        {
            new Event00(0), new Event01(1), new Event02(2), new Event03(3),
            new Event04(4), new Event05(5), new Event06(6), new Event07(7),
            new Event08(8), new Event09(9), new Event10(10), new Event11(11),
            new Event12(12), new Event13(13), new Event14(14), new Event15(15),
            new Event16(16), new Event17(17), new Event18(18), new Event19(19),
            new Event20(20), new Event21(21), new Event22(22), new Event23(23),
            new Event24(24), new Event25(25), new Event26(26), new Event27(27),
            new Event28(28), new Event29(29), new Event30(30), new Event31(31),
        };
    }
}
