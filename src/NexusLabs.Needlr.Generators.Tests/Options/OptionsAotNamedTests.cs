// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using Xunit;

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
        var runner = GeneratorTestRunner.ForOptions()
            .WithSource(source)
            .WithAotMode()
            .WithBreadcrumbLevel("Minimal");

        var generatedCode = runner.GetTypeRegistryOutput();
        var diagnostics = runner.RunTypeRegistryGeneratorDiagnostics();
        return (generatedCode, diagnostics.ToImmutableArray());
    }

    private static (string GeneratedCode, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithoutAot(string source)
    {
        var runner = GeneratorTestRunner.ForOptions()
            .WithSource(source)
            .WithBreadcrumbLevel("Minimal");

        var generatedCode = runner.GetTypeRegistryOutput();
        var diagnostics = runner.RunTypeRegistryGeneratorDiagnostics();
        return (generatedCode, diagnostics.ToImmutableArray());
    }
}
