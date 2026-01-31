// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for named options support (multiple configurations of the same type).
/// </summary>
public sealed class OptionsNamedTests
{
    [Fact]
    public void Generator_NamedOptions_GeneratesConfigureWithName()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Databases:Primary", Name = "Primary")]
                public class DatabaseOptions
                {
                    public string ConnectionString { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should use Configure with name parameter
        Assert.Contains("\"Primary\"", generated);
        Assert.Contains("GetSection(\"Databases:Primary\")", generated);
    }

    [Fact]
    public void Generator_MultipleNamedOptions_GeneratesAllConfigurations()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Databases:Primary", Name = "Primary")]
                [Options("Databases:Replica", Name = "Replica")]
                public class DatabaseOptions
                {
                    public string ConnectionString { get; set; } = "";
                    public bool ReadOnly { get; set; }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should generate both named configurations
        Assert.Contains("\"Primary\"", generated);
        Assert.Contains("GetSection(\"Databases:Primary\")", generated);
        Assert.Contains("\"Replica\"", generated);
        Assert.Contains("GetSection(\"Databases:Replica\")", generated);
    }

    [Fact]
    public void Generator_MixedNamedAndDefault_GeneratesBoth()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]  // Default (unnamed)
                [Options("Databases:Backup", Name = "Backup")]  // Named
                public class DatabaseOptions
                {
                    public string ConnectionString { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should generate default (inferred section name) and named configuration
        Assert.Contains("GetSection(\"Database\")", generated);  // Default inferred from DatabaseOptions
        Assert.Contains("\"Backup\"", generated);
        Assert.Contains("GetSection(\"Databases:Backup\")", generated);
    }    [Fact]
    public void Generator_NamedOptionsWithValidation_UsesAddOptionsPattern()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Connections:Primary", Name = "Primary", ValidateOnStart = true)]
                public class ConnectionOptions
                {
                    public string Host { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should use AddOptions with name for validation chain
        Assert.Contains("AddOptions<global::TestApp.ConnectionOptions>(\"Primary\")", generated);
        Assert.Contains("BindConfiguration(\"Connections:Primary\")", generated);
        Assert.Contains("ValidateOnStart()", generated);
    }    [Fact]
    public void Generator_NameWithoutExplicitSection_InfersSectionName()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(Name = "Custom")]
                public class CacheSettings
                {
                    public int ExpirationMinutes { get; set; }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should infer section name from class name but use explicit Name
        Assert.Contains("\"Custom\"", generated);
        Assert.Contains("GetSection(\"Cache\")", generated);  // Inferred from CacheSettings
    }

    private static string RunGenerator(string source)
    {
        return GeneratorTestRunner.ForOptions()
            .WithSource(source)
            .RunTypeRegistryGenerator();
    }
}
