using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NexusLabs.Needlr.Generators.Tests.Diagnostics;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Centralized test infrastructure for running source generators.
/// Eliminates duplication of RunGenerator boilerplate across test files.
/// </summary>
public sealed class GeneratorTestRunner
{
    private readonly List<string> _sources = new();
    private readonly List<MetadataReference> _additionalReferences = new();
    private readonly Dictionary<string, string> _analyzerConfigOptions = new();
    private CSharpParseOptions? _parseOptions;
    private string _assemblyName = "TestAssembly";

    /// <summary>
    /// Sets the source code to compile and run the generator against.
    /// </summary>
    public GeneratorTestRunner WithSource(string source)
    {
        _sources.Add(source);
        return this;
    }

    /// <summary>
    /// Adds multiple source files to the compilation.
    /// </summary>
    public GeneratorTestRunner WithSources(params string[] sources)
    {
        _sources.AddRange(sources);
        return this;
    }

    /// <summary>
    /// Sets a custom assembly name for the compilation.
    /// </summary>
    public GeneratorTestRunner WithAssemblyName(string assemblyName)
    {
        _assemblyName = assemblyName;
        return this;
    }

    /// <summary>
    /// Adds a reference to the assembly containing the specified type.
    /// </summary>
    public GeneratorTestRunner WithReference<T>()
    {
        _additionalReferences.Add(MetadataReference.CreateFromFile(typeof(T).Assembly.Location));
        return this;
    }

    /// <summary>
    /// Adds references to assemblies containing the specified types.
    /// </summary>
    public GeneratorTestRunner WithReferences<T1, T2>()
    {
        return WithReference<T1>().WithReference<T2>();
    }

    /// <summary>
    /// Adds references to assemblies containing the specified types.
    /// </summary>
    public GeneratorTestRunner WithReferences<T1, T2, T3>()
    {
        return WithReference<T1>().WithReference<T2>().WithReference<T3>();
    }

    /// <summary>
    /// Adds references to assemblies containing the specified types.
    /// </summary>
    public GeneratorTestRunner WithReferences<T1, T2, T3, T4>()
    {
        return WithReference<T1>().WithReference<T2>().WithReference<T3>().WithReference<T4>();
    }

    /// <summary>
    /// Enables documentation mode for XML doc extraction.
    /// </summary>
    public GeneratorTestRunner WithDocumentationMode()
    {
        _parseOptions = new CSharpParseOptions(documentationMode: DocumentationMode.Parse);
        return this;
    }

    /// <summary>
    /// Sets an analyzer config option (e.g., build_property.NeedlrDiagnostics).
    /// </summary>
    public GeneratorTestRunner WithAnalyzerConfigOption(string key, string value)
    {
        _analyzerConfigOptions[key] = value;
        return this;
    }

    /// <summary>
    /// Enables Needlr diagnostics generation.
    /// </summary>
    public GeneratorTestRunner WithDiagnosticsEnabled(bool enabled = true)
    {
        return WithAnalyzerConfigOption("build_property.NeedlrDiagnostics", enabled ? "true" : "false");
    }

    /// <summary>
    /// Sets the breadcrumb level for diagnostics.
    /// </summary>
    public GeneratorTestRunner WithBreadcrumbLevel(string level)
    {
        return WithAnalyzerConfigOption("build_property.NeedlrBreadcrumbLevel", level);
    }

    /// <summary>
    /// Enables AOT mode (PublishAot=true).
    /// </summary>
    public GeneratorTestRunner WithAotMode(bool enabled = true)
    {
        if (enabled)
        {
            return WithAnalyzerConfigOption("build_property.PublishAot", "true");
        }
        return this;
    }

    /// <summary>
    /// Sets the diagnostics filter.
    /// </summary>
    public GeneratorTestRunner WithDiagnosticsFilter(string filter)
    {
        return WithAnalyzerConfigOption("build_property.NeedlrDiagnosticsFilter", filter);
    }

    /// <summary>
    /// Sets the diagnostics output path.
    /// </summary>
    public GeneratorTestRunner WithDiagnosticsPath(string path)
    {
        return WithAnalyzerConfigOption("build_property.NeedlrDiagnosticsPath", path);
    }

    /// <summary>
    /// Sets the project directory for breadcrumb relative paths.
    /// </summary>
    public GeneratorTestRunner WithProjectDir(string path)
    {
        return WithAnalyzerConfigOption("build_property.projectdir", path);
    }

    /// <summary>
    /// Runs the TypeRegistryGenerator and returns all generated code concatenated.
    /// </summary>
    public string RunTypeRegistryGenerator()
    {
        var files = RunTypeRegistryGeneratorFiles();
        if (files.Length == 0)
        {
            return string.Empty;
        }
        return string.Join("\n\n", files.Select(f => f.Content));
    }

    /// <summary>
    /// Runs the TypeRegistryGenerator and returns the ServiceCatalog output only.
    /// </summary>
    public string GetServiceCatalogOutput()
    {
        var files = RunTypeRegistryGeneratorFiles();
        var catalogFile = files.FirstOrDefault(f => f.FilePath.Contains("ServiceCatalog"));
        return catalogFile?.Content ?? string.Empty;
    }

    /// <summary>
    /// Runs the TypeRegistryGenerator and returns the TypeRegistry output only.
    /// </summary>
    public string GetTypeRegistryOutput()
    {
        var files = RunTypeRegistryGeneratorFiles();
        var registryFile = files.FirstOrDefault(f => f.FilePath.EndsWith("TypeRegistry.g.cs"));
        return registryFile?.Content ?? string.Empty;
    }

    /// <summary>
    /// Runs the TypeRegistryGenerator and returns a specific file by partial path match.
    /// </summary>
    public string GetFileContaining(string pathFragment)
    {
        var files = RunTypeRegistryGeneratorFiles();
        var file = files.FirstOrDefault(f => f.FilePath.Contains(pathFragment));
        return file?.Content ?? string.Empty;
    }

    /// <summary>
    /// Runs the TypeRegistryGenerator and returns code excluding specified files.
    /// </summary>
    public string RunTypeRegistryGeneratorExcluding(params string[] excludePatterns)
    {
        var files = RunTypeRegistryGeneratorFiles()
            .Where(f => !excludePatterns.Any(p => f.FilePath.Contains(p)));
        return string.Join("\n\n", files.Select(f => f.Content));
    }

    /// <summary>
    /// Runs the TypeRegistryGenerator and returns all generated files.
    /// </summary>
    public GeneratedFile[] RunTypeRegistryGeneratorFiles()
    {
        var generator = new TypeRegistryGenerator();
        return RunGenerator(generator);
    }

    /// <summary>
    /// Runs the TypeRegistryGenerator and returns diagnostics.
    /// </summary>
    public IReadOnlyList<Diagnostic> RunTypeRegistryGeneratorDiagnostics()
    {
        var generator = new TypeRegistryGenerator();
        _ = RunGeneratorWithDiagnostics(generator, out var diagnostics);
        return diagnostics;
    }

    /// <summary>
    /// Runs a specific generator and returns all generated files.
    /// </summary>
    public GeneratedFile[] RunGenerator(IIncrementalGenerator generator)
    {
        return RunGeneratorWithDiagnostics(generator, out _);
    }

    /// <summary>
    /// Runs a specific generator and returns all generated files along with diagnostics.
    /// </summary>
    public GeneratedFile[] RunGeneratorWithDiagnostics(IIncrementalGenerator generator, out IReadOnlyList<Diagnostic> generatorDiagnostics)
    {
        var parseOptions = _parseOptions ?? new CSharpParseOptions();
        var syntaxTrees = _sources.Select(s => CSharpSyntaxTree.ParseText(s, parseOptions)).ToArray();

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Concat(_additionalReferences)
            .ToArray();

        var compilation = CSharpCompilation.Create(
            _assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        ISourceGenerator sourceGenerator = generator.AsSourceGenerator();

        CSharpGeneratorDriver driver;
        if (_analyzerConfigOptions.Count > 0)
        {
            var optionsProvider = new FlexibleAnalyzerConfigOptionsProvider(_analyzerConfigOptions);
            driver = CSharpGeneratorDriver.Create(
                generators: new[] { sourceGenerator },
                additionalTexts: Array.Empty<AdditionalText>(),
                parseOptions: parseOptions,
                optionsProvider: optionsProvider);
        }
        else
        {
            driver = CSharpGeneratorDriver.Create(generator);
        }

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        generatorDiagnostics = diagnostics;

        return outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .OrderBy(t => t.FilePath)
            .Select(t => new GeneratedFile(t.FilePath, t.GetText().ToString()))
            .ToArray();
    }

    /// <summary>
    /// Creates a default runner configured for TypeRegistry tests.
    /// </summary>
    public static GeneratorTestRunner ForTypeRegistry()
    {
        return new GeneratorTestRunner()
            .WithReference<GenerateTypeRegistryAttribute>()
            .WithReference<DeferToContainerAttribute>();
    }

    /// <summary>
    /// Creates a default runner configured for Factory tests.
    /// </summary>
    public static GeneratorTestRunner ForFactory()
    {
        return new GeneratorTestRunner()
            .WithReference<GenerateTypeRegistryAttribute>()
            .WithReference<GenerateFactoryAttribute>();
    }

    /// <summary>
    /// Creates a default runner configured for Decorator tests.
    /// </summary>
    public static GeneratorTestRunner ForDecorator()
    {
        return new GeneratorTestRunner()
            .WithReference<GenerateTypeRegistryAttribute>()
            .WithReference<DecoratorForAttribute<object>>();
    }

    /// <summary>
    /// Creates a default runner configured for Interceptor tests.
    /// Uses GenerateTypeRegistryAttribute assembly which contains all interceptor types.
    /// </summary>
    public static GeneratorTestRunner ForInterceptor()
    {
        // InterceptAttribute is in the same assembly as other generator attributes
        return new GeneratorTestRunner()
            .WithReference<GenerateTypeRegistryAttribute>()
            .WithReference<IMethodInterceptor>();
    }

    /// <summary>
    /// Creates a default runner configured for Options tests.
    /// </summary>
    public static GeneratorTestRunner ForOptions()
    {
        return new GeneratorTestRunner()
            .WithReference<GenerateTypeRegistryAttribute>()
            .WithReference<OptionsAttribute>();
    }

    /// <summary>
    /// Creates a default runner configured for Provider tests.
    /// </summary>
    public static GeneratorTestRunner ForProvider()
    {
        return new GeneratorTestRunner()
            .WithReference<GenerateTypeRegistryAttribute>()
            .WithReference<ProviderAttribute>();
    }

    /// <summary>
    /// Creates a runner that uses inline attribute definitions (no real assembly references).
    /// Useful for tests that need specific attribute shapes.
    /// </summary>
    public static GeneratorTestRunner WithInlineAttributes()
    {
        return new GeneratorTestRunner()
            .WithSource(InlineAttributeDefinitions);
    }

    /// <summary>
    /// Creates a runner for hosted service tests with inline hosting type definitions.
    /// </summary>
    public static GeneratorTestRunner ForHostedServiceWithInlineTypes()
    {
        return new GeneratorTestRunner()
            .WithSource(InlineAttributeDefinitions)
            .WithSource(InlineHostingDefinitions);
    }

    /// <summary>
    /// Creates a runner for hosted service tests with inline hosting type definitions
    /// and lifetime attributes (Singleton, Scoped, Transient).
    /// </summary>
    public static GeneratorTestRunner ForHostedServiceWithLifetimes()
    {
        return new GeneratorTestRunner()
            .WithSource(InlineAttributeDefinitionsWithLifetimes)
            .WithSource(InlineHostingDefinitions);
    }

    /// <summary>
    /// Inline attribute definitions for tests that don't use real assembly references.
    /// </summary>
    public const string InlineAttributeDefinitions = """
        namespace NexusLabs.Needlr
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class DoNotAutoRegisterAttribute : System.Attribute { }
        }

        namespace NexusLabs.Needlr.Generators
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly)]
            public sealed class GenerateTypeRegistryAttribute : System.Attribute
            {
                public string[]? IncludeNamespacePrefixes { get; set; }
                public bool IncludeSelf { get; set; } = true;
            }
        }
        """;

    /// <summary>
    /// Extended inline attribute definitions including lifetime attributes.
    /// </summary>
    public const string InlineAttributeDefinitionsWithLifetimes = """
        namespace NexusLabs.Needlr
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class DoNotAutoRegisterAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class SingletonAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class ScopedAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class TransientAttribute : System.Attribute { }
        }

        namespace NexusLabs.Needlr.Generators
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly)]
            public sealed class GenerateTypeRegistryAttribute : System.Attribute
            {
                public string[]? IncludeNamespacePrefixes { get; set; }
                public bool IncludeSelf { get; set; } = true;
            }
        }
        """;

    /// <summary>
    /// Inline hosting type definitions for hosted service tests.
    /// </summary>
    public const string InlineHostingDefinitions = """
        namespace Microsoft.Extensions.Hosting
        {
            public interface IHostedService
            {
                System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken);
                System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken);
            }

            public abstract class BackgroundService : IHostedService
            {
                public virtual System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken) 
                    => System.Threading.Tasks.Task.CompletedTask;
                public virtual System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken) 
                    => System.Threading.Tasks.Task.CompletedTask;
                protected abstract System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken);
            }
        }
        """;

    /// <summary>
    /// Comprehensive inline definitions for decorator tests.
    /// </summary>
    public const string InlineDecoratorDefinitions = """
        namespace NexusLabs.Needlr
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class DoNotAutoRegisterAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class SingletonAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class DecoratorForAttribute<TService> : System.Attribute
                where TService : class
            {
                public int Order { get; set; } = 0;
                public System.Type ServiceType => typeof(TService);
            }
        }

        namespace NexusLabs.Needlr.Generators
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly)]
            public sealed class GenerateTypeRegistryAttribute : System.Attribute
            {
                public string[]? IncludeNamespacePrefixes { get; set; }
                public bool IncludeSelf { get; set; } = true;
            }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public sealed class OpenDecoratorForAttribute : System.Attribute
            {
                public OpenDecoratorForAttribute(System.Type openGenericServiceType)
                {
                    OpenGenericServiceType = openGenericServiceType;
                }

                public System.Type OpenGenericServiceType { get; }
                public int Order { get; set; } = 0;
            }

            public enum InjectableLifetime
            {
                Singleton = 0,
                Scoped = 1,
                Transient = 2
            }

            public readonly struct InjectableTypeInfo
            {
                public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces)
                    : this(type, interfaces, null) { }

                public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces, InjectableLifetime? lifetime)
                {
                    Type = type;
                    Interfaces = interfaces;
                    Lifetime = lifetime;
                }

                public System.Type Type { get; }
                public System.Collections.Generic.IReadOnlyList<System.Type> Interfaces { get; }
                public InjectableLifetime? Lifetime { get; }
            }

            public readonly struct PluginTypeInfo
            {
                public PluginTypeInfo(System.Type pluginType, System.Collections.Generic.IReadOnlyList<System.Type> pluginInterfaces, System.Func<object> factory)
                {
                    PluginType = pluginType;
                    PluginInterfaces = pluginInterfaces;
                    Factory = factory;
                }

                public System.Type PluginType { get; }
                public System.Collections.Generic.IReadOnlyList<System.Type> PluginInterfaces { get; }
                public System.Func<object> Factory { get; }
            }

            public static class NeedlrSourceGenBootstrap
            {
                public static void Register(
                    System.Func<System.Collections.Generic.IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
                    System.Func<System.Collections.Generic.IReadOnlyList<PluginTypeInfo>> pluginTypeProvider,
                    System.Action<object>? decoratorApplier = null)
                {
                }
            }
        }
        """;

    /// <summary>
    /// Comprehensive inline definitions for interceptor tests.
    /// </summary>
    public const string InlineInterceptorDefinitions = """
        namespace NexusLabs.Needlr
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class DoNotAutoRegisterAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class ScopedAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class SingletonAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class TransientAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
            public sealed class InterceptAttribute : System.Attribute
            {
                public InterceptAttribute(System.Type interceptorType) { InterceptorType = interceptorType; }
                public System.Type InterceptorType { get; }
                public int Order { get; set; } = 0;
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
            public sealed class InterceptAttribute<TInterceptor> : System.Attribute
                where TInterceptor : class, IMethodInterceptor
            {
                public System.Type InterceptorType => typeof(TInterceptor);
                public int Order { get; set; } = 0;
            }

            public interface IMethodInterceptor
            {
                System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation);
            }

            public interface IMethodInvocation
            {
                object Target { get; }
                System.Reflection.MethodInfo Method { get; }
                object?[] Arguments { get; }
                System.Type[] GenericArguments { get; }
                System.Threading.Tasks.ValueTask<object?> ProceedAsync();
            }

            public sealed class MethodInvocation : IMethodInvocation
            {
                private readonly System.Func<System.Threading.Tasks.ValueTask<object?>> _proceed;
                public MethodInvocation(object target, System.Reflection.MethodInfo method, object?[] arguments, System.Func<System.Threading.Tasks.ValueTask<object?>> proceed)
                {
                    Target = target;
                    Method = method;
                    Arguments = arguments;
                    GenericArguments = System.Type.EmptyTypes;
                    _proceed = proceed;
                }
                public object Target { get; }
                public System.Reflection.MethodInfo Method { get; }
                public object?[] Arguments { get; }
                public System.Type[] GenericArguments { get; }
                public System.Threading.Tasks.ValueTask<object?> ProceedAsync() => _proceed();
            }
        }

        namespace NexusLabs.Needlr.Generators
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly)]
            public sealed class GenerateTypeRegistryAttribute : System.Attribute
            {
                public string[]? IncludeNamespacePrefixes { get; set; }
                public bool IncludeSelf { get; set; } = true;
            }

            public enum InjectableLifetime
            {
                Singleton = 0,
                Scoped = 1,
                Transient = 2
            }

            public readonly struct InjectableTypeInfo
            {
                public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces)
                    : this(type, interfaces, null) { }

                public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces, InjectableLifetime? lifetime)
                {
                    Type = type;
                    Interfaces = interfaces;
                    Lifetime = lifetime;
                }

                public System.Type Type { get; }
                public System.Collections.Generic.IReadOnlyList<System.Type> Interfaces { get; }
                public InjectableLifetime? Lifetime { get; }
            }

            public readonly struct PluginTypeInfo
            {
                public PluginTypeInfo(System.Type pluginType, System.Collections.Generic.IReadOnlyList<System.Type> pluginInterfaces, System.Func<object> factory)
                {
                    PluginType = pluginType;
                    PluginInterfaces = pluginInterfaces;
                    Factory = factory;
                }

                public System.Type PluginType { get; }
                public System.Collections.Generic.IReadOnlyList<System.Type> PluginInterfaces { get; }
                public System.Func<object> Factory { get; }
            }

            public static class NeedlrSourceGenBootstrap
            {
                public static void Register(
                    System.Func<System.Collections.Generic.IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
                    System.Func<System.Collections.Generic.IReadOnlyList<PluginTypeInfo>> pluginTypeProvider,
                    System.Action<object>? decoratorApplier = null)
                {
                }
            }
        }
        """;

    /// <summary>
    /// Creates a runner for decorator tests with inline type definitions.
    /// </summary>
    public static GeneratorTestRunner ForDecoratorWithInlineTypes()
    {
        return new GeneratorTestRunner()
            .WithSource(InlineDecoratorDefinitions);
    }

    /// <summary>
    /// Creates a runner for interceptor tests with inline type definitions.
    /// </summary>
    public static GeneratorTestRunner ForInterceptorWithInlineTypes()
    {
        return new GeneratorTestRunner()
            .WithSource(InlineInterceptorDefinitions);
    }
}

/// <summary>
/// Flexible analyzer config options provider that accepts any key-value pairs.
/// </summary>
internal sealed class FlexibleAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly FlexibleAnalyzerConfigOptions _globalOptions;

    public FlexibleAnalyzerConfigOptionsProvider(Dictionary<string, string> options)
    {
        _globalOptions = new FlexibleAnalyzerConfigOptions(options);
    }

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
}

internal sealed class FlexibleAnalyzerConfigOptions : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _options;

    public FlexibleAnalyzerConfigOptions(Dictionary<string, string> options)
    {
        _options = options;
    }

    public override bool TryGetValue(string key, out string value)
    {
        return _options.TryGetValue(key, out value!);
    }
}
