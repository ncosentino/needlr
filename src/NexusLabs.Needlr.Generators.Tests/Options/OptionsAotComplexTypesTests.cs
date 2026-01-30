using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for AOT-compatible complex type binding:
/// - Nested objects
/// - Arrays
/// - Lists
/// - Dictionaries
/// 
/// Goal: Achieve feature parity with non-AOT source generation.
/// </summary>
public sealed class OptionsAotComplexTypesTests
{
    // =============================================================
    // 3.1 Nested Objects
    // =============================================================

    [Fact]
    public void Generator_AotNestedObject_GeneratesSubsectionBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public string Name { get; set; } = "";
                    public DatabaseConfig Database { get; set; } = new();
                }
                
                public class DatabaseConfig
                {
                    public string Host { get; set; } = "";
                    public int Port { get; set; } = 5432;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should bind nested object from subsection
        Assert.Contains("GetSection(\"Database\")", generated);
        Assert.Contains("options.Database.Host", generated);
        Assert.Contains("options.Database.Port", generated);
    }

    [Fact]
    public void Generator_AotDeeplyNestedObject_GeneratesRecursiveBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("Root")]
                public class RootOptions
                {
                    public MiddleConfig Middle { get; set; } = new();
                }
                
                public class MiddleConfig
                {
                    public LeafConfig Leaf { get; set; } = new();
                }
                
                public class LeafConfig
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should handle deeply nested structures
        Assert.Contains("GetSection(\"Middle\")", generated);
        Assert.Contains("GetSection(\"Leaf\")", generated);
        Assert.Contains("options.Middle.Leaf.Value", generated);
    }

    [Fact]
    public void Generator_AotNestedObjectWithNullCheck_InitializesIfNull()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public NestedConfig? Nested { get; set; }
                }
                
                public class NestedConfig
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should handle nullable nested objects by initializing if null
        Assert.Contains("options.Nested ??= new", generated);
    }

    // =============================================================
    // 3.2 Arrays
    // =============================================================

    [Fact]
    public void Generator_AotStringArray_GeneratesArrayBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public string[] Tags { get; set; } = System.Array.Empty<string>();
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should bind array from indexed config values
        Assert.Contains("GetSection(\"Tags\")", generated);
        Assert.Contains("GetChildren()", generated);
        Assert.Contains("options.Tags", generated);
    }

    [Fact]
    public void Generator_AotIntArray_GeneratesArrayBindingWithParsing()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public int[] Numbers { get; set; } = System.Array.Empty<int>();
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should parse integers from config
        Assert.Contains("int.TryParse", generated);
        Assert.Contains("options.Numbers", generated);
    }

    [Fact]
    public void Generator_AotComplexObjectArray_GeneratesObjectArrayBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public ServerConfig[] Servers { get; set; } = System.Array.Empty<ServerConfig>();
                }
                
                public class ServerConfig
                {
                    public string Host { get; set; } = "";
                    public int Port { get; set; } = 80;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should create objects and bind their properties
        // Type name may be fully qualified (global::TestApp.ServerConfig)
        Assert.Contains("new global::TestApp.ServerConfig", generated);
        Assert.Contains(".Host", generated);
        Assert.Contains(".Port", generated);
    }

    // =============================================================
    // 3.3 Lists
    // =============================================================

    [Fact]
    public void Generator_AotStringList_GeneratesListBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public List<string> Tags { get; set; } = new();
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should bind list from config
        Assert.Contains("GetSection(\"Tags\")", generated);
        Assert.Contains("options.Tags", generated);
        Assert.Contains("Add(", generated);
    }

    [Fact]
    public void Generator_AotIListInterface_GeneratesListBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public IList<int> Numbers { get; set; } = new List<int>();
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should handle IList<T> interface
        Assert.Contains("options.Numbers", generated);
    }

    [Fact]
    public void Generator_AotComplexObjectList_GeneratesObjectListBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public List<EndpointConfig> Endpoints { get; set; } = new();
                }
                
                public class EndpointConfig
                {
                    public string Url { get; set; } = "";
                    public int Timeout { get; set; } = 30;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should create objects and add to list
        // Type name may be fully qualified (global::TestApp.EndpointConfig)
        Assert.Contains("new global::TestApp.EndpointConfig", generated);
        Assert.Contains("Add(", generated);
    }

    // =============================================================
    // 3.4 Dictionaries
    // =============================================================

    [Fact]
    public void Generator_AotStringDictionary_GeneratesDictionaryBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public Dictionary<string, string> Headers { get; set; } = new();
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should bind dictionary from config section
        Assert.Contains("GetSection(\"Headers\")", generated);
        Assert.Contains("GetChildren()", generated);
        Assert.Contains("options.Headers", generated);
    }

    [Fact]
    public void Generator_AotIntValueDictionary_GeneratesDictionaryWithParsing()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public Dictionary<string, int> Limits { get; set; } = new();
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should parse int values
        Assert.Contains("int.TryParse", generated);
        Assert.Contains("options.Limits", generated);
    }

    [Fact]
    public void Generator_AotComplexValueDictionary_GeneratesObjectDictionary()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public Dictionary<string, ConnectionConfig> Connections { get; set; } = new();
                }
                
                public class ConnectionConfig
                {
                    public string Host { get; set; } = "";
                    public int Port { get; set; } = 0;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should create objects for dictionary values
        // Type name may be fully qualified (global::TestApp.ConnectionConfig)
        Assert.Contains("new global::TestApp.ConnectionConfig", generated);
        Assert.Contains("Key", generated);
    }

    // =============================================================
    // Combined Scenarios
    // =============================================================

    [Fact]
    public void Generator_AotMixedComplexTypes_GeneratesAllBindings()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public string Name { get; set; } = "";
                    public DatabaseConfig Database { get; set; } = new();
                    public List<string> Tags { get; set; } = new();
                    public Dictionary<string, int> Limits { get; set; } = new();
                }
                
                public class DatabaseConfig
                {
                    public string Host { get; set; } = "";
                    public int Port { get; set; } = 5432;
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should handle all property types
        Assert.Contains("options.Name", generated);
        Assert.Contains("options.Database.Host", generated);
        Assert.Contains("options.Tags", generated);
        Assert.Contains("options.Limits", generated);
    }

    [Fact]
    public void Generator_AotNestedObjectWithList_GeneratesCompleteBinding()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry()]
            
            namespace TestApp
            {
                [Options("App")]
                public class AppOptions
                {
                    public ClusterConfig Cluster { get; set; } = new();
                }
                
                public class ClusterConfig
                {
                    public string Name { get; set; } = "";
                    public List<string> Nodes { get; set; } = new();
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should handle nested object containing list
        Assert.Contains("options.Cluster.Name", generated);
        Assert.Contains("options.Cluster.Nodes", generated);
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
