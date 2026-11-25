using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Shared;
using static CSteroid.Generator.Shared.GeneratorConstants;
using static CSteroid.Generator.Shared.OutputConventions;
using static CSteroid.Generator.Shared.SourceTextHelper;

namespace CSteroid.Generator.EFCore
{
    [Generator]
    public class CSteroidEFCoreGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var source = InterfaceDiscovery.Register(context);
            var canEmitConfiguration = context.CompilationProvider
                .Select(static (compilation, _) => EfCoreConfigurationBuilder.CanEmit(compilation));

            context.RegisterSourceOutput(
                source.Combine(canEmitConfiguration),
                static (spc, input) => Execute(spc, input.Left, input.Right));
        }

        private static void Execute(SourceProductionContext spc, GenerationSpec source, bool canEmitConfiguration)
        {
            InterfaceDiscovery.ExecuteWithErrorBoundary(spc, () => ExecuteCore(spc, source, canEmitConfiguration));
        }

        private static void ExecuteCore(SourceProductionContext spc, GenerationSpec source, bool canEmitConfiguration)
        {
            if (source.Models.Count == 0)
                return;

            ConfigDiagnostics.Report(spc, source);
            var modelInfos = source.MergedModels;
            var fileNamingConvention = source.Config.FileNamingConvention;
            var policy = new EmissionPolicy(source.Config.GenerateOnlyNamespaces);
            var targetNames = new HashSet<string>(modelInfos.Select(m => m.Target), StringComparer.Ordinal);

            foreach (var info in modelInfos.OrderBy(m => m.Target, StringComparer.Ordinal))
            {
                if (info.OrmTarget != OrmTargetEFCore || !policy.IsLocal(info))
                    continue;

                var modelSource = ModelSourceBuilder.GenerateModel(info, targetNames);
                spc.AddSource(GetHintPath(info, GetFileName(info.Target, fileNamingConvention)), ToSourceText(modelSource));

                if (canEmitConfiguration)
                {
                    var configurationSource = EfCoreConfigurationBuilder.GenerateConfiguration(info);
                    spc.AddSource(EfCoreConfigurationBuilder.GetConfigurationHintPath(info), ToSourceText(configurationSource));
                }
            }
        }
    }
}
