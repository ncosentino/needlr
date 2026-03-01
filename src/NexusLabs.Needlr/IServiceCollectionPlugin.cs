using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr;

/// <summary>
/// Defines a plugin that participates in Needlr's <see cref="IServiceCollection"/> configuration pipeline.
/// Implement this interface to encapsulate a cohesive block of service registrations that can be
/// discovered and applied automatically during startup.
/// </summary>
/// <remarks>
/// <para>
/// Plugins are discovered and invoked by the Needlr startup pipeline
/// as part of its fluent setup chain.
/// The <see cref="DoNotAutoRegisterAttribute"/> and <see cref="DoNotInjectAttribute"/> on this interface
/// prevent Needlr from mistakenly registering it as a concrete service.
/// </para>
/// <para>
/// Create a class that implements <see cref="IServiceCollectionPlugin"/> and place your
/// registration logic in <see cref="Configure"/>. Needlr will call <see cref="Configure"/> once
/// during the startup assembly scan.
/// </para>
/// <para>
/// Do <b>not</b> apply <see cref="DoNotAutoRegisterAttribute"/> directly to an implementing class.
/// This interface already carries the attribute to prevent DI registration of the interface itself;
/// adding it to the class too is redundant and was historically a silent bug that suppressed plugin
/// discovery. Analyzer NDLRCOR016 will warn you if you do this.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class InfrastructurePlugin : IServiceCollectionPlugin
/// {
///     public void Configure(ServiceCollectionPluginOptions options)
///     {
///         options.Services.AddSingleton&lt;IConnectionFactory, SqlConnectionFactory&gt;();
///         options.Services.AddScoped&lt;IUnitOfWork, SqlUnitOfWork&gt;();
///     }
/// }
/// </code>
/// </example>
[DoNotAutoRegister]
[DoNotInject]
public interface IServiceCollectionPlugin
{
    /// <summary>
    /// Configures the <see cref="IServiceCollection"/> instance.
    /// </summary>
    void Configure(ServiceCollectionPluginOptions options);
}