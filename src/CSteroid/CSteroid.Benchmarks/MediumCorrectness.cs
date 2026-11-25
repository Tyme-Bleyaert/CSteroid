using CSteroid.Medium;

namespace CSteroid.Benchmarks
{
    internal static class MediumCorrectness
    {
        internal static void RunAll()
        {
            CheckComparisons();
            MediumGeneratorBuildBenchmarks.CheckCorrectness();
        }

        private static void CheckComparisons()
        {
            var benchmark = new MediumBenchmarks();
            benchmark.Setup();
            try
            {
                Assert(Read(benchmark.Medium_Send_Response()) == 42, "Medium response");
                Assert(Read(benchmark.MediatR_Send_Response()) == 42, "MediatR response");
                Assert(benchmark.MediumSink.BehaviorCalls == 0, "Medium plain response behaviors");
                Assert(benchmark.MediatRSink.BehaviorCalls == 0, "MediatR plain response behaviors");

                var mediumCommands = benchmark.MediumSink.CommandCalls;
                Read(benchmark.Medium_Send_Command());
                Assert(benchmark.MediumSink.CommandCalls - mediumCommands == 1, "Medium command");
                var mediatRCommands = benchmark.MediatRSink.CommandCalls;
                Read(benchmark.MediatR_Send_Command());
                Assert(benchmark.MediatRSink.CommandCalls - mediatRCommands == 1, "MediatR command");

                var mediumPublish = benchmark.MediumSink.PublishCalls;
                Read(benchmark.Medium_Publish());
                Assert(benchmark.MediumSink.PublishCalls - mediumPublish == 6, "Medium Publish fan-out");
                var mediatRPublish = benchmark.MediatRSink.PublishCalls;
                Read(benchmark.MediatR_Publish());
                Assert(benchmark.MediatRSink.PublishCalls - mediatRPublish == 6, "MediatR Publish fan-out");

                Assert(Read(benchmark.Medium_Send_Response_TwoBehaviors()) == 42, "Medium response pipeline");
                Assert(benchmark.MediumSink.BehaviorCalls == 2, "Medium behavior count");
                Assert(Read(benchmark.MediatR_Send_Response_TwoBehaviors()) == 42, "MediatR response pipeline");
                Assert(benchmark.MediatRSink.BehaviorCalls == 2, "MediatR behavior count");

                // Delegates keep the compile-time unknown-route analyzer out of this runtime check.
                Func<ICommand<int>, CancellationToken, ValueTask<int>> responseSend = benchmark.Medium.Send;
                Func<ICommand, CancellationToken, ValueTask> commandSend = benchmark.Medium.Send;
                Func<IMessage, CancellationToken, ValueTask> publish = benchmark.Medium.Publish;
                AssertThrows<InvalidOperationException>(
                    () => Read(responseSend(new UnknownMediumResponse(), default)),
                    "Medium unknown response");
                AssertThrows<InvalidOperationException>(
                    () => Read(commandSend(new UnknownMediumCommand(), default)),
                    "Medium unknown command");
                Read(publish(new UnknownMediumEvent(), default));
            }
            finally
            {
                benchmark.Cleanup();
            }
        }

        private static T Read<T>(ValueTask<T> valueTask) => valueTask.GetAwaiter().GetResult();
        private static T Read<T>(Task<T> task) => task.GetAwaiter().GetResult();
        private static void Read(ValueTask valueTask) => valueTask.GetAwaiter().GetResult();
        private static void Read(Task task) => task.GetAwaiter().GetResult();

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
