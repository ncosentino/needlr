using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for source-generated DataAnnotations validation.
/// 
/// Phase 4 Goal: Generate IValidateOptions implementations that check
/// DataAnnotation attributes without reflection, enabling AOT compatibility.
/// </summary>
public sealed class OptionsDataAnnotationsTests
{
    // =============================================================
    // 4.1 Required Attribute
    // =============================================================

    [Fact]
    public void Generator_RequiredAttribute_GeneratesValidation()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [Required]
                    public string Name { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Should generate a DataAnnotations validator class
        Assert.Contains("SettingsOptionsDataAnnotationsValidator", generated);
        Assert.Contains("IValidateOptions<global::TestApp.SettingsOptions>", generated);
        
        // Should check for null/empty
        Assert.Contains("IsNullOrEmpty", generated);
        Assert.Contains("Name", generated);
    }

    [Fact]
    public void Generator_RequiredAttribute_UsesCustomErrorMessage()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [Required(ErrorMessage = "Name is mandatory")]
                    public string Name { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Should use custom error message
        Assert.Contains("Name is mandatory", generated);
    }

    // =============================================================
    // 4.2 Range Attribute
    // =============================================================

    [Fact]
    public void Generator_RangeAttribute_GeneratesValidation()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [Range(1, 100)]
                    public int Count { get; set; } = 1;
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Should generate range check
        Assert.Contains("SettingsOptionsDataAnnotationsValidator", generated);
        Assert.Contains("Count", generated);
        // Should check min and max bounds
        Assert.Contains("< 1", generated);
        Assert.Contains("> 100", generated);
    }

    [Fact]
    public void Generator_RangeAttribute_SupportsDouble()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [Range(0.5, 99.5)]
                    public double Rate { get; set; } = 1.0;
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Should handle double values
        Assert.Contains("0.5", generated);
        Assert.Contains("99.5", generated);
    }

    // =============================================================
    // 4.3 StringLength Attribute
    // =============================================================

    [Fact]
    public void Generator_StringLengthAttribute_GeneratesValidation()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [StringLength(50, MinimumLength = 3)]
                    public string Code { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Should check both min and max length
        Assert.Contains("SettingsOptionsDataAnnotationsValidator", generated);
        Assert.Contains("Code", generated);
        Assert.Contains("Length", generated);
    }

    // =============================================================
    // 4.4 MinLength / MaxLength Attributes
    // =============================================================

    [Fact]
    public void Generator_MinLengthAttribute_GeneratesValidation()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [MinLength(5)]
                    public string Tag { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        Assert.Contains("SettingsOptionsDataAnnotationsValidator", generated);
        Assert.Contains("Tag", generated);
        Assert.Contains("5", generated);
    }

    [Fact]
    public void Generator_MaxLengthAttribute_GeneratesValidation()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [MaxLength(200)]
                    public string Description { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        Assert.Contains("SettingsOptionsDataAnnotationsValidator", generated);
        Assert.Contains("Description", generated);
        Assert.Contains("200", generated);
    }

    // =============================================================
    // 4.5 RegularExpression Attribute
    // =============================================================

    [Fact]
    public void Generator_RegularExpressionAttribute_GeneratesValidation()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [RegularExpression(@"^[A-Z]{3}$")]
                    public string CountryCode { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        Assert.Contains("SettingsOptionsDataAnnotationsValidator", generated);
        Assert.Contains("Regex", generated);
        Assert.Contains("CountryCode", generated);
    }

    // =============================================================
    // 4.6 Multiple Attributes
    // =============================================================

    [Fact]
    public void Generator_MultipleAttributes_GeneratesAllValidation()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [Required]
                    [StringLength(100, MinimumLength = 1)]
                    public string Name { get; set; } = "";
                    
                    [Range(1, 1000)]
                    public int Limit { get; set; } = 100;
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Should validate both properties
        Assert.Contains("Name", generated);
        Assert.Contains("Limit", generated);
        Assert.Contains("IsNullOrEmpty", generated);
        Assert.Contains("< 1", generated);
    }

    // =============================================================
    // 4.7 Combined with Custom Validator
    // =============================================================

    [Fact]
    public void Generator_DataAnnotationsWithCustomValidator_RegistersBoth()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using System.Collections.Generic;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [Required]
                    public string Name { get; set; } = "";
                    
                    public IEnumerable<string> Validate()
                    {
                        if (Name == "invalid")
                            yield return "Name cannot be 'invalid'";
                    }
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Should have both validators
        Assert.Contains("SettingsOptionsDataAnnotationsValidator", generated);
        Assert.Contains("SettingsOptionsValidator", generated);
        
        // Both should be registered
        Assert.Contains("IValidateOptions<global::TestApp.SettingsOptions>", generated);
    }

    // =============================================================
    // 4.8 Validator Registration
    // =============================================================

    [Fact]
    public void Generator_DataAnnotationsValidator_IsRegistered()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings", ValidateOnStart = true)]
                public class SettingsOptions
                {
                    [Required]
                    public string Name { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Validator should be registered in TypeRegistry
        Assert.Contains("AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<global::TestApp.SettingsOptions>", generated);
        Assert.Contains("SettingsOptionsDataAnnotationsValidator", generated);
    }

    // =============================================================
    // 4.9 No DataAnnotations = No Validator Generated
    // =============================================================

    [Fact]
    public void Generator_NoDataAnnotations_NoValidatorGenerated()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Settings")]
                public class SettingsOptions
                {
                    public string Name { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source, isAot: true);

        // Should NOT generate DataAnnotations validator
        Assert.DoesNotContain("DataAnnotationsValidator", generated);
    }

    // =============================================================
    // Helper Methods
    // =============================================================

    private static string RunGenerator(string source, bool isAot)
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

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var generatedCode = "";
        var runResult = driver.GetRunResult();
        foreach (var result in runResult.Results)
        {
            foreach (var generatedSource in result.GeneratedSources)
            {
                generatedCode += generatedSource.SourceText.ToString() + "\n";
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
