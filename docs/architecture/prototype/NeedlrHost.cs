using Microsoft.Extensions.Hosting;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Entry point for creating Needlr-powered hosts that are compatible with Microsoft.Extensions.Hosting.
/// This demonstrates how Needlr can provide familiar APIs while maintaining its automatic registration benefits.
/// </summary>
public static class NeedlrHost
{
    /// <summary>
    /// Creates a host builder with Needlr's automatic registration capabilities using familiar Microsoft.Extensions.Hosting patterns.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>A configured IHostBuilder</returns>
    /// <example>
    /// <code>
    /// // This looks and feels like Host.CreateDefaultBuilder but uses Needlr under the hood
    /// var host = NeedlrHost.CreateDefaultBuilder(args)
    ///     .ConfigureNeedlr(syringe => syringe
    ///         .UsingScrutorTypeRegistrar()
    ///         .UsingAssemblyProvider(builder => builder
    ///             .MatchingAssemblies(x => x.Contains("MyApp"))
    ///             .Build()))
    ///     .ConfigureServices((context, services) =>
    ///     {
    ///         // Standard service registration still works
    ///         services.AddSingleton<IMyManualService, MyManualService>();
    ///         services.AddHostedService<MyBackgroundService>();
    ///     })
    ///     .Build();
    /// 
    /// await host.RunAsync();
    /// </code>
    /// </example>
    public static IHostBuilder CreateDefaultBuilder(string[]? args = null)
    {
        return new NeedlrHostBuilder()
            .UseNeedlrDefaults()
            .UseCommandLineArguments(args);
    }

    /// <summary>
    /// Creates a host builder with a pre-configured Syringe.
    /// Useful when you want to configure Needlr first, then add hosting-specific configuration.
    /// </summary>
    /// <param name="syringe">The pre-configured Syringe to use</param>
    /// <param name="args">Command line arguments</param>
    /// <returns>A configured IHostBuilder</returns>
    /// <example>
    /// <code>
    /// // Configure Needlr first
    /// var syringe = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .UsingAssemblyProvider(builder => builder
    ///         .MatchingAssemblies(x => x.Contains("MyApp"))
    ///         .Build());
    /// 
    /// // Then create a host with it
    /// var host = NeedlrHost.CreateBuilder(syringe, args)
    ///     .ConfigureServices((context, services) =>
    ///     {
    ///         services.AddHostedService<MyBackgroundService>();
    ///     })
    ///     .Build();
    /// </code>
    /// </example>
    public static IHostBuilder CreateBuilder(Syringe syringe, string[]? args = null)
    {
        return new NeedlrHostBuilder(syringe)
            .UseCommandLineArguments(args);
    }
}

/// <summary>
/// Extension methods for NeedlrHostBuilder to provide Needlr-specific configuration.
/// </summary>
public static class NeedlrHostBuilderExtensions
{
    /// <summary>
    /// Configures the underlying Syringe used by the host builder.
    /// This allows you to set up Needlr-specific features like type registrars and assembly providers.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure</param>
    /// <param name="configure">Configuration action for the Syringe</param>
    /// <returns>The configured host builder</returns>
    /// <example>
    /// <code>
    /// var host = NeedlrHost.CreateDefaultBuilder(args)
    ///     .ConfigureNeedlr(syringe => syringe
    ///         .UsingScrutorTypeRegistrar()
    ///         .UsingAssemblyProvider(builder => builder
    ///             .MatchingAssemblies(x => x.Contains("MyApp"))
    ///             .Build())
    ///         .UsingDefaultTypeFilterer())
    ///     .Build();
    /// </code>
    /// </example>
    public static IHostBuilder ConfigureNeedlr(this IHostBuilder hostBuilder, Action<Syringe> configure)
    {
        if (hostBuilder is NeedlrHostBuilder needlrBuilder)
        {
            return needlrBuilder.ConfigureNeedlr(configure);
        }
        
        throw new InvalidOperationException("ConfigureNeedlr can only be used with NeedlrHostBuilder instances.");
    }
}