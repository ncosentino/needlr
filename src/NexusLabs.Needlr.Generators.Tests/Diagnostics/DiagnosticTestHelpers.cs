using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// Shared test infrastructure for diagnostic output tests.
/// </summary>
internal static class DiagnosticTestHelpers
{
    public static string GetDiagnosticContent(string source, string fieldName)
    {
        return GetDiagnosticContentWithFilter(source, fieldName, null);
    }

    public static string GetDiagnosticContentWithFilter(string source, string fieldName, string? filter)
    {
        var generatedFiles = RunGeneratorWithDiagnostics(source, enabled: true, outputPath: null, filter: filter);
        var diagnosticsFile = generatedFiles.FirstOrDefault(f => f.FilePath.Contains("NeedlrDiagnostics"));
        
        if (diagnosticsFile == null)
        {
            return string.Empty;
        }

        var pattern = $@"public\s+const\s+string\s+{fieldName}\s*=\s*@""([^""]*(?:""""[^""]*)*)""";
        var match = System.Text.RegularExpressions.Regex.Match(diagnosticsFile.Content, pattern, 
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (match.Success)
        {
            return match.Groups[1].Value.Replace("\"\"", "\"");
        }

        return string.Empty;
    }

    public static GeneratedFile[] RunGeneratorWithDiagnosticsEnabled(string source, bool? enabled)
    {
        return RunGeneratorWithDiagnostics(source, enabled, outputPath: null, filter: null);
    }

    public static GeneratedFile[] RunGeneratorWithDiagnosticsEnabledValue(string source, string enabledValue)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(GenerateTypeRegistryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DeferToContainerAttribute).Assembly.Location),
            })
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeRegistryGenerator();
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(enabledValue, null, null);

        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: Array.Empty<AdditionalText>(),
            parseOptions: (CSharpParseOptions)syntaxTree.Options,
            optionsProvider: optionsProvider);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        return outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => new GeneratedFile(t.FilePath, t.GetText().ToString()))
            .ToArray();
    }

    public static GeneratedFile[] RunGeneratorWithDiagnostics(string source, bool? enabled, string? outputPath, string? filter)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(GenerateTypeRegistryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DeferToContainerAttribute).Assembly.Location),
            })
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeRegistryGenerator();
        
        var enabledValue = enabled.HasValue ? (enabled.Value ? "true" : "false") : null;
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(enabledValue, outputPath, filter);

        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: Array.Empty<AdditionalText>(),
            parseOptions: (CSharpParseOptions)syntaxTree.Options,
            optionsProvider: optionsProvider);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        return outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => new GeneratedFile(t.FilePath, t.GetText().ToString()))
            .ToArray();
    }

    /// <summary>
    /// Gets diagnostic content from a host assembly that references a plugin assembly with [GenerateTypeRegistry].
    /// </summary>
    public static string GetDiagnosticContentWithReferencedAssembly(
        string hostSource,
        string pluginSource,
        string pluginAssemblyName,
        string fieldName)
    {
        return GetDiagnosticContentWithMultipleReferencedAssemblies(
            hostSource,
            new[] { (pluginSource, pluginAssemblyName) },
            fieldName);
    }

    /// <summary>
    /// Gets diagnostic content from a host assembly that references multiple plugin assemblies.
    /// </summary>
    public static string GetDiagnosticContentWithMultipleReferencedAssemblies(
        string hostSource,
        (string Source, string AssemblyName)[] pluginSources,
        string fieldName)
    {
        // First, compile each plugin assembly
        var pluginReferences = new List<MetadataReference>();
        
        foreach (var (source, assemblyName) in pluginSources)
        {
            var pluginTree = CSharpSyntaxTree.ParseText(source);
            var pluginRefs = Basic.Reference.Assemblies.Net100.References.All
                .Concat(new[]
                {
                    MetadataReference.CreateFromFile(typeof(GenerateTypeRegistryAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(DeferToContainerAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(DecoratorForAttribute<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(InterceptAttribute<>).Assembly.Location),
                })
                .ToArray();

            var pluginCompilation = CSharpCompilation.Create(
                assemblyName,
                new[] { pluginTree },
                pluginRefs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Create an in-memory reference from the plugin compilation
            using var ms = new System.IO.MemoryStream();
            var emitResult = pluginCompilation.Emit(ms);
            if (!emitResult.Success)
            {
                var errors = string.Join("\n", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
                throw new InvalidOperationException($"Plugin compilation failed: {errors}");
            }
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            pluginReferences.Add(MetadataReference.CreateFromStream(ms));
        }

        // Now compile the host assembly with references to the plugins
        var hostTree = CSharpSyntaxTree.ParseText(hostSource);
        var hostReferences = Basic.Reference.Assemblies.Net100.References.All
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(GenerateTypeRegistryAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DeferToContainerAttribute).Assembly.Location),
            })
            .Concat(pluginReferences)
            .ToArray();

        var hostCompilation = CSharpCompilation.Create(
            "HostAssembly",
            new[] { hostTree },
            hostReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeRegistryGenerator();
        var optionsProvider = new TestAnalyzerConfigOptionsProvider("true", null, null);

        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: Array.Empty<AdditionalText>(),
            parseOptions: (CSharpParseOptions)hostTree.Options,
            optionsProvider: optionsProvider);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            hostCompilation,
            out var outputCompilation,
            out var diagnostics);

        var diagnosticsFile = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => new GeneratedFile(t.FilePath, t.GetText().ToString()))
            .FirstOrDefault(f => f.FilePath.Contains("NeedlrDiagnostics"));

        if (diagnosticsFile == null)
        {
            return string.Empty;
        }

        var pattern = $@"public\s+const\s+string\s+{fieldName}\s*=\s*@""([^""]*(?:""""[^""]*)*)""";
        var match = System.Text.RegularExpressions.Regex.Match(diagnosticsFile.Content, pattern,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (match.Success)
        {
            return match.Groups[1].Value.Replace("\"\"", "\"");
        }

        return string.Empty;
    }
}

public sealed record GeneratedFile(string FilePath, string Content);

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly TestAnalyzerConfigOptions _globalOptions;

    public TestAnalyzerConfigOptionsProvider(string? diagnosticsEnabled, string? diagnosticsPath, string? diagnosticsFilter)
    {
        var options = new Dictionary<string, string>();

        if (diagnosticsEnabled != null)
        {
            options["build_property.NeedlrDiagnostics"] = diagnosticsEnabled;
        }

        if (diagnosticsPath != null)
        {
            options["build_property.NeedlrDiagnosticsPath"] = diagnosticsPath;
        }

        if (diagnosticsFilter != null)
        {
            options["build_property.NeedlrDiagnosticsFilter"] = diagnosticsFilter;
        }

        options["build_property.NeedlrBreadcrumbLevel"] = "Minimal";

        _globalOptions = new TestAnalyzerConfigOptions(options);
    }

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
}

internal sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
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
