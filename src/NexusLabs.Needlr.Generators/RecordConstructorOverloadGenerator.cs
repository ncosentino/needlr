using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using NexusLabs.Needlr.Generators.CodeGen;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Incremental source generator that emits one additional public forwarding
/// constructor for each eligible positional record.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class RecordConstructorOverloadGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateModels = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    RecordConstructorOverloadDiscoveryHelper
                        .IsCandidateRecordDeclaration(node),
                transform: static (generatorContext, _) =>
                    RecordConstructorOverloadDiscoveryHelper
                        .TryCreateCanonicalModel(generatorContext))
            .WithTrackingName(
                RecordConstructorOverloadTrackingNames.Candidates);

        var models = candidateModels
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .WithTrackingName(RecordConstructorOverloadTrackingNames.Models);

        var assemblyName = context.CompilationProvider
            .Select(static (compilation, _) =>
                compilation.AssemblyName ?? "Generated")
            .WithTrackingName(
                RecordConstructorOverloadTrackingNames.AssemblyName);

        var breadcrumbLevel = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
                TypeRegistryGenerator.GetBreadcrumbLevel(options))
            .WithTrackingName(
                RecordConstructorOverloadTrackingNames.BreadcrumbLevel);

        var emitContext = assemblyName
            .Combine(breadcrumbLevel)
            .Select(static (pair, _) =>
                new RecordConstructorOverloadEmitContext(
                    pair.Left,
                    pair.Right))
            .WithTrackingName(
                RecordConstructorOverloadTrackingNames.EmitContext);

        var output = models
            .Combine(emitContext)
            .WithTrackingName(
                RecordConstructorOverloadTrackingNames.Output);

        context.RegisterSourceOutput(output, static (sourceContext, source) =>
        {
            var (model, modelContext) = source;
            var breadcrumbs =
                new BreadcrumbWriter(modelContext.BreadcrumbLevel);
            var generatedSource =
                RecordConstructorOverloadCodeGenerator.GenerateSource(
                    model,
                    modelContext.AssemblyName,
                    breadcrumbs);
            sourceContext.AddSource(
                RecordConstructorOverloadCodeGenerator.BuildHintName(model),
                SourceText.From(generatedSource, Encoding.UTF8));
        });
    }
}
