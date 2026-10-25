using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Shared;
using static CSteroid.Generator.Shared.GeneratorConstants;
using static CSteroid.Generator.Shared.OutputConventions;
using static CSteroid.Generator.Shared.SourceTextHelper;

namespace CSteroid.Generator.Dapper
{
    [Generator]
    public class CSteroidDapperGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(InterfaceDiscovery.Register(context), Execute);
        }

        private static void Execute(SourceProductionContext spc, (ImmutableArray<INamedTypeSymbol> Symbols, Compilation Compilation) source)
        {
            InterfaceDiscovery.ExecuteWithErrorBoundary(spc, () => ExecuteCore(spc, source));
        }

        private static void ExecuteCore(SourceProductionContext spc, (ImmutableArray<INamedTypeSymbol> Symbols, Compilation Compilation) source)
        {
            var modelInfos = InterfaceDiscovery.BuildModelInfos(spc, source.Symbols, source.Compilation);
            if (modelInfos.Count == 0)
                return;

            var fileNamingConvention = GetFileNamingConvention(source.Compilation.Assembly);
            var policy = EmissionPolicy.For(source.Compilation);
            var targetNames = new HashSet<string>(modelInfos.Select(m => m.Target), StringComparer.Ordinal);

            foreach (var info in modelInfos.OrderBy(m => m.Target, StringComparer.Ordinal))
            {
                if (info.OrmTarget != OrmTargetDapper)
                    continue;

                if (!policy.IsLocal(info))
                    continue;

                var modelSource = ModelSourceBuilder.GenerateModel(info, targetNames);
                spc.AddSource(GetHintPath(info, GetFileName(info.Target, fileNamingConvention)), ToSourceText(modelSource));
            }
        }
    }
}
