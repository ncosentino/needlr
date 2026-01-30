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
/// Tests for AOT-compatible enum binding in options classes.
/// </summary>
public sealed class OptionsAotEnumTests
{
    [Fact]
    public void Generator_EmitsEnumBinding_ForEnumProperty()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum LogLevel
                {
                    Debug,
                    Info,
                    Warning,
                    Error
                }

                [Options]
                public class LoggingOptions
                {
                    public LogLevel Level { get; set; } = LogLevel.Info;
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should not emit NDLRGEN020 for enum properties
        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));

        // Should generate Enum.TryParse binding
        Assert.Contains("Enum.TryParse", generatedCode);
        Assert.Contains("LogLevel", generatedCode);
        Assert.Contains("section[\"Level\"]", generatedCode);
    }

    [Fact]
    public void Generator_EmitsEnumBinding_ForNullableEnumProperty()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum Priority
                {
                    Low,
                    Medium,
                    High
                }

                [Options]
                public class TaskOptions
                {
                    public Priority? DefaultPriority { get; set; }
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Should not emit NDLRGEN020 for nullable enum properties
        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));

        // Should generate Enum.TryParse binding for nullable
        Assert.Contains("Enum.TryParse", generatedCode);
        Assert.Contains("Priority", generatedCode);
    }

    [Fact]
    public void Generator_EmitsEnumBinding_ForFlagsEnum()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            using System;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Flags]
                public enum Permissions
                {
                    None = 0,
                    Read = 1,
                    Write = 2,
                    Execute = 4,
                    All = Read | Write | Execute
                }

                [Options]
                public class SecurityOptions
                {
                    public Permissions UserPermissions { get; set; } = Permissions.Read;
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        // Flags enums should also work with Enum.TryParse
        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));
        Assert.Contains("Enum.TryParse", generatedCode);
    }

    [Fact]
    public void Generator_EmitsEnumBinding_ForMultipleEnumProperties()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum Environment { Development, Staging, Production }
                public enum LogLevel { Debug, Info, Warning, Error }

                [Options]
                public class AppOptions
                {
                    public Environment Env { get; set; } = Environment.Development;
                    public LogLevel MinLogLevel { get; set; } = LogLevel.Info;
                    public string AppName { get; set; } = "";
                }
            }
            """;

        var (generatedCode, diagnostics) = RunGeneratorWithAot(source);

        Assert.Empty(diagnostics.Where(d => d.Id == "NDLRGEN020"));
        
        // Should bind all properties
        Assert.Contains("section[\"Env\"]", generatedCode);
        Assert.Contains("section[\"MinLogLevel\"]", generatedCode);
        Assert.Contains("section[\"AppName\"]", generatedCode);
        
        // Should have enum parsing for both
        Assert.Contains("Environment", generatedCode);
        Assert.Contains("LogLevel", generatedCode);
    }

    [Fact]
    public void Generator_EnumBinding_MatchesNonAotBehavior()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public enum Status { Pending, Active, Completed }

                [Options]
                public class WorkflowOptions
                {
                    public Status InitialStatus { get; set; } = Status.Pending;
                }
            }
            """;

        // Both paths should handle enums without errors
        var (_, aotDiagnostics) = RunGeneratorWithAot(source);
        var (_, nonAotDiagnostics) = RunGeneratorWithoutAot(source);

        Assert.Empty(aotDiagnostics.Where(d => d.Id == "NDLRGEN020"));
        Assert.Empty(nonAotDiagnostics.Where(d => d.Id == "NDLRGEN020"));
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
