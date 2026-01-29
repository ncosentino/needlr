using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.TypeFilterers;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Represents a Syringe that has been configured with a strategy (UsingReflection, UsingSourceGen, or UsingAutoConfiguration).
/// This type has access to all configuration extension methods and can build a service provider.
/// </summary>
/// <remarks>
/// <para>
/// ConfiguredSyringe is created by calling one of the strategy methods on <see cref="Syringe"/>:
/// </para>
/// <list type="bullet">
/// <item><c>new Syringe().UsingReflection()</c> - uses reflection-based type discovery</item>
/// <item><c>new Syringe().UsingSourceGen()</c> - uses source-generated type discovery</item>
/// <item><c>new Syringe().UsingAutoConfiguration()</c> - automatically selects best available strategy</item>
/// </list>
/// <para>
/// Once configured, use extension methods to further customize the container, then call 
/// <see cref="BuildServiceProvider(IConfiguration)"/> to create the service provider.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed record ConfiguredSyringe
{
    internal ITypeRegistrar? TypeRegistrar { get; init; }
    internal ITypeFilterer? TypeFilterer { get; init; }
    internal IPluginFactory? PluginFactory { get; init; }
    internal Func<ITypeRegistrar, ITypeFilterer, IPluginFactory, IServiceCollectionPopulator>? ServiceCollectionPopulatorFactory { get; init; }
    internal IAssemblyProvider? AssemblyProvider { get; init; }
    internal AssemblyOrdering.AssemblyOrderBuilder? AssemblyOrder { get; init; }
    internal IReadOnlyList<Assembly>? AdditionalAssemblies { get; init; }
    internal IReadOnlyList<Action<IServiceCollection>>? PostPluginRegistrationCallbacks { get; init; }
    internal VerificationOptions? VerificationOptions { get; init; }
    
    /// <summary>
    /// Factory for creating <see cref="IServiceProviderBuilder"/> instances.
    /// </summary>
    internal Func<IServiceCollectionPopulator, IAssemblyProvider, IReadOnlyList<Assembly>, IServiceProviderBuilder>? ServiceProviderBuilderFactory { get; init; }

    /// <summary>
    /// Creates a ConfiguredSyringe from a base Syringe, copying all properties.
    /// </summary>
    /// <param name="source">The source Syringe to copy from.</param>
    internal ConfiguredSyringe(Syringe source)
    {
        ArgumentNullException.ThrowIfNull(source);
        TypeRegistrar = source.TypeRegistrar;
        TypeFilterer = source.TypeFilterer;
        PluginFactory = source.PluginFactory;
        ServiceCollectionPopulatorFactory = source.ServiceCollectionPopulatorFactory;
        AssemblyProvider = source.AssemblyProvider;
        AssemblyOrder = source.AssemblyOrder;
        AdditionalAssemblies = source.AdditionalAssemblies;
        PostPluginRegistrationCallbacks = source.PostPluginRegistrationCallbacks;
        VerificationOptions = source.VerificationOptions;
        ServiceProviderBuilderFactory = source.ServiceProviderBuilderFactory;
    }

    /// <summary>
    /// Default constructor for record initialization syntax.
    /// Internal to prevent direct construction - use strategy methods like UsingReflection().
    /// </summary>
    internal ConfiguredSyringe() { }

    /// <summary>
    /// Builds a service provider with the configured settings.
    /// Automatically runs container verification based on <see cref="VerificationOptions"/>.
    /// </summary>
    /// <param name="config">The configuration to use for building the service provider.</param>
    /// <returns>The configured <see cref="IServiceProvider"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if required components (TypeRegistrar, TypeFilterer, PluginFactory, AssemblyProvider) are not configured.
    /// </exception>
    /// <exception cref="ContainerVerificationException">
    /// Thrown if verification issues are detected and the configured behavior is <see cref="VerificationBehavior.Throw"/>.
    /// </exception>
    public IServiceProvider BuildServiceProvider(
        IConfiguration config)
    {
        var typeRegistrar = GetOrCreateTypeRegistrar();
        var typeFilterer = GetOrCreateTypeFilterer();
        var pluginFactory = GetOrCreatePluginFactory();
        var serviceCollectionPopulator = GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory);
        var assemblyProvider = GetOrCreateAssemblyProvider();
        var additionalAssemblies = AdditionalAssemblies ?? [];
        var callbacks = PostPluginRegistrationCallbacks ?? [];
        var verificationOptions = VerificationOptions ?? Needlr.VerificationOptions.Default;

        // Build the list of post-plugin callbacks
        var callbacksWithExtras = new List<Action<IServiceCollection>>(callbacks);
        
        // Auto-register options from source-generated bootstrap
        if (SourceGenRegistry.TryGetOptionsRegistrar(out var optionsRegistrar) && optionsRegistrar != null)
        {
            callbacksWithExtras.Add(services => optionsRegistrar(services, config));
        }
        
        // Auto-register extensions (e.g., FluentValidation) from source-generated bootstrap
        if (SourceGenRegistry.TryGetExtensionRegistrar(out var extensionRegistrar) && extensionRegistrar != null)
        {
            callbacksWithExtras.Add(services => extensionRegistrar(services, config));
        }
        
        // Add verification as the final callback
        callbacksWithExtras.Add(services => RunVerification(services, verificationOptions));

        var serviceProviderBuilder = GetOrCreateServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies);

        return serviceProviderBuilder.Build(
            services: new ServiceCollection(),
            config: config,
            postPluginRegistrationCallbacks: callbacksWithExtras);
    }

    private static void RunVerification(IServiceCollection services, VerificationOptions options)
    {
        var issues = new List<VerificationIssue>();

        // Check for lifetime mismatches
        if (options.LifetimeMismatchBehavior != VerificationBehavior.Silent)
        {
            var mismatches = services.DetectLifetimeMismatches();
            foreach (var mismatch in mismatches)
            {
                issues.Add(new VerificationIssue(
                    Type: VerificationIssueType.LifetimeMismatch,
                    Message: $"Lifetime mismatch: {mismatch.ConsumerServiceType.Name} ({mismatch.ConsumerLifetime}) depends on {mismatch.DependencyServiceType.Name} ({mismatch.DependencyLifetime})",
                    DetailedMessage: mismatch.ToDetailedString(),
                    ConfiguredBehavior: options.LifetimeMismatchBehavior)
                {
                    InvolvedTypes = [mismatch.ConsumerServiceType, mismatch.DependencyServiceType]
                });
            }
        }

        // Process issues based on configured behavior
        var issuesByBehavior = issues.GroupBy(i => i.ConfiguredBehavior);
        
        foreach (var group in issuesByBehavior)
        {
            switch (group.Key)
            {
                case VerificationBehavior.Warn:
                    foreach (var issue in group)
                    {
                        if (options.IssueReporter is not null)
                        {
                            options.IssueReporter(issue);
                        }
                        else
                        {
                            Console.Error.WriteLine($"[Needlr Warning] {issue.Message}");
                            Console.Error.WriteLine(issue.DetailedMessage);
                            Console.Error.WriteLine();
                        }
                    }
                    break;

                case VerificationBehavior.Throw:
                    var throwableIssues = group.ToList();
                    if (throwableIssues.Count > 0)
                    {
                        throw new ContainerVerificationException(throwableIssues);
                    }
                    break;
            }
        }
    }
        
    /// <summary>
    /// Gets the configured type registrar.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no type registrar is configured. This should not happen if the syringe was created
    /// via UsingReflection(), UsingSourceGen(), or UsingAutoConfiguration().
    /// </exception>
    public ITypeRegistrar GetOrCreateTypeRegistrar()
    {
        return TypeRegistrar ?? throw new InvalidOperationException(
            "No TypeRegistrar configured. This ConfiguredSyringe was not properly initialized. " +
            "Use new Syringe().UsingSourceGen(), .UsingReflection(), or .UsingAutoConfiguration().");
    }

    /// <summary>
    /// Gets the configured type filterer or creates an empty one.
    /// </summary>
    public ITypeFilterer GetOrCreateTypeFilterer()
    {
        return TypeFilterer ?? new EmptyTypeFilterer();
    }

    /// <summary>
    /// Gets the configured plugin factory.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no plugin factory is configured. This should not happen if the syringe was created
    /// via UsingReflection(), UsingSourceGen(), or UsingAutoConfiguration().
    /// </exception>
    public IPluginFactory GetOrCreatePluginFactory()
    {
        return PluginFactory ?? throw new InvalidOperationException(
            "No PluginFactory configured. This ConfiguredSyringe was not properly initialized. " +
            "Use new Syringe().UsingSourceGen(), .UsingReflection(), or .UsingAutoConfiguration().");
    }

    /// <summary>
    /// Gets the configured service collection populator or creates a default one.
    /// </summary>
    public IServiceCollectionPopulator GetOrCreateServiceCollectionPopulator(
        ITypeRegistrar typeRegistrar,
        ITypeFilterer typeFilterer,
        IPluginFactory pluginFactory)
    {
        return ServiceCollectionPopulatorFactory?.Invoke(typeRegistrar, typeFilterer, pluginFactory)
            ?? new ServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory);
    }

    /// <summary>
    /// Gets the configured service provider builder or throws if not configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no service provider builder factory is configured. This should not happen if the syringe was created
    /// via UsingReflection(), UsingSourceGen(), or UsingAutoConfiguration().
    /// </exception>
    public IServiceProviderBuilder GetOrCreateServiceProviderBuilder(
        IServiceCollectionPopulator serviceCollectionPopulator,
        IAssemblyProvider assemblyProvider,
        IReadOnlyList<Assembly> additionalAssemblies)
    {
        return ServiceProviderBuilderFactory?.Invoke(serviceCollectionPopulator, assemblyProvider, additionalAssemblies)
            ?? throw new InvalidOperationException(
                "No ServiceProviderBuilderFactory configured. This ConfiguredSyringe was not properly initialized. " +
                "Use new Syringe().UsingSourceGen(), .UsingReflection(), or .UsingAutoConfiguration().");
    }

    /// <summary>
    /// Gets the configured assembly provider, with ordering applied if configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no assembly provider is configured. This should not happen if the syringe was created
    /// via UsingReflection(), UsingSourceGen(), or UsingAutoConfiguration().
    /// </exception>
    public IAssemblyProvider GetOrCreateAssemblyProvider()
    {
        var provider = AssemblyProvider ?? throw new InvalidOperationException(
            "No AssemblyProvider configured. This ConfiguredSyringe was not properly initialized. " +
            "Use new Syringe().UsingSourceGen(), .UsingReflection(), or .UsingAutoConfiguration().");

        // Apply ordering if configured
        if (AssemblyOrder != null)
        {
            return new OrderedAssemblyProvider(provider, AssemblyOrder);
        }

        return provider;
    }

    /// <summary>
    /// Gets the configured additional assemblies.
    /// </summary>
    public IReadOnlyList<Assembly> GetAdditionalAssemblies()
    {
        return AdditionalAssemblies ?? [];
    }

    /// <summary>
    /// Gets the configured post-plugin registration callbacks.
    /// </summary>
    public IReadOnlyList<Action<IServiceCollection>> GetPostPluginRegistrationCallbacks()
    {
        return PostPluginRegistrationCallbacks ?? [];
    }
}
