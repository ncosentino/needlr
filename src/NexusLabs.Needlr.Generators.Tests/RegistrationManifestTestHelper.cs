using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

internal static class RegistrationManifestTestHelper
{
    internal const string AssemblyMetadataKey =
        CodeGen.RegistrationManifestCodeGenerator.AssemblyMetadataKey;

    internal static GeneratorTestRunner WithRegistrationManifest(
        this GeneratorTestRunner runner)
    {
        return runner.WithAnalyzerConfigOption(
            "build_property.NeedlrEmitRegistrationManifest",
            "true");
    }

    internal static JsonDocument ReadManifest(IAssemblySymbol assembly)
    {
        var manifestAttributes = assembly.GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass?.ToDisplayString() ==
                    "System.Reflection.AssemblyMetadataAttribute" &&
                attribute.ConstructorArguments.Length == 2 &&
                attribute.ConstructorArguments[0].Value is string key &&
                key == AssemblyMetadataKey)
            .ToArray();

        if (manifestAttributes.Length != 1 ||
            manifestAttributes[0].ConstructorArguments[1].Value is not string json)
        {
            throw new InvalidOperationException(
                $"Expected exactly one '{AssemblyMetadataKey}' assembly metadata value.");
        }

        return JsonDocument.Parse(json);
    }

    internal static IAssemblySymbol EmitAndReference(Compilation producerCompilation)
    {
        using var stream = new MemoryStream();
        var emitResult = producerCompilation.Emit(
            stream,
            cancellationToken: TestContext.Current.CancellationToken);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                "Producer compilation failed: " +
                string.Join("; ", emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        var producerReference = MetadataReference.CreateFromImage(stream.ToArray());
        var consumerCompilation = CSharpCompilation.Create(
            "Consumer",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(
                    "namespace Consumer; public sealed class Marker;",
                    cancellationToken: TestContext.Current.CancellationToken),
            ],
            references: Basic.Reference.Assemblies.Net100.References.All
                .Append(producerReference),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary));

        return (IAssemblySymbol?)consumerCompilation.GetAssemblyOrModuleSymbol(
            producerReference) ??
            throw new InvalidOperationException(
                "The emitted producer assembly was not available to the consumer compilation.");
    }
}
