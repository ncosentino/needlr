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
/// Tests documenting behavioral parity between AOT and non-AOT options generation.
/// These tests capture known limitations and expected behaviors that are consistent
/// across both paths. When behavior is improved, these tests will break, signaling
/// the change and indicating what documentation needs updating.
/// </summary>
public sealed class OptionsParityBehaviorTests
{
    /// <summary>
    /// Documents that interface-typed properties are silently skipped during binding.
    /// This matches ConfigurationBinder behavior which cannot construct interface types.
    /// When we add an analyzer to warn about unbindable types, this test should be updated
    /// to expect the warning diagnostic.
    /// </summary>
    [Fact]
    public void Options_WithInterfaceProperty_IsSkippedSilently_MatchesNonAotBehavior()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public interface INestedService { }

                [Options]
                public class OptionsWithInterface
                {
                    public string Name { get; set; } = "";
                    public INestedService Service { get; set; } = null!;
                }
            }
            """;

        // AOT path
        var (aotCode, aotDiagnostics) = RunGenerator(source, isAot: true);
        
        // Non-AOT path
        var (nonAotCode, nonAotDiagnostics) = RunGenerator(source, isAot: false);

        // Both paths should NOT emit NDLRGEN020 for interface properties
        // (matching ConfigurationBinder's silent skip behavior)
        Assert.Empty(aotDiagnostics.Where(d => d.Id == "NDLRGEN020"));
        Assert.Empty(nonAotDiagnostics.Where(d => d.Id == "NDLRGEN020"));

        // AOT should generate binding for Name but skip Service
        Assert.Contains("section[\"Name\"]", aotCode);
        // Interface property should be skipped (not cause an error)
        Assert.DoesNotContain("Service { get; set; } = null!", aotCode); // Not trying to bind it
    }

    /// <summary>
    /// Documents that object-typed properties cannot be bound from configuration.
    /// This is a known limitation matching ConfigurationBinder behavior.
    /// The property is silently skipped in both AOT and non-AOT paths.
    /// </summary>
    [Fact]
    public void Options_WithObjectProperty_IsSkippedSilently_MatchesNonAotBehavior()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class OptionsWithObject
                {
                    public string Name { get; set; } = "";
                    public object Data { get; set; } = null!;
                }
            }
            """;

        // AOT path
        var (aotCode, aotDiagnostics) = RunGenerator(source, isAot: true);
        
        // Non-AOT path
        var (nonAotCode, nonAotDiagnostics) = RunGenerator(source, isAot: false);

        // Both paths should NOT emit NDLRGEN020 for object properties
        Assert.Empty(aotDiagnostics.Where(d => d.Id == "NDLRGEN020"));
        Assert.Empty(nonAotDiagnostics.Where(d => d.Id == "NDLRGEN020"));

        // AOT should generate binding for Name but skip Data
        Assert.Contains("section[\"Name\"]", aotCode);
    }

    /// <summary>
    /// Documents that circular references in options are not detected at compile time.
    /// Both AOT and non-AOT paths currently allow circular references without warning.
    /// When NDLRGEN023 is implemented, this test should be updated to expect a diagnostic.
    /// </summary>
    [Fact]
    public void Options_WithCircularReference_NoCompileTimeWarning_KnownLimitation()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class SelfReferencingOptions
                {
                    public string Name { get; set; } = "";
                    public SelfReferencingOptions Child { get; set; } = null!;
                }
            }
            """;

        // AOT path
        var (_, aotDiagnostics) = RunGenerator(source, isAot: true);
        
        // Non-AOT path
        var (_, nonAotDiagnostics) = RunGenerator(source, isAot: false);

        // Currently, neither path detects circular references
        // When NDLRGEN023 is implemented, update this test to expect the diagnostic
        var circularDiagnostics = aotDiagnostics.Where(d => d.Id == "NDLRGEN023").ToList();
        Assert.Empty(circularDiagnostics); // Known limitation - no warning yet

        circularDiagnostics = nonAotDiagnostics.Where(d => d.Id == "NDLRGEN023").ToList();
        Assert.Empty(circularDiagnostics); // Known limitation - no warning yet
    }

    /// <summary>
    /// Documents that mutual circular references between options classes are not detected.
    /// This is the same limitation as self-referential properties.
    /// When NDLRGEN023 is implemented, this test should be updated to expect a diagnostic.
    /// </summary>
    [Fact]
    public void Options_WithMutualCircularReference_NoCompileTimeWarning_KnownLimitation()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options("SectionA")]
                public class OptionsA
                {
                    public string Value { get; set; } = "";
                    public OptionsB Other { get; set; } = null!;
                }

                [Options("SectionB")]
                public class OptionsB
                {
                    public string Value { get; set; } = "";
                    public OptionsA Other { get; set; } = null!;
                }
            }
            """;

        // AOT path
        var (_, aotDiagnostics) = RunGenerator(source, isAot: true);
        
        // Non-AOT path  
        var (_, nonAotDiagnostics) = RunGenerator(source, isAot: false);

        // Currently, neither path detects mutual circular references
        var circularDiagnostics = aotDiagnostics.Where(d => d.Id == "NDLRGEN023").ToList();
        Assert.Empty(circularDiagnostics); // Known limitation

        circularDiagnostics = nonAotDiagnostics.Where(d => d.Id == "NDLRGEN023").ToList();
        Assert.Empty(circularDiagnostics); // Known limitation
    }

    /// <summary>
    /// Documents that abstract class properties are silently skipped.
    /// ConfigurationBinder cannot construct abstract types.
    /// Both AOT and non-AOT paths should exhibit identical behavior.
    /// </summary>
    [Fact]
    public void Options_WithAbstractClassProperty_IsSkippedSilently_MatchesNonAotBehavior()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                public abstract class AbstractSettings
                {
                    public string Value { get; set; } = "";
                }

                [Options]
                public class OptionsWithAbstract
                {
                    public string Name { get; set; } = "";
                    public AbstractSettings Settings { get; set; } = null!;
                }
            }
            """;

        // AOT path
        var (aotCode, aotDiagnostics) = RunGenerator(source, isAot: true);
        
        // Non-AOT path
        var (nonAotCode, nonAotDiagnostics) = RunGenerator(source, isAot: false);

        // Both paths should NOT emit NDLRGEN020 for abstract properties
        Assert.Empty(aotDiagnostics.Where(d => d.Id == "NDLRGEN020"));
        Assert.Empty(nonAotDiagnostics.Where(d => d.Id == "NDLRGEN020"));

        // AOT should generate binding for Name
        Assert.Contains("section[\"Name\"]", aotCode);
    }

    /// <summary>
    /// Documents that init-only properties are skipped in AOT mode.
    /// The AOT binding approach uses a callback on an already-constructed instance,
    /// which cannot assign to init-only properties. This is an AOT-specific limitation.
    /// Non-AOT uses ConfigurationBinder which can set init-only via reflection.
    /// When a future enhancement adds proper init-only support, update this test.
    /// </summary>
    [Fact]
    public void Options_WithInitOnlyProperties_AreSkippedInAot_KnownLimitation()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public class ImmutableOptions
                {
                    public string Name { get; init; } = "default";
                    public int Count { get; init; } = 0;
                    public bool Enabled { get; set; } = false; // Regular setter works
                }
            }
            """;

        // AOT path - init-only properties should be skipped
        var (aotCode, _) = RunGenerator(source, isAot: true);
        
        // Init-only properties should have skip comments
        Assert.Contains("Skipped: Name (init-only property cannot be bound in AOT mode)", aotCode);
        Assert.Contains("Skipped: Count (init-only property cannot be bound in AOT mode)", aotCode);
        
        // Regular setter should still work
        Assert.Contains("section[\"Enabled\"]", aotCode);
    }

    /// <summary>
    /// Documents that positional records have init-only properties that are skipped in AOT.
    /// Even though we generate a parameterless constructor, the properties are still init-only
    /// and cannot be assigned in the Configure callback.
    /// This is a known limitation of the current AOT implementation.
    /// </summary>
    [Fact]
    public void Options_PositionalRecord_InitOnlyPropertiesSkippedInAot_KnownLimitation()
    {
        var source = """
            using NexusLabs.Needlr.Generators;
            
            [assembly: GenerateTypeRegistry]
            
            namespace TestApp
            {
                [Options]
                public partial record PositionalOptions(string Host, int Port, bool Secure);
            }
            """;

        // AOT path - positional record properties are init-only and should be skipped
        var (aotCode, _) = RunGenerator(source, isAot: true);
        
        // All properties should have skip comments (they're all init-only)
        Assert.Contains("Skipped: Host (init-only property cannot be bound in AOT mode)", aotCode);
        Assert.Contains("Skipped: Port (init-only property cannot be bound in AOT mode)", aotCode);
        Assert.Contains("Skipped: Secure (init-only property cannot be bound in AOT mode)", aotCode);
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
