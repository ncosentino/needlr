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

    [Fact]
    public void Generator_WithInitOnlyProperties_GeneratesConfigureCall()
    {
        // Init-only properties are supported by Microsoft's configuration binding (.NET 5+)
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class ImmutableSettings
                {
                    public string ApiKey { get; init; } = "";
                    public int Timeout { get; init; } = 30;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Section name is inferred as "Immutable" (strips "Settings" suffix)
        Assert.Contains("Configure<global::TestApp.ImmutableSettings>", generated);
        Assert.Contains("configuration.GetSection(\"Immutable\")", generated);
    }

    [Fact]
    public void Generator_WithRecordType_GeneratesConfigureCall()
    {
        // Records with init properties are supported (.NET 5+)
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public record DatabaseConfig
                {
                    public string Host { get; init; } = "";
                    public int Port { get; init; } = 5432;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Section name is inferred as "Database" (strips "Config" suffix)
        Assert.Contains("Configure<global::TestApp.DatabaseConfig>", generated);
        Assert.Contains("configuration.GetSection(\"Database\")", generated);
    }

    [Fact]
    public void Generator_WithPositionalRecord_GeneratesConfigureCall()
    {
        // NOTE: While the generator handles positional records, they will FAIL at runtime
        // with reflection-based configuration binding because they lack parameterless constructors.
        // This test verifies the generator doesn't crash - use init-only records instead.
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Redis")]
                public record RedisConfig(string Host, int Port);
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("Configure<global::TestApp.RedisConfig>", generated);
        Assert.Contains("configuration.GetSection(\"Redis\")", generated);
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
