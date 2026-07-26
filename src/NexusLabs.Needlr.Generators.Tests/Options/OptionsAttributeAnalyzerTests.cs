// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for the OptionsAttributeAnalyzer.
/// </summary>
public sealed class OptionsAttributeAnalyzerTests
{
    [Fact]
    public void Analyzer_ValidatorWithoutValidateOnStart_ReportsWarning()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            namespace TestApp
            {
                [Options(Validator = typeof(TestValidator))]
                public class TestOptions
                {
                    public string Value { get; set; } = "";
                }
                
                public class TestValidator : IOptionsValidator<TestOptions>
                {
                    public IEnumerable<ValidationError> Validate(TestOptions options)
                    {
                        yield break;
                    }
                }
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN018",
            DiagnosticSeverity.Warning,
            "Validator 'TestValidator' will not run because ValidateOnStart is false. Set ValidateOnStart = true to enable validation.",
            source);
    }

    [Fact]
    public void Analyzer_ValidateMethodWithoutValidateOnStart_ReportsWarning()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            namespace TestApp
            {
                [Options(ValidateMethod = "CustomValidate")]
                public class TestOptions
                {
                    public string Value { get; set; } = "";
                    
                    public IEnumerable<ValidationError> CustomValidate()
                    {
                        yield break;
                    }
                }
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN019",
            DiagnosticSeverity.Warning,
            "ValidateMethod 'CustomValidate' will not run because ValidateOnStart is false. Set ValidateOnStart = true to enable validation.",
            source);
    }

    [Fact]
    public void Analyzer_ValidateMethodNotFound_ReportsError()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, ValidateMethod = "NonExistentMethod")]
                public class TestOptions
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN016",
            DiagnosticSeverity.Error,
            "Method 'NonExistentMethod' not found on type 'TestOptions'. Ensure the method exists and has the correct signature.",
            source);
    }

    [Fact]
    public void Analyzer_ExternalValidatorMethodNotFound_ReportsError()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, Validator = typeof(EmptyValidator), ValidateMethod = "NonExistentMethod")]
                public class TestOptions
                {
                    public string Value { get; set; } = "";
                }
                
                public class EmptyValidator
                {
                    // No methods
                }
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN016",
            DiagnosticSeverity.Error,
            "Method 'NonExistentMethod' not found on type 'EmptyValidator'. Ensure the method exists and has the correct signature.",
            source);
    }

    [Fact]
    public void Analyzer_ValidatorTypeMismatch_ReportsError()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, Validator = typeof(WrongTypeValidator))]
                public class TestOptions
                {
                    public string Value { get; set; } = "";
                }
                
                public class OtherOptions
                {
                    public string Other { get; set; } = "";
                }
                
                public class WrongTypeValidator
                {
                    // Validates OtherOptions, not TestOptions
                    public IEnumerable<ValidationError> Validate(OtherOptions options)
                    {
                        yield break;
                    }
                }
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN015",
            DiagnosticSeverity.Error,
            "Validator 'WrongTypeValidator' validates 'OtherOptions' but is applied to options type 'TestOptions'. The validator must be for the same type.",
            source);
    }

    [Fact]
    public void Analyzer_ValidConfiguration_NoDiagnostics()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class TestOptions
                {
                    public string Value { get; set; } = "";
                    
                    public IEnumerable<ValidationError> Validate()
                    {
                        if (string.IsNullOrEmpty(Value))
                            yield return "Value is required";
                    }
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyzer_ValidExternalValidator_NoDiagnostics()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;
            
            namespace TestApp
            {
                [Options(ValidateOnStart = true, Validator = typeof(TestValidator))]
                public class TestOptions
                {
                    public string Value { get; set; } = "";
                }
                
                public class TestValidator
                {
                    public IEnumerable<ValidationError> Validate(TestOptions options)
                    {
                        if (string.IsNullOrEmpty(options.Value))
                            yield return "Value is required";
                    }
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyzer_NoValidateOnStart_NoDiagnostics()
    {
        // When ValidateOnStart is false and nothing is specified, no diagnostics
        var source = """
            using NexusLabs.Needlr.Generators;
            
            namespace TestApp
            {
                [Options]
                public class SimpleOptions
                {
                    public string Value { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyzer_SelfValidationInstanceMethodWithParameter_ReportsWrongSignature()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true)]
            public class TestOptions
            {
                public IEnumerable<ValidationError> Validate(TestOptions options) => [];
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN017",
            DiagnosticSeverity.Error,
            "Method 'Validate' on type 'TestOptions' has wrong signature, expected IEnumerable<ValidationError> Validate()",
            source);
    }

    [Fact]
    public void Analyzer_SelfValidationStaticMethodWithMatchingParameter_NoDiagnostics()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true)]
            public class TestOptions
            {
                public static IEnumerable<ValidationError> Validate(TestOptions options) => [];
            }
            """;

        Assert.Empty(RunAnalyzer(source));
    }

    [Fact]
    public void Analyzer_SelfValidationStaticMethodWithoutParameter_ReportsWrongSignature()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true)]
            public class TestOptions
            {
                public static IEnumerable<ValidationError> Validate() => [];
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN017",
            DiagnosticSeverity.Error,
            "Method 'Validate' on type 'TestOptions' has wrong signature, expected static IEnumerable<ValidationError> Validate(TestOptions options)",
            source);
    }

    [Fact]
    public void Analyzer_SelfValidationStaticMethodWithMultipleParameters_ReportsWrongSignature()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true)]
            public class TestOptions
            {
                public static IEnumerable<ValidationError> Validate(TestOptions options, int value) => [];
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN017",
            DiagnosticSeverity.Error,
            "Method 'Validate' on type 'TestOptions' has wrong signature, expected static IEnumerable<ValidationError> Validate(TestOptions options)",
            source);
    }

    [Fact]
    public void Analyzer_SelfValidationStaticMethodWithWrongParameterType_ReportsWrongSignature()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true)]
            public class TestOptions
            {
                public static IEnumerable<ValidationError> Validate(string options) => [];
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN017",
            DiagnosticSeverity.Error,
            "Method 'Validate' on type 'TestOptions' has wrong signature, expected static IEnumerable<ValidationError> Validate(TestOptions options)",
            source);
    }

    [Fact]
    public void Analyzer_ExternalValidatorWithoutParameter_ReportsWrongSignature()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true, Validator = typeof(TestValidator))]
            public class TestOptions;

            public class TestValidator
            {
                public IEnumerable<ValidationError> Validate() => [];
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN017",
            DiagnosticSeverity.Error,
            "Method 'Validate' on type 'TestValidator' has wrong signature, expected IEnumerable<ValidationError> Validate(TestOptions options)",
            source);
    }

    [Fact]
    public void Analyzer_ExternalValidatorWithMultipleParameters_ReportsWrongSignature()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true, Validator = typeof(TestValidator))]
            public class TestOptions;

            public class TestValidator
            {
                public IEnumerable<ValidationError> Validate(TestOptions options, int value) => [];
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN017",
            DiagnosticSeverity.Error,
            "Method 'Validate' on type 'TestValidator' has wrong signature, expected IEnumerable<ValidationError> Validate(TestOptions options)",
            source);
    }

    [Fact]
    public void Analyzer_ExternalValidatorStaticMethodWithMatchingParameter_NoDiagnostics()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true, Validator = typeof(TestValidator))]
            public class TestOptions;

            public class TestValidator
            {
                public static IEnumerable<ValidationError> Validate(TestOptions options) => [];
            }
            """;

        Assert.Empty(RunAnalyzer(source));
    }

    [Theory]
    [InlineData("void")]
    [InlineData("int")]
    [InlineData("System.Collections.IEnumerable")]
    [InlineData("System.Collections.Generic.IEnumerable<int>")]
    [InlineData("ValidationResults")]
    public void Analyzer_UnsupportedValidationReturnType_ReportsWrongSignature(string returnType)
    {
        var source = $$"""
            using NexusLabs.Needlr.Generators;
            using System.Collections;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true)]
            public class TestOptions
            {
                public {{returnType}} Validate() => throw null!;
            }

            public class ValidationResults : IEnumerable<ValidationError>
            {
                public IEnumerator<ValidationError> GetEnumerator() => throw null!;

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN017",
            DiagnosticSeverity.Error,
            "Method 'Validate' on type 'TestOptions' has wrong signature, expected IEnumerable<ValidationError> or IEnumerable<string>",
            source);
    }

    [Fact]
    public void Analyzer_ExplicitMatchingOptionsValidatorImplementation_NoDiagnostics()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true, Validator = typeof(TestValidator))]
            public class TestOptions;

            public class TestValidator : IOptionsValidator<TestOptions>
            {
                IEnumerable<ValidationError> IOptionsValidator<TestOptions>.Validate(TestOptions options) => [];
            }
            """;

        Assert.Empty(RunAnalyzer(source));
    }

    [Fact]
    public void Analyzer_MismatchedOptionsValidatorImplementation_ReportsTypeMismatch()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true, Validator = typeof(TestValidator))]
            public class TestOptions;

            public class OtherOptions;

            public class TestValidator : IOptionsValidator<OtherOptions>
            {
                IEnumerable<ValidationError> IOptionsValidator<OtherOptions>.Validate(OtherOptions options) => [];
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN015",
            DiagnosticSeverity.Error,
            "Validator 'TestValidator' validates 'OtherOptions' but is applied to options type 'TestOptions'. The validator must be for the same type.",
            source);
    }

    [Theory]
    [InlineData("Provider.ValidatorBase")]
    [InlineData("Provider.IntermediateValidator")]
    public void Analyzer_ReferencedValidatorProviderRecognizesDerivedValidator(
        string validatorBaseType)
    {
        var providerSource = """
            using NexusLabs.Needlr.Generators;

            [assembly: ValidatorProvider("Provider.ValidatorBase")]

            namespace Provider;

            public class ValidatorBase;

            public class IntermediateValidator : ValidatorBase;
            """;
        var source = $$"""
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            [Options(ValidateOnStart = true, Validator = typeof(TestValidator))]
            public class TestOptions;

            public class TestValidator : {{validatorBaseType}}
            {
                public bool Validate() => true;
            }
            """;

        Assert.Empty(RunAnalyzer(source, providerSource));
    }

    [Fact]
    public void Analyzer_ForeignOptionsAttribute_NoDiagnostics()
    {
        var source = """
            using System;
            using Foreign;

            namespace Foreign
            {
                [AttributeUsage(AttributeTargets.Class)]
                public sealed class OptionsAttribute : Attribute
                {
                    public bool ValidateOnStart { get; set; }
                }
            }

            namespace TestApp
            {
                [Options(ValidateOnStart = true)]
                public class TestOptions
                {
                    public int Validate() => 0;
                }
            }
            """;

        Assert.Empty(RunAnalyzer(source));
    }

    [Fact]
    public void Analyzer_ForeignValidatorProviderAttribute_DoesNotRecognizeValidator()
    {
        var providerSource = """
            using System;

            [assembly: Foreign.ValidatorProvider("Provider.ValidatorBase")]

            namespace Foreign
            {
                [AttributeUsage(AttributeTargets.Assembly)]
                public sealed class ValidatorProviderAttribute : Attribute
                {
                    public ValidatorProviderAttribute(string baseTypeName)
                    {
                    }
                }
            }

            namespace Provider
            {
                public class ValidatorBase;
            }
            """;
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            [Options(ValidateOnStart = true, Validator = typeof(TestValidator))]
            public class TestOptions;

            public class TestValidator : Provider.ValidatorBase;
            """;

        var diagnostic = AssertSingleDiagnostic(source, providerSource);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN014",
            DiagnosticSeverity.Error,
            "Validator type 'TestValidator' must have a Validate method. Implement IOptionsValidator<TestOptions> or add a 'Validate(TestOptions)' method.",
            source);
    }

    [Fact]
    public void Analyzer_RecordWithValidSelfValidation_NoDiagnostics()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            namespace TestApp;

            [Options(ValidateOnStart = true)]
            public record TestOptions(string Value)
            {
                public IEnumerable<string> Validate() => [];
            }
            """;

        Assert.Empty(RunAnalyzer(source));
    }

    [Fact]
    public void Analyzer_RecordWithInvalidSelfValidation_ReportsWrongSignature()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            [Options(ValidateOnStart = true)]
            public record TestOptions(string Value)
            {
                public int Validate() => 0;
            }
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN017",
            DiagnosticSeverity.Error,
            "Method 'Validate' on type 'TestOptions' has wrong signature, expected IEnumerable<ValidationError> or IEnumerable<string>",
            source);
    }

    [Fact]
    public void Analyzer_ExplicitValidateMethodMissing_ReportsSingleNotFoundDiagnostic()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            [Options(ValidateOnStart = true, Validator = typeof(TestValidator), ValidateMethod = "Check")]
            public class TestOptions;

            public class TestValidator;
            """;

        var diagnostic = AssertSingleDiagnostic(source);

        AssertDiagnostic(
            diagnostic,
            "NDLRGEN016",
            DiagnosticSeverity.Error,
            "Method 'Check' not found on type 'TestValidator'. Ensure the method exists and has the correct signature.",
            source);
    }

    private static Diagnostic AssertSingleDiagnostic(
        string source,
        string? referencedAssemblySource = null)
    {
        return Assert.Single(RunAnalyzer(source, referencedAssemblySource));
    }

    private static void AssertDiagnostic(
        Diagnostic diagnostic,
        string id,
        DiagnosticSeverity severity,
        string message,
        string source)
    {
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(severity, diagnostic.Severity);
        Assert.Equal(message, diagnostic.GetMessage());

        var attributeStart = source.IndexOf("[Options", StringComparison.Ordinal);
        Assert.NotEqual(-1, attributeStart);
        var expectedStart = attributeStart + 1;
        Assert.Equal(expectedStart, diagnostic.Location.SourceSpan.Start);
        Assert.Equal(source.AsSpan(attributeStart).IndexOf(']') - 1, diagnostic.Location.SourceSpan.Length);
    }

    private static Diagnostic[] RunAnalyzer(
        string source,
        string? referencedAssemblySource = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        MetadataReference[] references = Basic.Reference.Assemblies.Net100.References.All
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(GenerateTypeRegistryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(OptionsAttribute).Assembly.Location),
            })
            .ToArray();

        if (referencedAssemblySource != null)
        {
            references = references
                .Append(CreateMetadataReference(referencedAssemblySource, references))
                .ToArray();
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new OptionsAttributeAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result.ToArray();
    }

    private static MetadataReference CreateMetadataReference(
        string source,
        MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            "ReferencedAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        Assert.Empty(emitResult.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        stream.Position = 0;
        return MetadataReference.CreateFromStream(stream);
    }
}
