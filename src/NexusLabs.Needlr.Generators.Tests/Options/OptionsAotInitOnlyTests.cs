using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for AOT-compatible init-only and positional record support.
/// 
/// Phase 5 Goal: Bind init-only properties and positional records in AOT
/// by using object initializer syntax in a factory pattern.
/// </summary>
public sealed class OptionsAotInitOnlyTests
{
    // =============================================================
    // 5.1 Init-Only Properties
    // =============================================================

    [Fact]
    public void Generator_AotInitOnlyProperty_UsesObjectInitializer()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings")]
                public class SettingsOptions
                {
                    public string Name { get; init; } = "";
                    public int Count { get; init; } = 0;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should use Options.Create with object initializer, not Configure delegate
        Assert.Contains("Options.Create(new global::TestApp.SettingsOptions", generated);
        Assert.Contains("Name =", generated);
        Assert.Contains("Count =", generated);
        // Should NOT have "Skipped" comments for init-only
        Assert.DoesNotContain("Skipped: Name", generated);
        Assert.DoesNotContain("Skipped: Count", generated);
    }

    [Fact]
    public void Generator_AotMixedSettersAndInitOnly_BindsBoth()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Mixed")]
                public class MixedOptions
                {
                    public string InitProp { get; init; } = "";
                    public int SetProp { get; set; } = 0;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Both properties should be bound
        Assert.Contains("InitProp =", generated);
        Assert.Contains("SetProp", generated);
        Assert.DoesNotContain("Skipped", generated);
    }

    [Fact]
    public void Generator_AotInitOnlyWithValidation_WorksWithFactory()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Validated", ValidateOnStart = true)]
                public class ValidatedInitOptions
                {
                    public string Required { get; init; } = "";
                    public int MaxValue { get; init; } = 100;
                    
                    public System.Collections.Generic.IEnumerable<string> Validate()
                    {
                        if (string.IsNullOrEmpty(Required))
                            yield return "Required is required";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should still work with validation
        Assert.Contains("Options.Create(new global::TestApp.ValidatedInitOptions", generated);
        Assert.Contains("ValidateOnStart()", generated);
    }

    // =============================================================
    // 5.2 Positional Records
    // =============================================================

    [Fact]
    public void Generator_AotPositionalRecord_UsesConstructor()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Security")]
                public partial record SecurityOptions(string SecretKey, bool RequireHttps, int TokenExpiry);
            }
            """;

        var generated = RunGenerator(source);

        // Should use Options.Create with constructor call
        Assert.Contains("Options.Create(new global::TestApp.SecurityOptions(", generated);
        // Should NOT have "Skipped" comments
        Assert.DoesNotContain("Skipped: SecretKey", generated);
        Assert.DoesNotContain("Skipped: RequireHttps", generated);
        Assert.DoesNotContain("Skipped: TokenExpiry", generated);
    }

    [Fact]
    public void Generator_AotPositionalRecord_ParsesConstructorArgs()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Connection")]
                public partial record ConnectionOptions(string Host, int Port, bool UseSsl);
            }
            """;

        var generated = RunGenerator(source);

        // Should have proper parsing for each constructor parameter type
        Assert.Contains("section[\"Host\"]", generated);
        Assert.Contains("int.TryParse", generated);
        Assert.Contains("bool.TryParse", generated);
    }

    [Fact]
    public void Generator_AotPositionalRecordWithEnum_ParsesEnumArg()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum LogLevel { Debug, Info, Warning, Error }
                
                [Options("Log")]
                public partial record LogOptions(string Path, LogLevel Level);
            }
            """;

        var generated = RunGenerator(source);

        // Should parse enum using Enum.TryParse
        Assert.Contains("Enum.TryParse", generated);
    }

    // =============================================================
    // 5.3 Records with Init Properties (non-positional)
    // =============================================================

    [Fact]
    public void Generator_AotRecordWithInitProperties_UsesObjectInitializer()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Cache")]
                public record CacheOptions
                {
                    public string Provider { get; init; } = "";
                    public int Ttl { get; init; } = 60;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should use Options.Create with object initializer
        Assert.Contains("Options.Create(new global::TestApp.CacheOptions", generated);
        Assert.Contains("Provider =", generated);
        Assert.Contains("Ttl =", generated);
    }

    // =============================================================
    // Combined Scenarios
    // =============================================================

    [Fact]
    public void Generator_AotMultipleOptionsTypes_EachUsesAppropriatePattern()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                // Regular class with setters - uses Configure pattern
                [Options("Database")]
                public class DatabaseOptions
                {
                    public string Host { get; set; } = "";
                }
                
                // Class with init-only - uses factory pattern
                [Options("Cache")]
                public class CacheOptions
                {
                    public string Provider { get; init; } = "";
                }
                
                // Positional record - uses constructor pattern
                [Options("Auth")]
                public partial record AuthOptions(string Key, int Expiry);
            }
            """;

        var generated = RunGenerator(source);

        // Each should use appropriate binding pattern
        // DatabaseOptions: Configure delegate via AddOptions<T>().Configure<IConfiguration>
        Assert.Contains("AddOptions<global::TestApp.DatabaseOptions>()", generated);
        Assert.Contains(".Configure<IConfiguration>", generated);
        // CacheOptions: Options.Create with initializer
        Assert.Contains("Options.Create(new global::TestApp.CacheOptions", generated);
        // AuthOptions: Options.Create with constructor
        Assert.Contains("Options.Create(new global::TestApp.AuthOptions(", generated);
    }

    [Fact]
    public void Generator_AotInitOnlyWithComplexTypes_BindsNestedObjects()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Server")]
                public class ServerOptions
                {
                    public string Name { get; init; } = "";
                    public EndpointConfig Endpoint { get; init; } = new();
                    public List<string> Tags { get; init; } = new();
                }
                
                public class EndpointConfig
                {
                    public string Host { get; set; } = "";
                    public int Port { get; set; } = 80;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should handle init-only with complex types
        Assert.Contains("Options.Create(new global::TestApp.ServerOptions", generated);
        Assert.Contains("Name =", generated);
        Assert.Contains("Endpoint =", generated);
        Assert.Contains("Tags =", generated);
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

        // Use AOT mode options provider
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(isAot: true);

        var generator = new TypeRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: Array.Empty<AdditionalText>(),
            parseOptions: (CSharpParseOptions)syntaxTree.Options,
            optionsProvider: optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

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

        return generatedCode;
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
