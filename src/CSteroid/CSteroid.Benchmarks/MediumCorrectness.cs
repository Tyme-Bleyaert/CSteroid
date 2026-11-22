using CSteroid.Medium;

namespace CSteroid.Benchmarks
{
    internal static class MediumCorrectness
    {
        internal static void RunAll()
        {
            CheckRoutes<OneRoute>();
            CheckRoutes<EightRoutes>();
            CheckRoutes<ThirtyTwoRoutes>();
            CheckBehaviors();
            MediumGeneratorBuildBenchmarks.CheckCorrectness();
        }

        private static void CheckRoutes<TSize>()
            where TSize : IRouteSet
        {
            var table = new DispatchTables<TSize>(BenchmarkServiceProvider.CreateHandlerScope());
            var chain = new DispatchChains<TSize>(BenchmarkServiceProvider.CreateHandlerScope());
            Assert(DispatchTables<TSize>.ResponseCount == TSize.Count, "Response table route count");
            Assert(DispatchTables<TSize>.CommandCount == TSize.Count, "Command table route count");
            Assert(DispatchTables<TSize>.PublishCount == TSize.Count, "Publish table route count");

            for (var index = 0; index < TSize.Count; index++)
            {
                Assert(Read(table.Send(RouteMessages.Responses[index])) == index, "Table response route " + index);
                Assert(Read(chain.Send(RouteMessages.Responses[index])) == index, "Chain response route " + index);

                var tableSink = table.Sink;
                Read(table.Send(RouteMessages.Commands[index]));
                Assert(table.Sink - tableSink == index + 1, "Table command route " + index);

                var chainSink = chain.Sink;
                Read(chain.Send(RouteMessages.Commands[index]));
                Assert(chain.Sink - chainSink == index + 1, "Chain command route " + index);

                tableSink = table.Sink;
                Read(table.Publish(RouteMessages.Events[index]));
                Assert(table.Sink - tableSink == index + 1, "Table Publish route " + index);

                chainSink = chain.Sink;
                Read(chain.Publish(RouteMessages.Events[index]));
                Assert(chain.Sink - chainSink == index + 1, "Chain Publish route " + index);
            }

            CheckUnknownRoutes(
                "Table " + TSize.Count,
                command => table.Send(command),
                command => table.Send(command),
                message => table.Publish(message),
                () => table.Sink);
            CheckUnknownRoutes(
                "Chain " + TSize.Count,
                command => chain.Send(command),
                command => chain.Send(command),
                message => chain.Publish(message),
                () => chain.Sink);

            var consumer = new MediatorConsumer(table);
            Assert(Read(consumer.Send(RouteMessages.Responses[0])) == 0, "Constructor-injected mediator");
        }

        private static void CheckUnknownRoutes(
            string side,
            Func<ICommand<int>, ValueTask<int>> responseSend,
            Func<ICommand, ValueTask> commandSend,
            Func<IMessage, ValueTask> publish,
            Func<int> sink)
        {
            AssertThrows<InvalidOperationException>(() => responseSend(new UnknownResponse()), side + " unknown response");
            AssertThrows<InvalidOperationException>(() => commandSend(new UnknownCommand()), side + " unknown command");

            var before = sink();
            Read(publish(new UnknownEvent()));
            Assert(sink() == before, side + " unknown event must be a no-op");

            AssertThrows<ArgumentNullException>(() => responseSend(null!), side + " null response command");
            AssertThrows<ArgumentNullException>(() => commandSend(null!), side + " null command");
            AssertThrows<ArgumentNullException>(() => publish(null!), side + " null message");
            AssertThrows<ArgumentException>(() => publish(RouteMessages.Commands[0]), side + " Publish rejects commands");
            AssertThrows<InvalidOperationException>(
                () => commandSend((ICommand)RouteMessages.Responses[0]),
                side + " erased response command");
        }

        private static void CheckBehaviors()
        {
            var mediator = new BehaviorMediator(BehaviorMediator.CreateScope());
            Assert(Read(mediator.SendWithoutBehaviors(new BehaviorResponse(), default)) == 42, "Response without behavior");
            Assert(mediator.ResponseBehaviorCalls == 0, "No response behavior selected");
            Assert(Read(mediator.SendWithBehaviors(new BehaviorResponse(), default)) == 42, "Response fixed behavior chain");
            Assert(mediator.ResponseBehaviorCalls == 2, "Two response behaviors execute once");

            Read(mediator.SendWithoutBehaviors(new BehaviorCommand(), default));
            Assert(mediator.Sink == 1 && mediator.CommandBehaviorCalls == 0, "Command without behavior");
            Read(mediator.SendWithBehaviors(new BehaviorCommand(), default));
            Assert(mediator.Sink == 2 && mediator.CommandBehaviorCalls == 2, "Command fixed behavior chain");

            Read(mediator.PublishWithoutBehaviors(new BehaviorEvent(), default));
            Assert(mediator.Sink == 8 && mediator.PublishBehaviorCalls == 0, "Publish without behavior");
            Read(mediator.PublishWithBehaviors(new BehaviorEvent(), default));
            Assert(mediator.Sink == 14, "Publish fixed chain preserves fan-out");
            Assert(mediator.PublishBehaviorCalls == 2, "Publish chain wraps the whole fan-out once");
        }

        private static T Read<T>(ValueTask<T> valueTask)
        {
            return valueTask.GetAwaiter().GetResult();
        }

        private static void Read(ValueTask valueTask)
        {
            valueTask.GetAwaiter().GetResult();
        }

        private static void AssertThrows<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException("Correctness check failed: " + message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Correctness check failed: " + message);
        }
    }
}
