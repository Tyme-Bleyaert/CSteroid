using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.AutoMediator
{
    /// <summary>Runtime symbols resolved by exact metadata name; the compiler has no runtime binary reference.</summary>
    internal sealed class AutoMediatorRuntimeContracts
    {
        internal const string MessageMetadataName = "CSteroid.AutoMediator.IMessage";
        internal const string CommandMetadataName = "CSteroid.AutoMediator.ICommand";
        internal const string CommandOfTMetadataName = "CSteroid.AutoMediator.ICommand`1";
        internal const string CommandHandlerMetadataName = "CSteroid.AutoMediator.ICommandHandler`1";
        internal const string CommandHandlerOfTMetadataName = "CSteroid.AutoMediator.ICommandHandler`2";
        internal const string MessageHandlerMetadataName = "CSteroid.AutoMediator.IMessageHandler`1";
        internal const string SenderMetadataName = "CSteroid.AutoMediator.ISender";
        internal const string PublisherMetadataName = "CSteroid.AutoMediator.IPublisher";
        internal const string HandlerLifetimeAttributeMetadataName = "CSteroid.AutoMediator.HandlerLifetimeAttribute";
        internal const string BehaviorAttributeMetadataName = "CSteroid.AutoMediator.AutoMediatorBehaviorAttribute";
        internal const string PipelineBehaviorMetadataName = "CSteroid.AutoMediator.IPipelineBehavior`1";
        internal const string PipelineBehaviorOfTMetadataName = "CSteroid.AutoMediator.IPipelineBehavior`2";
        internal const string CompositionRootAttributeMetadataName = "CSteroid.AutoMediator.AutoMediatorCompositionRootAttribute";
        internal const string ManifestAttributeMetadataName = "CSteroid.AutoMediator.AutoMediatorManifestAttribute";

        private AutoMediatorRuntimeContracts(Compilation compilation)
        {
            Message = compilation.GetTypeByMetadataName(MessageMetadataName)!;
            Command = compilation.GetTypeByMetadataName(CommandMetadataName)!;
            CommandOfT = compilation.GetTypeByMetadataName(CommandOfTMetadataName)!;
            CommandHandler = compilation.GetTypeByMetadataName(CommandHandlerMetadataName)!;
            CommandHandlerOfT = compilation.GetTypeByMetadataName(CommandHandlerOfTMetadataName)!;
            MessageHandler = compilation.GetTypeByMetadataName(MessageHandlerMetadataName)!;
            Sender = compilation.GetTypeByMetadataName(SenderMetadataName)!;
            Publisher = compilation.GetTypeByMetadataName(PublisherMetadataName)!;
            HandlerLifetimeAttribute = compilation.GetTypeByMetadataName(HandlerLifetimeAttributeMetadataName);
            BehaviorAttribute = compilation.GetTypeByMetadataName(BehaviorAttributeMetadataName);
            PipelineBehavior = compilation.GetTypeByMetadataName(PipelineBehaviorMetadataName);
            PipelineBehaviorOfT = compilation.GetTypeByMetadataName(PipelineBehaviorOfTMetadataName);
            CompositionRootAttribute = compilation.GetTypeByMetadataName(CompositionRootAttributeMetadataName)!;
            ManifestAttribute = compilation.GetTypeByMetadataName(ManifestAttributeMetadataName)!;
        }

        internal INamedTypeSymbol Message { get; }
        internal INamedTypeSymbol Command { get; }
        internal INamedTypeSymbol CommandOfT { get; }
        internal INamedTypeSymbol CommandHandler { get; }
        internal INamedTypeSymbol CommandHandlerOfT { get; }
        internal INamedTypeSymbol MessageHandler { get; }
        internal INamedTypeSymbol Sender { get; }
        internal INamedTypeSymbol Publisher { get; }
        internal INamedTypeSymbol? HandlerLifetimeAttribute { get; }
        internal INamedTypeSymbol? BehaviorAttribute { get; }
        internal INamedTypeSymbol? PipelineBehavior { get; }
        internal INamedTypeSymbol? PipelineBehaviorOfT { get; }
        internal INamedTypeSymbol CompositionRootAttribute { get; }
        internal INamedTypeSymbol ManifestAttribute { get; }

        internal static AutoMediatorRuntimeContracts? Resolve(Compilation compilation)
        {
            if (compilation.GetTypeByMetadataName(MessageMetadataName) is null
                || compilation.GetTypeByMetadataName(CommandMetadataName) is null
                || compilation.GetTypeByMetadataName(CommandOfTMetadataName) is null
                || compilation.GetTypeByMetadataName(CommandHandlerMetadataName) is null
                || compilation.GetTypeByMetadataName(CommandHandlerOfTMetadataName) is null
                || compilation.GetTypeByMetadataName(MessageHandlerMetadataName) is null
                || compilation.GetTypeByMetadataName(SenderMetadataName) is null
                || compilation.GetTypeByMetadataName(PublisherMetadataName) is null
                || compilation.GetTypeByMetadataName(CompositionRootAttributeMetadataName) is null
                || compilation.GetTypeByMetadataName(ManifestAttributeMetadataName) is null)
            {
                return null;
            }
            return new AutoMediatorRuntimeContracts(compilation);
        }
    }
}
