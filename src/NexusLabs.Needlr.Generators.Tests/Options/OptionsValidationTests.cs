// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for options validation support.
/// </summary>
public sealed class OptionsValidationTests
{
    #region ValidateOnStart with DataAnnotations

    [Fact]
    public void Generator_ValidateOnStart_GeneratesAddOptionsPattern()
    {
        // When ValidateOnStart = true, should use AddOptions pattern instead of Configure
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class StripeOptions
                {
                    public string ApiKey { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should use AddOptions pattern for validation chain
        Assert.Contains("AddOptions<global::TestApp.StripeOptions>", generated);
        Assert.Contains("BindConfiguration(", generated);
        Assert.Contains("ValidateOnStart()", generated);
    }

    [Fact]
    public void Generator_ValidateOnStart_IncludesDataAnnotationsValidation()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class StripeOptions
                {
                    [Required]
                    public string ApiKey { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("ValidateDataAnnotations()", generated);
    }

    [Fact]
    public void Generator_ValidateOnStartFalse_UsesConfigurePattern()
    {
        // Default behavior (no validation) should use simple Configure pattern
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

        Assert.Contains("Configure<global::TestApp.DatabaseOptions>", generated);
        Assert.DoesNotContain("AddOptions<", generated);
        Assert.DoesNotContain("ValidateOnStart", generated);
    }

    #endregion

    #region OptionsValidator Method

    [Fact]
    public void Generator_OptionsValidatorMethod_GeneratesValidatorClass()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class StripeOptions
                {
                    public string ApiKey { get; set; } = "";
                    
                    [OptionsValidator]
                    public IEnumerable<string> Validate()
                    {
                        if (!ApiKey.StartsWith("sk_"))
                            yield return "ApiKey must start with 'sk_'";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should generate a validator class implementing IValidateOptions
        Assert.Contains("IValidateOptions<global::TestApp.StripeOptions>", generated);
        Assert.Contains("StripeOptionsValidator", generated);
    }

    [Fact]
    public void Generator_OptionsValidatorMethod_RegistersValidator()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class ApiOptions
                {
                    public string Key { get; set; } = "";
                    
                    [OptionsValidator]
                    public IEnumerable<string> Validate()
                    {
                        if (string.IsNullOrEmpty(Key))
                            yield return "Key is required";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should register the generated validator
        Assert.Contains("AddSingleton<global::Microsoft.Extensions.Options.IValidateOptions<global::TestApp.ApiOptions>", generated);
    }

    [Fact]
    public void Generator_StaticValidatorMethod_Works()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class ConfigOptions
                {
                    public int MaxItems { get; set; }
                    
                    [OptionsValidator]
                    public static IEnumerable<string> ValidateConfig(ConfigOptions options)
                    {
                        if (options.MaxItems < 0)
                            yield return "MaxItems cannot be negative";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("IValidateOptions<global::TestApp.ConfigOptions>", generated);
    }

    #endregion

    #region IOptionsValidator<T> Type Discovery

    [Fact]
    public void Generator_SeparateValidatorType_IsDiscoveredAndRegistered()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public interface IOptionsValidator<T>
                {
                    IEnumerable<string> Validate(T options);
                }
                
                [Options(ValidateOnStart = true)]
                public class PaymentOptions
                {
                    public string MerchantId { get; set; } = "";
                }
                
                public class PaymentOptionsValidator : IOptionsValidator<PaymentOptions>
                {
                    public IEnumerable<string> Validate(PaymentOptions options)
                    {
                        if (string.IsNullOrEmpty(options.MerchantId))
                            yield return "MerchantId is required";
                    }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should discover and wire up the validator type
        Assert.Contains("PaymentOptionsValidator", generated);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Generator_ValidateOnStartWithoutValidation_StillValidatesOnStart()
    {
        // ValidateOnStart without any validation attributes should still call ValidateOnStart
        // (even if no actual validation occurs, the pattern is established)
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class SimpleOptions
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("ValidateOnStart()", generated);
    }

    [Fact]
    public void Generator_MultipleOptionsWithValidation_AllValidated()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class OptionsA
                {
                    public string Value { get; set; } = "";
                }
                
                [Options(ValidateOnStart = true)]
                public class OptionsB
                {
                    public int Count { get; set; }
                }
                
                [Options]
                public class OptionsC
                {
                    public bool Flag { get; set; }
                }
            }
            """;

        var generated = RunGenerator(source);

        // A and B should have ValidateOnStart, C should use Configure
        Assert.Contains("AddOptions<global::TestApp.OptionsA>", generated);
        Assert.Contains("AddOptions<global::TestApp.OptionsB>", generated);
        Assert.Contains("Configure<global::TestApp.OptionsC>", generated);
    }

    #endregion

    #region Helper Methods

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
