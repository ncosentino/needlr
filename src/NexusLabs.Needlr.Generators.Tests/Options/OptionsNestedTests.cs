using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for nested options detection.
/// Nested options (types used as properties in other [Options] types) should not be
/// registered separately - they are configured as part of their parent.
/// </summary>
public sealed class OptionsNestedTests
{
    #region Nested Detection Tests

    [Fact]
    public void Generator_NestedOptionsType_NotRegisteredSeparately()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class ParentOptions
                {
                    public ChildOptions Child { get; set; } = new();
                }
                
                [Options]
                public class ChildOptions
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Parent is registered
        Assert.Contains("Configure<global::TestApp.ParentOptions>", generated);
        // Child is NOT registered (it's nested inside Parent)
        Assert.DoesNotContain("Configure<global::TestApp.ChildOptions>", generated);
    }

    [Fact]
    public void Generator_DeeplyNestedOptionsType_NotRegisteredSeparately()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class RootOptions
                {
                    public MiddleOptions Middle { get; set; } = new();
                }
                
                [Options]
                public class MiddleOptions
                {
                    public LeafOptions Leaf { get; set; } = new();
                }
                
                [Options]
                public class LeafOptions
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Root is registered
        Assert.Contains("Configure<global::TestApp.RootOptions>", generated);
        // Middle and Leaf are NOT registered (nested)
        Assert.DoesNotContain("Configure<global::TestApp.MiddleOptions>", generated);
        Assert.DoesNotContain("Configure<global::TestApp.LeafOptions>", generated);
    }

    [Fact]
    public void Generator_IndependentOptionsTypes_AllRegistered()
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

        // Both are independent, both should be registered
        Assert.Contains("Configure<global::TestApp.DatabaseOptions>", generated);
        Assert.Contains("Configure<global::TestApp.CacheOptions>", generated);
    }

    [Fact]
    public void Generator_OptionsTypeUsedInMultipleParents_NotRegisteredSeparately()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class Parent1Options
                {
                    public SharedOptions Shared { get; set; } = new();
                }
                
                [Options]
                public class Parent2Options
                {
                    public SharedOptions Shared { get; set; } = new();
                }
                
                [Options]
                public class SharedOptions
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Both parents are registered
        Assert.Contains("Configure<global::TestApp.Parent1Options>", generated);
        Assert.Contains("Configure<global::TestApp.Parent2Options>", generated);
        // Shared is NOT registered (it's nested in both parents)
        Assert.DoesNotContain("Configure<global::TestApp.SharedOptions>", generated);
    }

    [Fact]
    public void Generator_OptionsTypeWithNonOptionsPropertyType_OnlyOptionsTypeRegistered()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class ParentOptions
                {
                    public RegularClass Regular { get; set; } = new();
                }
                
                // No [Options] attribute - just a regular class
                public class RegularClass
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Parent is registered in options
        Assert.Contains("Configure<global::TestApp.ParentOptions>", generated);
        // RegularClass has no [Options] so it's not in the options Configure calls
        // (It may appear elsewhere as an injectable type, but not in RegisterOptions)
        var registerOptionsSection = ExtractRegisterOptionsMethod(generated);
        Assert.DoesNotContain("RegularClass", registerOptionsSection);
    }

    #endregion

    #region Helper Methods

    private static string ExtractRegisterOptionsMethod(string generated)
    {
        // Extract just the RegisterOptions method body
        var startMarker = "public static void RegisterOptions(";
        var startIndex = generated.IndexOf(startMarker);
        if (startIndex < 0) return "";
        
        var endIndex = generated.IndexOf("public static void", startIndex + startMarker.Length);
        if (endIndex < 0) endIndex = generated.Length;
        
        return generated.Substring(startIndex, endIndex - startIndex);
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

        // Return all generated sources
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

    #endregion
}
