using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using NexusLabs.Needlr.Generators.CodeGen;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Incremental source generator that emits a public constructor for partial classes
/// eligible for generated-constructor generation, either via
/// <c>[GenerateConstructor]</c> on the class or a positive field-level constructor
/// guard trigger.
/// </summary>
/// <remarks>
/// The pipeline is genuinely per-type: <see cref="ConstructorGenerationDiscoveryHelper.TryCreateCanonicalModel"/>
/// converts each candidate class declaration directly into an equatable
/// <see cref="GeneratedConstructorModel"/> (never passing an <see cref="INamedTypeSymbol"/>
/// further down the pipeline), and the only cross-type context combined into each
/// model is a small, independently-cacheable <see cref="GeneratedConstructorEmitContext"/>
/// -- never the whole <see cref="Compilation"/>. As a result, editing one type's fields
/// recomputes and re-emits only that type; every other eligible type's cached model and
/// generated source are reused unchanged.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class GeneratedConstructorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateModels = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => ConstructorGenerationDiscoveryHelper.IsCandidateClassDeclaration(node),
                transform: static (ctx, _) => ConstructorGenerationDiscoveryHelper.TryCreateCanonicalModel(ctx))
            .WithTrackingName(GeneratedConstructorTrackingNames.Candidates);

        var models = candidateModels
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .WithTrackingName(GeneratedConstructorTrackingNames.Models);

        // Only cheap, independently-cacheable scalars are derived from the compilation
        // and analyzer config options -- never the Compilation or
        // AnalyzerConfigOptionsProvider themselves -- so an edit that leaves both the
        // assembly name and the breadcrumb MSBuild property unchanged reuses the
        // cached emission context instead of forcing every model to recombine with a
        // brand-new Compilation snapshot.
        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? "Generated")
            .WithTrackingName(GeneratedConstructorTrackingNames.AssemblyName);

        var breadcrumbLevel = context.AnalyzerConfigOptionsProvider
            .Select(static (configOptions, _) => TypeRegistryGenerator.GetBreadcrumbLevel(configOptions))
            .WithTrackingName(GeneratedConstructorTrackingNames.BreadcrumbLevel);

        var emitContext = assemblyName.Combine(breadcrumbLevel)
            .Select(static (pair, _) => new GeneratedConstructorEmitContext(pair.Left, pair.Right))
            .WithTrackingName(GeneratedConstructorTrackingNames.EmitContext);

        var modelsWithContext = models.Combine(emitContext)
            .WithTrackingName(GeneratedConstructorTrackingNames.Output);

        context.RegisterSourceOutput(modelsWithContext, static (spc, source) =>
        {
            var (model, emitContext) = source;
            var breadcrumbs = new BreadcrumbWriter(emitContext.BreadcrumbLevel);
            var generatedSource = GeneratedConstructorCodeGenerator.GenerateConstructorSource(model, emitContext.AssemblyName, breadcrumbs);
            spc.AddSource(GeneratedConstructorCodeGenerator.BuildHintName(model), SourceText.From(generatedSource, Encoding.UTF8));
        });
    }
}
