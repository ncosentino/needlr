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
/// Tests for AOT-compatible named options support.
/// </summary>
public sealed class OptionsAotNamedTests
{
    [Fact]
    public void Generator_EmitsNamedOptions_ForOptionsWithName()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Endpoints", Name = "Api")]
                public class EndpointOptions
                {
                    public string BaseUrl { get; set; } = "";
                    public int Timeout { get; set; } = 30;
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should not emit NDLRGEN020 for named options
        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));

        // Should generate AddOptions with the name parameter
        Assert.Contains("AddOptions<global::TestApp.EndpointOptions>(\"Api\")", generatedCode);
        
        // Should still bind the section
        Assert.Contains("GetSection(\"Endpoints\")", generatedCode);
    }

    [Fact]
    public void Generator_EmitsMultipleNamedOptions_ForSameType()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Endpoints:Api", Name = "Api")]
                [Options("Endpoints:Admin", Name = "Admin")]
                public class EndpointOptions
                {
                    public string BaseUrl { get; set; } = "";
                    public int Timeout { get; set; } = 30;
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Only check for options-related diagnostics (NDLRGEN020+)
        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("NDLRGEN02") || d.Id.StartsWith("NDLRGEN03")));

        // Should have both named registrations
        Assert.Contains("AddOptions<global::TestApp.EndpointOptions>(\"Api\")", generatedCode);
        Assert.Contains("AddOptions<global::TestApp.EndpointOptions>(\"Admin\")", generatedCode);
        
        // Should bind different sections
        Assert.Contains("GetSection(\"Endpoints:Api\")", generatedCode);
        Assert.Contains("GetSection(\"Endpoints:Admin\")", generatedCode);
    }

    [Fact]
    public void Generator_EmitsDefaultAndNamedOptions_Together()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Database")]
                [Options("Database:Backup", Name = "Backup")]
                public class DatabaseOptions
                {
                    public string ConnectionString { get; set; } = "";
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Only check for options-related diagnostics (NDLRGEN020+)
        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("NDLRGEN02") || d.Id.StartsWith("NDLRGEN03")));

        // Should have default (no name parameter) and named
        Assert.Contains("AddOptions<global::TestApp.DatabaseOptions>()", generatedCode);
        Assert.Contains("AddOptions<global::TestApp.DatabaseOptions>(\"Backup\")", generatedCode);
    }

    [Fact]
    public void Generator_NamedOptions_MatchesNonAotBehavior()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Cache", Name = "Redis")]
                public class CacheOptions
                {
                    public string Host { get; set; } = "localhost";
                    public int Port { get; set; } = 6379;
                }
            }
            """;

        // Both paths should handle named options
        var (aotCode, aotDiagnostics) = RunGeneratorWithAot(source);
        var (nonAotCode, nonAotDiagnostics) = RunGeneratorWithoutAot(source);

        // Only check for options-related diagnostics (NDLRGEN020+)
        Assert.Empty(aotDiagnostics.Where(d => d.Id.StartsWith("NDLRGEN02") || d.Id.StartsWith("NDLRGEN03")));
        Assert.Empty(nonAotDiagnostics.Where(d => d.Id.StartsWith("NDLRGEN02") || d.Id.StartsWith("NDLRGEN03")));

        // Both should reference the name "Redis"
        Assert.Contains("\"Redis\"", aotCode);
        Assert.Contains("\"Redis\"", nonAotCode);
    }

    [Fact]
    public void Generator_NamedOptions_WithAllPropertyTypes()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Service", Name = "Primary")]
                public class ServiceOptions
                {
                    public string Url { get; set; } = "";
                    public int RetryCount { get; set; } = 3;
                    public bool EnableLogging { get; set; } = true;
                    public double TimeoutSeconds { get; set; } = 30.0;
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Only check for options-related diagnostics (NDLRGEN020+)
        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("NDLRGEN02") || d.Id.StartsWith("NDLRGEN03")));

        // Should bind all properties within the named options
        Assert.Contains("section[\"Url\"]", generatedCode);
        Assert.Contains("section[\"RetryCount\"]", generatedCode);
        Assert.Contains("section[\"EnableLogging\"]", generatedCode);
        Assert.Contains("section[\"TimeoutSeconds\"]", generatedCode);
    }

    private static (string GeneratedCode, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithAot(string source)
    {
        return RunGenerator(source, isAot: true);
    }

    private static (string GeneratedCode, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithoutAot(string source)
    {
        return RunGenerator(source, isAot: false);
    }

    private static (string GeneratedCode, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(
        string source,
        bool isAot)
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

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(isAot);

        var generator = new TypeRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: Array.Empty<AdditionalText>(),
            parseOptions: (CSharpParseOptions)syntaxTree.Options,
            optionsProvider: optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

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

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions _globalOptions;

        public TestAnalyzerConfigOptionsProvider(bool isAot)
        {
            var options = new Dictionary<string, string>
            {
                ["build_property.NeedlrBreadcrumbLevel"] = "Minimal"
            };

            if (isAot)
            {
                options["build_property.PublishAot"] = "true";
            }

            _globalOptions = new TestAnalyzerConfigOptions(options);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _options;

        public TestAnalyzerConfigOptions(Dictionary<string, string> options)
        {
            _options = options;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return _options.TryGetValue(key, out value!);
        }
    }
}
