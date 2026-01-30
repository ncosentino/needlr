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
/// Tests for AOT-compatible validation support in options generation.
/// Phase 2 of AOT options parity.
/// </summary>
public sealed class OptionsAotValidationTests
{
    /// <summary>
    /// Verifies that ValidateOnStart generates the validation chain in AOT mode.
    /// NOTE: ValidateDataAnnotations() is NOT included in AOT mode because it uses reflection.
    /// Only ValidateOnStart() is emitted. Custom validators still work.
    /// </summary>
    [Fact]
    public void Generator_ValidateOnStart_GeneratesValidationChain_InAotMode()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class ValidatedOptions
                {
                    [Required]
                    public string Name { get; set; } = "";
                    
                    [Range(1, 100)]
                    public int Count { get; set; } = 1;
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should not emit any NDLRGEN diagnostics (NDLRGEN022 will be added in future)
        Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("NDLRGEN02") || d.Id.StartsWith("NDLRGEN03")));

        // ValidateDataAnnotations() is NOT emitted in AOT (uses reflection)
        Assert.DoesNotContain(".ValidateDataAnnotations()", generatedCode);
        
        // ValidateOnStart() IS emitted
        Assert.Contains(".ValidateOnStart()", generatedCode);
    }

    /// <summary>
    /// Verifies that custom validation methods (Validate() on the options class)
    /// generate a validator registration in AOT mode.
    /// </summary>
    [Fact]
    public void Generator_CustomValidator_GeneratesValidatorRegistration_InAotMode()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class OptionsWithValidator
                {
                    public string Name { get; set; } = "";
                    public int Value { get; set; }
                    
                    public IEnumerable<string> Validate()
                    {
                        if (string.IsNullOrEmpty(Name))
                            yield return "Name is required";
                        if (Value < 0)
                            yield return "Value must be non-negative";
                    }
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should register the validator
        Assert.Contains("IValidateOptions<global::TestApp.OptionsWithValidator>", generatedCode);
        Assert.Contains("OptionsWithValidatorValidator", generatedCode);
    }

    /// <summary>
    /// Verifies that external validators are properly registered in AOT mode.
    /// </summary>
    [Fact]
    public void Generator_ExternalValidator_GeneratesRegistration_InAotMode()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public class MyValidator
                {
                    public IEnumerable<string> Validate(ExternallyValidatedOptions options)
                    {
                        if (options.Value < 0)
                            yield return "Value must be non-negative";
                        yield break;
                    }
                }

                [Options(ValidateOnStart = true, Validator = typeof(MyValidator))]
                public class ExternallyValidatedOptions
                {
                    public int Value { get; set; }
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should register the external validator type
        Assert.Contains("AddSingleton<global::TestApp.MyValidator>", generatedCode);
        
        // Should register the wrapper validator
        Assert.Contains("IValidateOptions<global::TestApp.ExternallyValidatedOptions>", generatedCode);
    }

    /// <summary>
    /// Verifies that static external validators don't need DI registration.
    /// </summary>
    [Fact]
    public void Generator_StaticExternalValidator_NoSingletonRegistration_InAotMode()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public static class StaticValidator
                {
                    public static IEnumerable<string> Validate(StaticValidatedOptions options)
                    {
                        if (options.Value < 0)
                            yield return "Value must be non-negative";
                        yield break;
                    }
                }

                [Options(ValidateOnStart = true, Validator = typeof(StaticValidator))]
                public class StaticValidatedOptions
                {
                    public int Value { get; set; }
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should NOT register the static validator as singleton
        Assert.DoesNotContain("AddSingleton<global::TestApp.StaticValidator>", generatedCode);
        
        // Should still register the wrapper validator
        Assert.Contains("IValidateOptions<global::TestApp.StaticValidatedOptions>", generatedCode);
    }

    /// <summary>
    /// Verifies that named options with validation work correctly in AOT mode.
    /// </summary>
    [Fact]
    public void Generator_NamedOptionsWithValidation_GeneratesCorrectChain_InAotMode()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Api", Name = "Primary", ValidateOnStart = true)]
                public class ApiOptions
                {
                    [Required]
                    public string Endpoint { get; set; } = "";
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should have named options with validation chain
        Assert.Contains("AddOptions<global::TestApp.ApiOptions>(\"Primary\")", generatedCode);
        // ValidateDataAnnotations NOT emitted in AOT (uses reflection)
        Assert.DoesNotContain(".ValidateDataAnnotations()", generatedCode);
        Assert.Contains(".ValidateOnStart()", generatedCode);
    }

    /// <summary>
    /// Documents the behavioral difference between AOT and non-AOT for DataAnnotations.
    /// Non-AOT includes ValidateDataAnnotations(), AOT does not (uses reflection).
    /// This is a known limitation.
    /// </summary>
    [Fact]
    public void Generator_DataAnnotations_DiffersBetweenAotAndNonAot_KnownLimitation()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class ParityOptions
                {
                    [Required]
                    public string Name { get; set; } = "";
                }
            }
            """;

        var (aotCode, _) = RunGeneratorWithAot(source);
        var (nonAotCode, _) = RunGeneratorWithoutAot(source);

        // Non-AOT includes ValidateDataAnnotations
        Assert.Contains(".ValidateDataAnnotations()", nonAotCode);
        
        // AOT does NOT include ValidateDataAnnotations (uses reflection)
        Assert.DoesNotContain(".ValidateDataAnnotations()", aotCode);
        
        // Both should have ValidateOnStart
        Assert.Contains(".ValidateOnStart()", aotCode);
        Assert.Contains(".ValidateOnStart()", nonAotCode);
    }

    /// <summary>
    /// Verifies that multiple validated options all get their validation chains.
    /// </summary>
    [Fact]
    public void Generator_MultipleValidatedOptions_AllHaveValidation_InAotMode()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("Database", ValidateOnStart = true)]
                public class DatabaseOptions
                {
                    [Required]
                    public string ConnectionString { get; set; } = "";
                }

                [Options("Cache", ValidateOnStart = true)]
                public class CacheOptions
                {
                    [Range(1, 3600)]
                    public int ExpirationSeconds { get; set; } = 60;
                }

                [Options("Logging")]
                public class LoggingOptions
                {
                    public string Level { get; set; } = "Info";
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Count ValidateOnStart calls - should be 2 (Database and Cache, not Logging)
        var validateOnStartCount = generatedCode.Split(".ValidateOnStart()").Length - 1;
        Assert.Equal(2, validateOnStartCount);
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
