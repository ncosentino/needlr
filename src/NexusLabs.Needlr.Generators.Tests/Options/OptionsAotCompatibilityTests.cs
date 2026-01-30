// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

#pragma warning disable xUnit1051 // Calls to methods which accept CancellationToken

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for NDLRGEN020: Options attribute is not compatible with AOT (for unsupported types).
/// With AOT-compatible code generation, primitive types are now supported in AOT projects.
/// </summary>
public sealed class OptionsAotCompatibilityTests
{
    [Fact]
    public void Generator_NoDiagnostic_WhenOptionsWithPrimitivesUsedInAotProject()
    {
        // Options with primitive types should work in AOT projects (we generate direct binding code)
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class AppSettings
                {
                    public string ApiKey { get; set; } = "";
                    public int Timeout { get; set; }
                    public bool EnableFeature { get; set; }
                    public double RetryDelay { get; set; }
                }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, publishAot: true);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);
    }

    [Fact]
    public void Generator_NoDiagnostic_WhenOptionsWithPrimitivesUsedInIsAotCompatibleProject()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class DatabaseOptions
                {
                    public string ConnectionString { get; set; } = "";
                    public int CommandTimeout { get; set; }
                }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, isAotCompatible: true);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);
    }

    [Fact]
    public void Generator_SkipsUnsupportedPropertyTypes_Silently_MatchingNonAotBehavior()
    {
        // Options with nested objects or collections are silently skipped in AOT
        // This matches ConfigurationBinder behavior in non-AOT
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public class NestedSettings
                {
                    public string Value { get; set; } = "";
                }

                [Options]
                public class AppSettings
                {
                    public string Name { get; set; } = "";
                    public NestedSettings Nested { get; set; } = new();
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAotEnabled_GetOutput(source, publishAot: true);

        // No NDLRGEN020 - we achieve parity by silently skipping unsupported types
        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);

        // Supported property should still be bound
        Assert.Contains("section[\"Name\"]", generatedCode);
        // Unsupported property should be skipped with a comment
        Assert.Contains("Skipped:", generatedCode);
    }

    [Fact]
    public void Generator_NoDiagnostic_WhenOptionsUsedInNonAotProject()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class CacheSettings
                {
                    public int CacheTimeoutSeconds { get; set; } = 300;
                }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, publishAot: false, isAotCompatible: false);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);
    }

    [Fact]
    public void Generator_NoDiagnostic_WhenNoOptionsInAotProject()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public interface IMyService { }
                public class MyService : IMyService { }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, publishAot: true);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);
    }

    [Fact]
    public void Generator_SkipsCollectionProperties_Silently_MatchingNonAotBehavior()
    {
        // Collections are silently skipped in AOT, matching non-AOT behavior
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class SupportedOptions
                {
                    public string Value { get; set; } = "";
                    public int Number { get; set; }
                }

                [Options]
                public class OptionsWithCollection
                {
                    public string Name { get; set; } = "";
                    public List<string> Items { get; set; } = new();
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAotEnabled_GetOutput(source, publishAot: true);

        // No NDLRGEN020 for either - parity with non-AOT
        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);

        // Supported properties should be bound
        Assert.Contains("section[\"Value\"]", generatedCode);
        Assert.Contains("section[\"Name\"]", generatedCode);
        // Collection should be skipped
        Assert.Contains("Skipped:", generatedCode);
    }

    [Fact]
    public void Generator_NoDiagnostic_ForNullablePrimitives()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class NullableOptions
                {
                    public string? NullableString { get; set; }
                    public int? NullableInt { get; set; }
                    public bool? NullableBool { get; set; }
                }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, publishAot: true);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);
    }

    [Fact]
    public void Generator_GeneratesAotBindingCode_ForPrimitiveTypes()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class DatabaseOptions
                {
                    public string ConnectionString { get; set; } = "";
                    public int CommandTimeout { get; set; }
                    public bool EnableRetry { get; set; }
                    public double RetryDelay { get; set; }
                }
            }
            """;

        var (generatedCode, _) = RunGeneratorWithAotEnabled_GetOutput(source, publishAot: true);

        // Should use AddOptions pattern with direct binding
        Assert.Contains("AddOptions<global::TestApp.DatabaseOptions>()", generatedCode);
        Assert.Contains(".Configure<IConfiguration>((options, config) =>", generatedCode);
        Assert.Contains("section[\"ConnectionString\"]", generatedCode);
        Assert.Contains("int.TryParse", generatedCode);
        Assert.Contains("bool.TryParse", generatedCode);
        Assert.Contains("double.TryParse", generatedCode);
    }

    [Fact]
    public void Generator_UsesReflectionBinding_ForNonAotProject()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class CacheSettings
                {
                    public int CacheTimeoutSeconds { get; set; } = 300;
                }
            }
            """;

        var (generatedCode, _) = RunGeneratorWithAotEnabled_GetOutput(source, publishAot: false, isAotCompatible: false);

        // Should use OptionsConfigurationServiceCollectionExtensions.Configure (reflection-based)
        Assert.Contains("OptionsConfigurationServiceCollectionExtensions.Configure<global::TestApp.CacheSettings>", generatedCode);
        // Should NOT use AddOptions pattern with direct binding
        Assert.DoesNotContain(".Configure<IConfiguration>((options, config) =>", generatedCode);
    }

    private static ImmutableArray<Diagnostic> RunGeneratorWithAotEnabled(
        string source,
        bool publishAot = false,
        bool isAotCompatible = false)
    {
        var (_, diagnostics) = RunGeneratorWithAotEnabled_GetOutput(source, publishAot, isAotCompatible);
        return diagnostics;
    }

    private static (string GeneratedCode, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithAotEnabled_GetOutput(
        string source,
        bool publishAot = false,
        bool isAotCompatible = false)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(GenerateTypeRegistryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(OptionsAttribute).Assembly.Location),
            })
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var optionsProvider = new AotTestAnalyzerConfigOptionsProvider(publishAot, isAotCompatible);

        var generator = new TypeRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: Array.Empty<AdditionalText>(),
            parseOptions: (CSharpParseOptions)syntaxTree.Options,
            optionsProvider: optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Find the TypeRegistry.g.cs output
        var generatedCode = "";
        var runResult = driver.GetRunResult();
        foreach (var result in runResult.Results)
        {
            foreach (var source2 in result.GeneratedSources)
            {
                if (source2.HintName == "TypeRegistry.g.cs")
                {
                    generatedCode = source2.SourceText.ToString();
                }
            }
        }

        return (generatedCode, diagnostics);
    }

    private sealed class AotTestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AotTestAnalyzerConfigOptions _globalOptions;

        public AotTestAnalyzerConfigOptionsProvider(bool publishAot, bool isAotCompatible)
        {
            var options = new Dictionary<string, string>
            {
                ["build_property.NeedlrBreadcrumbLevel"] = "Minimal"
            };

            if (publishAot)
            {
                options["build_property.PublishAot"] = "true";
            }

            if (isAotCompatible)
            {
                options["build_property.IsAotCompatible"] = "true";
            }

            _globalOptions = new AotTestAnalyzerConfigOptions(options);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
    }

    private sealed class AotTestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _options;

        public AotTestAnalyzerConfigOptions(Dictionary<string, string> options)
        {
            _options = options;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return _options.TryGetValue(key, out value!);
        }
    }
}
