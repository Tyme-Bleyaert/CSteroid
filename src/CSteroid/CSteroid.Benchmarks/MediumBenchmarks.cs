using BenchmarkDotNet.Attributes;
using CSteroid.Medium;
using Microsoft.Extensions.DependencyInjection;

namespace CSteroid.Benchmarks
{
    /// <summary>Compares generated Medium dispatch with MediatR using the same DI provider and scoped lifetimes.</summary>
    [MemoryDiagnoser]
    public class MediumBenchmarks
    {
        private static readonly MediumResponse s_mediumResponse = new();
        private static readonly MediumCommand s_mediumCommand = new();
        private static readonly MediumEvent s_mediumEvent = new();
        private static readonly MediumTracedResponse s_mediumTracedResponse = new();
        private static readonly MediatRResponse s_mediatRResponse = new();
        private static readonly MediatRCommand s_mediatRCommand = new();
        private static readonly MediatREvent s_mediatREvent = new();
        private static readonly MediatRTracedResponse s_mediatRTracedResponse = new();

        private ServiceProvider _mediumProvider;
        private ServiceProvider _mediatRProvider;
        private IServiceScope _mediumScope;
        private IServiceScope _mediatRScope;
        private IMedium _medium;
        private MediatR.IMediator _mediatR;

        internal IMedium Medium => _medium;
        internal ComparisonSink MediumSink { get; private set; }
        internal ComparisonSink MediatRSink { get; private set; }

        [GlobalSetup]
        public void Setup()
        {
            _mediumProvider = new ServiceCollection()
                .AddSingleton<ComparisonSink>()
                .AddMedium()
                .BuildServiceProvider();
            _mediumScope = _mediumProvider.CreateScope();
            _medium = _mediumScope.ServiceProvider.GetRequiredService<IMedium>();
            MediumSink = _mediumScope.ServiceProvider.GetRequiredService<ComparisonSink>();

            var mediatRServices = new ServiceCollection().AddSingleton<ComparisonSink>();
            mediatRServices.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<MediumBenchmarks>();
                configuration.Lifetime = ServiceLifetime.Scoped;
                configuration.AddOpenBehavior(typeof(MediatRFirstResponseBehavior<,>), ServiceLifetime.Scoped);
                configuration.AddOpenBehavior(typeof(MediatRSecondResponseBehavior<,>), ServiceLifetime.Scoped);
            });
            _mediatRProvider = mediatRServices.BuildServiceProvider();
            _mediatRScope = _mediatRProvider.CreateScope();
            _mediatR = _mediatRScope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            MediatRSink = _mediatRScope.ServiceProvider.GetRequiredService<ComparisonSink>();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _mediumScope.Dispose();
            _mediatRScope.Dispose();
            _mediumProvider.Dispose();
            _mediatRProvider.Dispose();
        }

        [Benchmark]
        public ValueTask<int> Medium_Send_Response() => _medium.Send(s_mediumResponse);

        [Benchmark]
        public Task<int> MediatR_Send_Response() => _mediatR.Send(s_mediatRResponse);

        [Benchmark]
        public ValueTask Medium_Send_Command() => _medium.Send(s_mediumCommand);

        [Benchmark]
        public Task MediatR_Send_Command() => _mediatR.Send(s_mediatRCommand);

        [Benchmark]
        public ValueTask Medium_Publish() => _medium.Publish(s_mediumEvent);

        [Benchmark]
        public Task MediatR_Publish() => _mediatR.Publish(s_mediatREvent);

        [Benchmark]
        public ValueTask<int> Medium_Send_Response_TwoBehaviors() => _medium.Send(s_mediumTracedResponse);

        [Benchmark]
        public Task<int> MediatR_Send_Response_TwoBehaviors() => _mediatR.Send(s_mediatRTracedResponse);
    }
}
