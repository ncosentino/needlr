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
/// Tests for NDLRGEN020: Options attribute is not compatible with AOT.
/// </summary>
public sealed class OptionsAotCompatibilityTests
{
    [Fact]
    public void Generator_EmitsDiagnostic_WhenOptionsUsedInAotProject()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class AppSettings
                {
                    public string ApiKey { get; set; } = "";
                }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, publishAot: true);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Single(ndlrgen020);
        Assert.Contains("AppSettings", ndlrgen020[0].GetMessage());
    }

    [Fact]
    public void Generator_EmitsDiagnostic_WhenOptionsUsedInIsAotCompatibleProject()
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
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, isAotCompatible: true);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Single(ndlrgen020);
        Assert.Contains("DatabaseOptions", ndlrgen020[0].GetMessage());
    }

    [Fact]
    public void Generator_NoDiagnostic_WhenOptionsUsedInNonAotProject()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class CacheSettings
                {
                    public int CacheTimeoutSeconds { get; set; } = 300;
                }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, publishAot: false, isAotCompatible: false);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);
    }

    [Fact]
    public void Generator_NoDiagnostic_WhenNoOptionsInAotProject()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public interface IMyService { }
                public class MyService : IMyService { }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, publishAot: true);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Empty(ndlrgen020);
    }

    [Fact]
    public void Generator_EmitsMultipleDiagnostics_WhenMultipleOptionsInAotProject()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class FirstOptions
                {
                    public string Value { get; set; } = "";
                }

                [Options]
                public class SecondOptions
                {
                    public int Number { get; set; }
                }
            }
            """;

        var diagnostics = RunGeneratorWithAotEnabled(source, publishAot: true);

        var ndlrgen020 = diagnostics.Where(d => d.Id == "NDLRGEN020").ToList();
        Assert.Equal(2, ndlrgen020.Count);
        Assert.Contains(ndlrgen020, d => d.GetMessage().Contains("FirstOptions"));
        Assert.Contains(ndlrgen020, d => d.GetMessage().Contains("SecondOptions"));
    }

    private static ImmutableArray<Diagnostic> RunGeneratorWithAotEnabled(
        string source,
        bool publishAot = false,
        bool isAotCompatible = false)
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

        var optionsProvider = new AotTestAnalyzerConfigOptionsProvider(publishAot, isAotCompatible);

        var generator = new TypeRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: Array.Empty<AdditionalText>(),
            parseOptions: (CSharpParseOptions)syntaxTree.Options,
            optionsProvider: optionsProvider);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        return diagnostics;
    }

    private sealed class AotTestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AotTestAnalyzerConfigOptions _globalOptions;

        public AotTestAnalyzerConfigOptionsProvider(bool publishAot, bool isAotCompatible)
        {
            var options = new Dictionary<string, string>
            {
                ["build_property.NeedlrBreadcrumbLevel"] = "Minimal"
            };

            if (publishAot)
            {
                options["build_property.PublishAot"] = "true";
            }

            if (isAotCompatible)
            {
                options["build_property.IsAotCompatible"] = "true";
            }

            _globalOptions = new AotTestAnalyzerConfigOptions(options);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
    }

    private sealed class AotTestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _options;

        public AotTestAnalyzerConfigOptions(Dictionary<string, string> options)
        {
            _options = options;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return _options.TryGetValue(key, out value!);
        }
    }
}
