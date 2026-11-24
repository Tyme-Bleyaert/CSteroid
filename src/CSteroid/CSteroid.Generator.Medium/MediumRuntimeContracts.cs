using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Medium
{
    /// <summary>Runtime symbols resolved by exact metadata name; the compiler has no runtime binary reference.</summary>
    internal sealed class MediumRuntimeContracts
    {
        internal const string MessageMetadataName = "CSteroid.Medium.IMessage";
        internal const string CommandMetadataName = "CSteroid.Medium.ICommand";
        internal const string CommandOfTMetadataName = "CSteroid.Medium.ICommand`1";
        internal const string StreamCommandMetadataName = "CSteroid.Medium.IStreamCommand";
        internal const string StreamCommandOfTMetadataName = "CSteroid.Medium.IStreamCommand`1";
        internal const string CommandHandlerMetadataName = "CSteroid.Medium.ICommandHandler`1";
        internal const string CommandHandlerOfTMetadataName = "CSteroid.Medium.ICommandHandler`2";
        internal const string StreamCommandHandlerMetadataName = "CSteroid.Medium.IStreamCommandHandler`2";
        internal const string MessageHandlerMetadataName = "CSteroid.Medium.IMessageHandler`1";
        internal const string SenderMetadataName = "CSteroid.Medium.ISender";
        internal const string StreamSenderMetadataName = "CSteroid.Medium.IStreamSender";
        internal const string PublisherMetadataName = "CSteroid.Medium.IPublisher";
        internal const string HandlerLifetimeAttributeMetadataName = "CSteroid.Medium.HandlerLifetimeAttribute";
        internal const string MessageHandlerAttributeMetadataName = "CSteroid.Medium.MessageHandlerAttribute";
        internal const string BehaviorAttributeMetadataName = "CSteroid.Medium.MediumBehaviorAttribute";
        internal const string PipelineBehaviorMetadataName = "CSteroid.Medium.IPipelineBehavior`1";
        internal const string PipelineBehaviorOfTMetadataName = "CSteroid.Medium.IPipelineBehavior`2";
        internal const string StreamPipelineBehaviorMetadataName = "CSteroid.Medium.IStreamPipelineBehavior`2";
        internal const string CompositionRootAttributeMetadataName = "CSteroid.Medium.MediumCompositionRootAttribute";
        internal const string ManifestAttributeMetadataName = "CSteroid.Medium.MediumManifestAttribute";

        private MediumRuntimeContracts(Compilation compilation)
        {
            Message = compilation.GetTypeByMetadataName(MessageMetadataName)!;
            Command = compilation.GetTypeByMetadataName(CommandMetadataName)!;
            CommandOfT = compilation.GetTypeByMetadataName(CommandOfTMetadataName)!;
            StreamCommand = compilation.GetTypeByMetadataName(StreamCommandMetadataName)!;
            StreamCommandOfT = compilation.GetTypeByMetadataName(StreamCommandOfTMetadataName)!;
            CommandHandler = compilation.GetTypeByMetadataName(CommandHandlerMetadataName)!;
            CommandHandlerOfT = compilation.GetTypeByMetadataName(CommandHandlerOfTMetadataName)!;
            StreamCommandHandler = compilation.GetTypeByMetadataName(StreamCommandHandlerMetadataName)!;
            MessageHandler = compilation.GetTypeByMetadataName(MessageHandlerMetadataName)!;
            Sender = compilation.GetTypeByMetadataName(SenderMetadataName)!;
            StreamSender = compilation.GetTypeByMetadataName(StreamSenderMetadataName)!;
            Publisher = compilation.GetTypeByMetadataName(PublisherMetadataName)!;
            HandlerLifetimeAttribute = compilation.GetTypeByMetadataName(HandlerLifetimeAttributeMetadataName);
            MessageHandlerAttribute = compilation.GetTypeByMetadataName(MessageHandlerAttributeMetadataName);
            BehaviorAttribute = compilation.GetTypeByMetadataName(BehaviorAttributeMetadataName);
            PipelineBehavior = compilation.GetTypeByMetadataName(PipelineBehaviorMetadataName);
            PipelineBehaviorOfT = compilation.GetTypeByMetadataName(PipelineBehaviorOfTMetadataName);
            StreamPipelineBehavior = compilation.GetTypeByMetadataName(StreamPipelineBehaviorMetadataName);
            CompositionRootAttribute = compilation.GetTypeByMetadataName(CompositionRootAttributeMetadataName)!;
            ManifestAttribute = compilation.GetTypeByMetadataName(ManifestAttributeMetadataName)!;
        }

        internal INamedTypeSymbol Message { get; }
        internal INamedTypeSymbol Command { get; }
        internal INamedTypeSymbol CommandOfT { get; }
        internal INamedTypeSymbol StreamCommand { get; }
        internal INamedTypeSymbol StreamCommandOfT { get; }
        internal INamedTypeSymbol CommandHandler { get; }
        internal INamedTypeSymbol CommandHandlerOfT { get; }
        internal INamedTypeSymbol StreamCommandHandler { get; }
        internal INamedTypeSymbol MessageHandler { get; }
        internal INamedTypeSymbol Sender { get; }
        internal INamedTypeSymbol StreamSender { get; }
        internal INamedTypeSymbol Publisher { get; }
        internal INamedTypeSymbol? HandlerLifetimeAttribute { get; }
        internal INamedTypeSymbol? MessageHandlerAttribute { get; }
        internal INamedTypeSymbol? BehaviorAttribute { get; }
        internal INamedTypeSymbol? PipelineBehavior { get; }
        internal INamedTypeSymbol? PipelineBehaviorOfT { get; }
        internal INamedTypeSymbol? StreamPipelineBehavior { get; }
        internal INamedTypeSymbol CompositionRootAttribute { get; }
        internal INamedTypeSymbol ManifestAttribute { get; }

        internal static MediumRuntimeContracts? Resolve(Compilation compilation)
        {
            if (compilation.GetTypeByMetadataName(MessageMetadataName) is null
                || compilation.GetTypeByMetadataName(CommandMetadataName) is null
                || compilation.GetTypeByMetadataName(CommandOfTMetadataName) is null
                || compilation.GetTypeByMetadataName(StreamCommandMetadataName) is null
                || compilation.GetTypeByMetadataName(StreamCommandOfTMetadataName) is null
                || compilation.GetTypeByMetadataName(CommandHandlerMetadataName) is null
                || compilation.GetTypeByMetadataName(CommandHandlerOfTMetadataName) is null
                || compilation.GetTypeByMetadataName(StreamCommandHandlerMetadataName) is null
                || compilation.GetTypeByMetadataName(MessageHandlerMetadataName) is null
                || compilation.GetTypeByMetadataName(SenderMetadataName) is null
                || compilation.GetTypeByMetadataName(StreamSenderMetadataName) is null
                || compilation.GetTypeByMetadataName(PublisherMetadataName) is null
                || compilation.GetTypeByMetadataName(CompositionRootAttributeMetadataName) is null
                || compilation.GetTypeByMetadataName(ManifestAttributeMetadataName) is null)
            {
                return null;
            }
            return new MediumRuntimeContracts(compilation);
        }
    }
}
