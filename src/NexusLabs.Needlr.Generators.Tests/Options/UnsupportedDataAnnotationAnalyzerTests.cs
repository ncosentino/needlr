// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Options;

/// <summary>
/// Tests for the UnsupportedDataAnnotationAnalyzer (NDLRGEN030).
/// </summary>
public sealed class UnsupportedDataAnnotationAnalyzerTests
{
    [Fact]
    public void Analyzer_SupportedDataAnnotations_NoDiagnostics()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            namespace TestApp
            {
                [Options]
                public class TestOptions
                {
                    [Required]
                    public string Name { get; set; } = "";
                    
                    [Range(1, 100)]
                    public int Count { get; set; }
                    
                    [StringLength(50, MinimumLength = 1)]
                    public string Title { get; set; } = "";
                    
                    [MinLength(5)]
                    public string MinLengthValue { get; set; } = "";
                    
                    [MaxLength(100)]
                    public string MaxLengthValue { get; set; } = "";
                    
                    [RegularExpression(@"^\d+$")]
                    public string Pattern { get; set; } = "";
                    
                    [EmailAddress]
                    public string Email { get; set; } = "";
                    
                    [Phone]
                    public string PhoneNumber { get; set; } = "";
                    
                    [Url]
                    public string Website { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyzer_CreditCardAttribute_ReportsWarning()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            namespace TestApp
            {
                [Options]
                public class TestOptions
                {
                    [CreditCard]
                    public string CardNumber { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN030");
        Assert.Contains("CreditCardAttribute", diagnostic.GetMessage());
        Assert.Contains("TestOptions", diagnostic.GetMessage());
        Assert.Contains("CardNumber", diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_FileExtensionsAttribute_ReportsWarning()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            namespace TestApp
            {
                [Options]
                public class TestOptions
                {
                    [FileExtensions(Extensions = ".pdf,.docx")]
                    public string FileName { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN030");
        Assert.Contains("FileExtensionsAttribute", diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_CustomValidationAttribute_ReportsWarning()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            namespace TestApp
            {
                [Options]
                public class TestOptions
                {
                    [CustomValidation(typeof(TestValidator), "ValidateValue")]
                    public string Value { get; set; } = "";
                }
                
                public static class TestValidator
                {
                    public static ValidationResult? ValidateValue(string value)
                    {
                        return ValidationResult.Success;
                    }
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN030");
        Assert.Contains("CustomValidationAttribute", diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Analyzer_MultipleUnsupportedAttributes_ReportsMultipleWarnings()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            namespace TestApp
            {
                [Options]
                public class TestOptions
                {
                    [CreditCard]
                    public string CardNumber { get; set; } = "";
                    
                    [FileExtensions(Extensions = ".pdf")]
                    public string FileName { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        Assert.Equal(2, diagnostics.Count(d => d.Id == "NDLRGEN030"));
    }

    [Fact]
    public void Analyzer_MixedSupportedAndUnsupported_OnlyReportsUnsupported()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            namespace TestApp
            {
                [Options]
                public class TestOptions
                {
                    [Required]
                    [CreditCard]
                    public string CardNumber { get; set; } = "";
                    
                    [StringLength(100)]
                    public string Name { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN030");
        Assert.Contains("CreditCardAttribute", diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_ClassWithoutOptions_NoDiagnostics()
    {
        var source = """
            using System.ComponentModel.DataAnnotations;
            
            namespace TestApp
            {
                public class RegularClass
                {
                    [CreditCard]
                    public string CardNumber { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyzer_RecordWithUnsupportedAttribute_ReportsWarning()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System.ComponentModel.DataAnnotations;
            
            namespace TestApp
            {
                [Options]
                public record TestOptions
                {
                    [CreditCard]
                    public string CardNumber { get; init; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == "NDLRGEN030");
        Assert.Contains("CreditCardAttribute", diagnostic.GetMessage());
    }

    [Fact]
    public void Analyzer_NonDataAnnotationAttribute_NoDiagnostics()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System;
            
            namespace TestApp
            {
                [Options]
                public class TestOptions
                {
                    [Obsolete("Deprecated")]
                    public string Value { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunAnalyzer(source);

        Assert.Empty(diagnostics);
    }

    private static Diagnostic[] RunAnalyzer(string source)
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

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new UnsupportedDataAnnotationAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result.ToArray();
    }
}
