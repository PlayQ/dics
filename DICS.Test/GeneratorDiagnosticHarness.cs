using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using DICS.Attribute;
using DICS.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DICS.Test
{
    /// <summary>
    /// Test harness that compiles a snippet of C# source against an in-memory compilation
    /// (with the DICS runtime + its attributes referenced) and runs the DICS source
    /// generators. Returns the merged set of diagnostics so tests can assert which
    /// rules fired (or did not).
    /// </summary>
    internal static class GeneratorDiagnosticHarness
    {
        private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

        private static ImmutableArray<MetadataReference> BuildReferences()
        {
            var trustedAssemblies =
                ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            var refs = trustedAssemblies
                .Select(p => MetadataReference.CreateFromFile(p))
                .Cast<MetadataReference>()
                .ToList();

            // The runtime and its attributes — needed so the snippet can name [LiftConstructor]
            // / [Inject] / Key<T> / etc.
            refs.Add(MetadataReference.CreateFromFile(typeof(Key).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(LiftConstructor).Assembly.Location));

            return refs.ToImmutableArray();
        }

        public sealed record Result(ImmutableArray<Diagnostic> Diagnostics)
        {
            public bool Fired(string id) => Diagnostics.Any(d => d.Id == id);
            public int Count(string id) => Diagnostics.Count(d => d.Id == id);
        }

        /// <param name="source">Stand-alone C# source the generators are run against.</param>
        /// <param name="generators">Generators to run. Defaults to ALL DICS generators.</param>
        public static Result Run(string source, params IIncrementalGenerator[] generators)
        {
            if (generators.Length == 0) generators = DefaultGenerators();

            var tree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create(
                assemblyName: "harness-" + Guid.NewGuid().ToString("N"),
                syntaxTrees: new[] { tree },
                references: References,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create(generators);
            driver = (CSharpGeneratorDriver)driver
                .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            var runResult = driver.GetRunResult();

            // Keep only diagnostics that originate from DICS itself (INJ####) plus the
            // CS#### errors the C# compiler raises against the generated code itself —
            // anything else (e.g. unrelated warnings from the snippet under test) is
            // noise from the harness's perspective.
            var diagnostics = runResult.Diagnostics
                .Concat(outputCompilation.GetDiagnostics()
                    .Where(d => d.Id.StartsWith("INJ", StringComparison.Ordinal)
                                || (d.Severity == DiagnosticSeverity.Error
                                    && d.Id.StartsWith("CS", StringComparison.Ordinal))))
                .ToImmutableArray();

            return new Result(diagnostics);
        }

        public static IIncrementalGenerator[] DefaultGenerators() => new IIncrementalGenerator[]
        {
            new LiftConstructorGenerator(),
            new LiftInitializerGenerator(),
            new LiftFactoryGenerator(),
            new InjectionValidatorGenerator(),
        };
    }
}
