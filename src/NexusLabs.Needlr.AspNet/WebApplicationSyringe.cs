using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Provides a fluent API for configuring and building web applications using Needlr.
/// Wraps a base Syringe with additional web application functionality.
/// </summary>
/// <example>
/// Creating and configuring a WebApplicationSyringe:
/// <code>
/// // Method 1: Transition from base Syringe
/// var webAppSyringe = new Syringe()
///     .UsingScrutorTypeRegistrar()
///     .UsingDefaultTypeFilterer()
///     .ForWebApplication();
/// 
/// // Method 2: Direct instantiation with base Syringe
/// var baseSyringe = new Syringe().UsingScrutorTypeRegistrar();
/// var webAppSyringe = new WebApplicationSyringe(baseSyringe);
/// 
/// // Method 3: Default constructor
/// var webAppSyringe = new WebApplicationSyringe();
/// 
/// // Build and run the web application
/// var webApp = webAppSyringe
///     .UsingOptions(() => CreateWebApplicationOptions.Default.UsingCliArgs(args))
///     .BuildWebApplication();
/// 
/// await webApp.RunAsync();
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed record WebApplicationSyringe
{
    internal Syringe BaseSyringe { get; init; } = new();
    internal Func<CreateWebApplicationOptions>? OptionsFactory { get; init; }
    internal Func<IServiceProviderBuilder, IServiceCollectionPopulator, IWebApplicationFactory>? WebApplicationFactoryCreator { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebApplicationSyringe"/> class.
    /// </summary>
    /// <param name="baseSyringe">The base syringe to wrap.</param>
    /// <example>
    /// <code>
    /// var baseSyringe = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .UsingDefaultTypeFilterer();
    /// 
    /// var webAppSyringe = new WebApplicationSyringe(baseSyringe);
    /// </code>
    /// </example>
    public WebApplicationSyringe(Syringe baseSyringe)
    {
        ArgumentNullException.ThrowIfNull(baseSyringe);
        BaseSyringe = baseSyringe;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebApplicationSyringe"/> class with default settings.
    /// </summary>
    /// <example>
    /// <code>
    /// var webAppSyringe = new WebApplicationSyringe();
    /// var webApp = webAppSyringe.BuildWebApplication();
    /// </code>
    /// </example>
    public WebApplicationSyringe()
    {
    }

    /// <summary>
    /// Builds a web application with the configured settings.
    /// </summary>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    /// <example>
    /// <code>
    /// var webApplication = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .ForWebApplication()
    ///     .UsingOptions(() => CreateWebApplicationOptions.Default
    ///         .UsingCliArgs(args)
    ///         .UsingApplicationName("My Web App"))
    ///     .BuildWebApplication();
    /// 
    /// // Configure middleware and endpoints
    /// webApplication.MapGet("/", () => "Hello World!");
    /// 
    /// await webApplication.RunAsync();
    /// </code>
    /// </example>
    public WebApplication BuildWebApplication()
    {
        var typeRegistrar = BaseSyringe.GetOrCreateTypeRegistrar();
        var typeFilterer = BaseSyringe.GetOrCreateTypeFilterer();
        var serviceCollectionPopulator = BaseSyringe.GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer);
        var assemblyProvider = BaseSyringe.GetOrCreateAssemblyProvider();
        var additionalAssemblies = BaseSyringe.GetAdditionalAssemblies();

        var serviceProviderBuilder = new ServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies);

        var webApplicationFactory = GetOrCreateWebApplicationFactory(serviceProviderBuilder, serviceCollectionPopulator);
        var options = GetOrCreateOptions();

        return webApplicationFactory.Create(options, () => WebApplication.CreateBuilder());
    }

    /// <summary>
    /// Builds a service provider with the configured settings.
    /// </summary>
    /// <param name="config">The configuration to use for building the service provider.</param>
    /// <returns>The configured <see cref="IServiceProvider"/>.</returns>
    /// <example>
    /// <code>
    /// var config = new ConfigurationBuilder()
    ///     .AddJsonFile("appsettings.json")
    ///     .Build();
    /// 
    /// var serviceProvider = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .ForWebApplication()
    ///     .BuildServiceProvider(config);
    /// 
    /// var myService = serviceProvider.GetRequiredService&lt;IMyService&gt;();
    /// </code>
    /// </example>
    public IServiceProvider BuildServiceProvider(IConfiguration config)
    {
        return BaseSyringe.BuildServiceProvider(config);
    }

    private IWebApplicationFactory GetOrCreateWebApplicationFactory(
        IServiceProviderBuilder serviceProviderBuilder, 
        IServiceCollectionPopulator serviceCollectionPopulator)
    {
        return WebApplicationFactoryCreator?.Invoke(serviceProviderBuilder, serviceCollectionPopulator)
            ?? new WebApplicationFactory(serviceProviderBuilder, serviceCollectionPopulator);
    }

    private CreateWebApplicationOptions GetOrCreateOptions()
    {
        return OptionsFactory?.Invoke() ?? CreateWebApplicationOptions.Default;
    }
}