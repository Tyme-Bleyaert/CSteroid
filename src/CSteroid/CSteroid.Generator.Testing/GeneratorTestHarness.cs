using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CSteroid.Generator.Testing
{
    public static class GeneratorTestHarness
    {
        public static Compilation RunGenerator(IIncrementalGenerator[] generators, params string[] sources)
        {
            var (compilation, _) = RunGeneratorCore(generators, sources);

            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errors);

            return compilation;
        }

        /// <summary>
        /// Metadata-lib pattern: compile <paramref name="metadataSources"/> into an in-memory
        /// assembly first, then run the generators against <paramref name="consumerSources"/>
        /// with the metadata assembly referenced (as if via a project reference).
        /// </summary>
        public static Compilation RunGeneratorWithMetadataReference(IIncrementalGenerator[] generators, string[] metadataSources, params string[] consumerSources)
        {
            var (compilation, _) = RunGeneratorWithMetadataReferenceAndDiagnostics(generators, metadataSources, consumerSources);

            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errors);

            return compilation;
        }

        public static (Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithMetadataReferenceAndDiagnostics(IIncrementalGenerator[] generators, string[] metadataSources, params string[] consumerSources)
        {
            var metadataReference = CompileToMetadataReference(metadataSources);
            return RunGeneratorCore(generators, consumerSources, metadataReference);
        }

        private static MetadataReference CompileToMetadataReference(string[] sources)
        {
            var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s));
            var compilation = CSharpCompilation.Create("MetadataAssembly",
                syntaxTrees,
                CreateDefaultReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var emitResult = compilation.Emit(stream);
            var errors = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errors);

            return MetadataReference.CreateFromImage(stream.ToArray());
        }

        private static List<MetadataReference> CreateDefaultReferences()
        {
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CanonicalModelAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(OrmTargetAttribute).Assembly.Location)
            };

            // Add System.Runtime if available (needed for .NET Core / .NET 5+ compilations)
            try
            {
                var systemRuntime = Assembly.Load("System.Runtime");
                references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));

                var dataAnnotationsPath = Path.Combine(Path.GetDirectoryName(systemRuntime.Location) ?? string.Empty, "System.ComponentModel.DataAnnotations.dll");
                if (File.Exists(dataAnnotationsPath))
                {
                    references.Add(MetadataReference.CreateFromFile(dataAnnotationsPath));
                }

                references.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression).Assembly.Location));

                // Generated DI registrations reference Microsoft.Extensions.DependencyInjection.
                references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location));

                var systemLinq = Assembly.Load("System.Linq");
                references.Add(MetadataReference.CreateFromFile(systemLinq.Location));

                var systemLinqQueryable = Assembly.Load("System.Linq.Queryable");
                references.Add(MetadataReference.CreateFromFile(systemLinqQueryable.Location));

                var systemCollections = Assembly.Load("System.Collections");
                references.Add(MetadataReference.CreateFromFile(systemCollections.Location));
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine("Failed to add framework refs: " + ex);
            }

            foreach (var assemblyName in typeof(CanonicalModelAttribute).Assembly.GetReferencedAssemblies())
            {
                try
                {
                    var assembly = Assembly.Load(assemblyName);
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch { }
            }

            return references;
        }

        public static (Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithDiagnostics(IIncrementalGenerator[] generators, params string[] sources)
        {
            return RunGeneratorCore(generators, sources);
        }

        private static (Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorCore(IIncrementalGenerator[] generators, string[] sources, MetadataReference? extraReference = null)
        {
            var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s));
            var references = CreateDefaultReferences();
            if (extraReference != null)
                references.Add(extraReference);

            var compilation = CSharpCompilation.Create("TestAssembly",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create(generators);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
            foreach (var diag in diagnostics)
            {
                System.Console.WriteLine("DIAG: " + diag);
            }

            return (outputCompilation, diagnostics);
        }

        public static string[] GetGeneratedFileNames(Compilation compilation)
        {
            return compilation.SyntaxTrees
                .Select(t => Path.GetFileName(t.FilePath))
                .Where(p => !string.IsNullOrEmpty(p) && p.EndsWith(".cs"))
                .ToArray();
        }

        public static string GetGeneratedSource(Compilation compilation, string hintName)
        {
            var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath != null && t.FilePath.EndsWith(hintName));
            return tree?.ToString() ?? string.Empty;
        }

        public static Assembly EmitAndLoad(Compilation compilation)
        {
            using var stream = new MemoryStream();
            var emitResult = compilation.Emit(stream);
            var errors = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errors);
            stream.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(stream.ToArray());
        }

        public static async Task<object> GetTaskResultAsync(Task task)
        {
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")!.GetValue(task)!;
        }

        public static bool IsValid(object result)
        {
            return (bool)result.GetType().GetProperty("IsValid")!.GetValue(result)!;
        }

        public static List<string> GetFailurePropertyNames(object result)
        {
            var failures = result.GetType().GetProperty("Failures")!.GetValue(result) as System.Collections.IEnumerable;
            var names = new List<string>();
            foreach (var failure in failures!)
            {
                names.Add((string)failure.GetType().GetProperty("PropertyName")!.GetValue(failure)!);
            }

            return names;
        }
    }
}
