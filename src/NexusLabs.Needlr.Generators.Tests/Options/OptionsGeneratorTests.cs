using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests that verify the source generator correctly handles [Options] attributes
/// and generates the appropriate Configure&lt;T&gt;() calls.
/// </summary>
public sealed class OptionsGeneratorTests
{
    [Fact]
    public void Generator_WithOptionsAttribute_GeneratesConfigureCall()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Database")]
                public class DatabaseOptions
                {
                    public string ConnectionString { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("configuration.GetSection(\"Database\")", generated);
        // Generator uses fully qualified type name
        Assert.Contains("Configure<global::TestApp.DatabaseOptions>", generated);
    }

    [Fact]
    public void Generator_WithOptionsAttribute_InfersSectionNameFromClassName()
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
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should infer "Database" from "DatabaseOptions"
        Assert.Contains("configuration.GetSection(\"Database\")", generated);
    }

    [Fact]
    public void Generator_WithOptionsAttribute_InfersSectionNameFromSettingsSuffix()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class CacheSettings
                {
                    public int ExpirationMinutes { get; set; } = 30;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should infer "Cache" from "CacheSettings"
        Assert.Contains("configuration.GetSection(\"Cache\")", generated);
    }

    [Fact]
    public void Generator_WithOptionsAttribute_InfersSectionNameFromConfigSuffix()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class RedisConfig
                {
                    public string Host { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should infer "Redis" from "RedisConfig"
        Assert.Contains("configuration.GetSection(\"Redis\")", generated);
    }

    [Fact]
    public void Generator_WithOptionsAttribute_KeepsFullNameWhenNoSuffix()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class FeatureFlags
                {
                    public bool EnableNewFeature { get; set; }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should use full name "FeatureFlags"
        Assert.Contains("configuration.GetSection(\"FeatureFlags\")", generated);
    }

    [Fact]
    public void Generator_WithMultipleOptionsClasses_GeneratesMultipleConfigureCalls()
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
                }
                
                [Options]
                public class CacheOptions
                {
                    public int ExpirationMinutes { get; set; } = 30;
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("Configure<global::TestApp.DatabaseOptions>", generated);
        Assert.Contains("configuration.GetSection(\"Database\")", generated);
        Assert.Contains("Configure<global::TestApp.CacheOptions>", generated);
        Assert.Contains("configuration.GetSection(\"Cache\")", generated);
    }

    [Fact]
    public void Generator_AddNeedlr_AcceptsIConfiguration()
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
                }
            }
            """;

        var generated = RunGenerator(source);

        // AddNeedlr should have IConfiguration parameter
        Assert.Contains("IConfiguration configuration", generated);
    }

    private static string RunGenerator(string source)
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

        var generator = new TypeRegistryGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Return all generated sources (like FactoryGeneratorTests)
        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .OrderBy(t => t.FilePath)
            .ToList();

        if (generatedTrees.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n\n", generatedTrees.Select(t => t.GetText().ToString()));
    }
}
