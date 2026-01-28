// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for the OptionsAttributeAnalyzer (Phase 5C).
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

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN018");
        Assert.Contains("TestValidator", diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
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

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN019");
        Assert.Contains("CustomValidate", diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }    [Fact]
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

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN016");
        Assert.Contains("NonExistentMethod", diagnostic.GetMessage());
        Assert.Contains("TestOptions", diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
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

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN016");
        Assert.Contains("NonExistentMethod", diagnostic.GetMessage());
        Assert.Contains("EmptyValidator", diagnostic.GetMessage());
    }    [Fact]
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

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN015");
        Assert.Contains("WrongTypeValidator", diagnostic.GetMessage());
        Assert.Contains("OtherOptions", diagnostic.GetMessage());
        Assert.Contains("TestOptions", diagnostic.GetMessage());
    }    [Fact]
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
    }    private static Diagnostic[] RunAnalyzer(string source)
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

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new OptionsAttributeAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result.ToArray();
    }}
