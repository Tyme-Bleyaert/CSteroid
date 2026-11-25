using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Shared;
using static CSteroid.Generator.Shared.OutputConventions;
using static CSteroid.Generator.Shared.SourceTextHelper;

namespace CSteroid.Generator.Generators
{
    [Generator]
    public class CSteroidGenerator : IIncrementalGenerator
    {
        private static readonly DiagnosticDescriptor ConflictingOrmFlags = new DiagnosticDescriptor(
            "CSG101",
            "Conflicting ORM targets",
            "Model '{0}' has conflicting ORM targets ({1}). Only one OrmTarget flag is allowed per generated model.",
            "CSteroid",
            DiagnosticSeverity.Error,
            true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(InterfaceDiscovery.Register(context), Execute);
        }

        private static void Execute(SourceProductionContext spc, GenerationSpec source)
        {
            InterfaceDiscovery.ExecuteWithErrorBoundary(spc, () => ExecuteCore(spc, source));
        }

        private static void ExecuteCore(SourceProductionContext spc, GenerationSpec source)
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
                if (info.OrmTarget != 0)
                {
                    if (!OrmTargetHelper.IsSingleOrmFlag(info.OrmTarget))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            ConflictingOrmFlags,
                            info.InterfaceLocation?.ToLocation() ?? Location.None,
                            info.Target,
                            OrmTargetHelper.GetOrmFlagNames(info.OrmTarget)));
                    }

                    continue;
                }

                if (!policy.IsLocal(info))
                    continue;

                var modelSource = ModelSourceBuilder.GenerateModel(info, targetNames);
                spc.AddSource(GetHintPath(info, GetFileName(info.Target, fileNamingConvention)), ToSourceText(modelSource));
            }
        }
    }
}
