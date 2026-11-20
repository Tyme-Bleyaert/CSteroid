using BenchmarkDotNet.Attributes;
using CSteroid.AutoMediator;

namespace CSteroid.Benchmarks
{
    /// <summary>
    /// Compares static route tables with exact-type if-chains using equivalent closed delegates,
    /// scoped providers, and typed handlers. Handwritten calls isolate dispatch and resolution cost.
    /// </summary>
    [MemoryDiagnoser]
    public class AutoMediatorDispatchBenchmarks
    {
        private static readonly ICommand<int> s_unknownResponse = new UnknownResponse();
        private static readonly ICommand s_unknownCommand = new UnknownCommand();
        private static readonly IMessage s_unknownEvent = new UnknownEvent();

        private MediatorConsumer _table1;
        private MediatorConsumer _table8;
        private MediatorConsumer _table32;
        private MediatorConsumer _chain1;
        private MediatorConsumer _chain8;
        private MediatorConsumer _chain32;
        private RouteHandlers _handwritten1;
        private RouteHandlers _handwritten8;
        private RouteHandlers _handwritten32;
        private ICommand<int> _response;
        private ICommand _command;
        private IMessage _event;

        [Params(1, 8, 32)]
        public int RouteCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var commonIndex = RouteCount - 1;
            _response = RouteMessages.Responses[commonIndex];
            _command = RouteMessages.Commands[commonIndex];
            _event = RouteMessages.Events[commonIndex];

            switch (RouteCount)
            {
                case 1:
                    _table1 = new MediatorConsumer(new DispatchTables<OneRoute>(BenchmarkServiceProvider.CreateHandlerScope()));
                    _chain1 = new MediatorConsumer(new DispatchChains<OneRoute>(BenchmarkServiceProvider.CreateHandlerScope()));
                    _handwritten1 = new RouteHandlers();
                    Warm<OneRoute>(_table1, _chain1);
                    break;
                case 8:
                    _table8 = new MediatorConsumer(new DispatchTables<EightRoutes>(BenchmarkServiceProvider.CreateHandlerScope()));
                    _chain8 = new MediatorConsumer(new DispatchChains<EightRoutes>(BenchmarkServiceProvider.CreateHandlerScope()));
                    _handwritten8 = new RouteHandlers();
                    Warm<EightRoutes>(_table8, _chain8);
                    break;
                case 32:
                    _table32 = new MediatorConsumer(new DispatchTables<ThirtyTwoRoutes>(BenchmarkServiceProvider.CreateHandlerScope()));
                    _chain32 = new MediatorConsumer(new DispatchChains<ThirtyTwoRoutes>(BenchmarkServiceProvider.CreateHandlerScope()));
                    _handwritten32 = new RouteHandlers();
                    Warm<ThirtyTwoRoutes>(_table32, _chain32);
                    break;
            }
        }

        [Benchmark(Description = "Table response, hot common route")]
        public ValueTask<int> Table_Response_Common() => RouteCount switch
        {
            1 => _table1.Send(_response),
            8 => _table8.Send(_response),
            _ => _table32.Send(_response),
        };

        [Benchmark(Description = "If-chain response, hot common route")]
        public ValueTask<int> Chain_Response_Common() => RouteCount switch
        {
            1 => _chain1.Send(_response),
            8 => _chain8.Send(_response),
            _ => _chain32.Send(_response),
        };

        [Benchmark(Description = "Handwritten typed response handler")]
        public ValueTask<int> Handwritten_Response_Common() => RouteCount switch
        {
            1 => _handwritten1.Handle((Response00)_response, default),
            8 => _handwritten8.Handle((Response07)_response, default),
            _ => _handwritten32.Handle((Response31)_response, default),
        };

        [Benchmark(Description = "Table non-response command, hot common route")]
        public ValueTask Table_Command_Common() => RouteCount switch
        {
            1 => _table1.Send(_command),
            8 => _table8.Send(_command),
            _ => _table32.Send(_command),
        };

        [Benchmark(Description = "If-chain non-response command, hot common route")]
        public ValueTask Chain_Command_Common() => RouteCount switch
        {
            1 => _chain1.Send(_command),
            8 => _chain8.Send(_command),
            _ => _chain32.Send(_command),
        };

        [Benchmark(Description = "Handwritten typed command handler")]
        public ValueTask Handwritten_Command_Common() => RouteCount switch
        {
            1 => _handwritten1.Handle((Command00)_command, default),
            8 => _handwritten8.Handle((Command07)_command, default),
            _ => _handwritten32.Handle((Command31)_command, default),
        };

        [Benchmark(Description = "Table Publish, hot common route")]
        public ValueTask Table_Publish_Common() => RouteCount switch
        {
            1 => _table1.Publish(_event),
            8 => _table8.Publish(_event),
            _ => _table32.Publish(_event),
        };

        [Benchmark(Description = "If-chain Publish, hot common route")]
        public ValueTask Chain_Publish_Common() => RouteCount switch
        {
            1 => _chain1.Publish(_event),
            8 => _chain8.Publish(_event),
            _ => _chain32.Publish(_event),
        };

        [Benchmark(Description = "Handwritten typed message handler")]
        public ValueTask Handwritten_Publish_Common() => RouteCount switch
        {
            1 => _handwritten1.Handle((Event00)_event, default),
            8 => _handwritten8.Handle((Event07)_event, default),
            _ => _handwritten32.Handle((Event31)_event, default),
        };

        [Benchmark(Description = "Table response, unknown route")]
        public string Table_Response_Unknown()
        {
            try
            {
                _ = RouteCount switch
                {
                    1 => _table1.Send(s_unknownResponse),
                    8 => _table8.Send(s_unknownResponse),
                    _ => _table32.Send(s_unknownResponse),
                };
                return "unexpected";
            }
            catch (InvalidOperationException)
            {
                return "missing";
            }
        }

        [Benchmark(Description = "If-chain response, unknown route")]
        public string Chain_Response_Unknown()
        {
            try
            {
                _ = RouteCount switch
                {
                    1 => _chain1.Send(s_unknownResponse),
                    8 => _chain8.Send(s_unknownResponse),
                    _ => _chain32.Send(s_unknownResponse),
                };
                return "unexpected";
            }
            catch (InvalidOperationException)
            {
                return "missing";
            }
        }

        [Benchmark(Description = "Table non-response command, unknown route")]
        public string Table_Command_Unknown()
        {
            try
            {
                _ = RouteCount switch
                {
                    1 => _table1.Send(s_unknownCommand),
                    8 => _table8.Send(s_unknownCommand),
                    _ => _table32.Send(s_unknownCommand),
                };
                return "unexpected";
            }
            catch (InvalidOperationException)
            {
                return "missing";
            }
        }

        [Benchmark(Description = "If-chain non-response command, unknown route")]
        public string Chain_Command_Unknown()
        {
            try
            {
                _ = RouteCount switch
                {
                    1 => _chain1.Send(s_unknownCommand),
                    8 => _chain8.Send(s_unknownCommand),
                    _ => _chain32.Send(s_unknownCommand),
                };
                return "unexpected";
            }
            catch (InvalidOperationException)
            {
                return "missing";
            }
        }

        [Benchmark(Description = "Table Publish, unknown event no-op")]
        public ValueTask Table_Publish_Unknown() => RouteCount switch
        {
            1 => _table1.Publish(s_unknownEvent),
            8 => _table8.Publish(s_unknownEvent),
            _ => _table32.Publish(s_unknownEvent),
        };

        [Benchmark(Description = "If-chain Publish, unknown event no-op")]
        public ValueTask Chain_Publish_Unknown() => RouteCount switch
        {
            1 => _chain1.Publish(s_unknownEvent),
            8 => _chain8.Publish(s_unknownEvent),
            _ => _chain32.Publish(s_unknownEvent),
        };

        private static void Warm<TSize>(MediatorConsumer table, MediatorConsumer chain)
            where TSize : IRouteSet
        {
            var index = TSize.Count - 1;
            table.Send(RouteMessages.Responses[index]).GetAwaiter().GetResult();
            chain.Send(RouteMessages.Responses[index]).GetAwaiter().GetResult();
            table.Send(RouteMessages.Commands[index]).GetAwaiter().GetResult();
            chain.Send(RouteMessages.Commands[index]).GetAwaiter().GetResult();
            table.Publish(RouteMessages.Events[index]).GetAwaiter().GetResult();
            chain.Publish(RouteMessages.Events[index]).GetAwaiter().GetResult();
        }
    }

    [MemoryDiagnoser]
    public class AutoMediatorBehaviorBenchmarks
    {
        private static readonly BehaviorResponse s_response = new();
        private static readonly BehaviorCommand s_command = new();
        private static readonly BehaviorEvent s_event = new();
        private BehaviorMediator _mediator;

        [GlobalSetup]
        public void Setup()
        {
            _mediator = new BehaviorMediator(BehaviorMediator.CreateScope());
        }

        [Benchmark]
        public ValueTask<int> Response_NoBehavior() => _mediator.SendWithoutBehaviors(s_response, default);

        [Benchmark]
        public ValueTask<int> Response_TwoFixedBehaviors() => _mediator.SendWithBehaviors(s_response, default);

        [Benchmark]
        public ValueTask Command_NoBehavior() => _mediator.SendWithoutBehaviors(s_command, default);

        [Benchmark]
        public ValueTask Command_TwoFixedBehaviors() => _mediator.SendWithBehaviors(s_command, default);

        [Benchmark]
        public ValueTask Publish_NoBehavior() => _mediator.PublishWithoutBehaviors(s_event, default);

        [Benchmark]
        public ValueTask Publish_TwoFixedBehaviors() => _mediator.PublishWithBehaviors(s_event, default);
    }
}
