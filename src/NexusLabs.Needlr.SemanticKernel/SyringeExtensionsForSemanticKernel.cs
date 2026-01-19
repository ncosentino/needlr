using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;

using System.Diagnostics.CodeAnalysis;

namespace NexusLabs.Needlr.SemanticKernel;

/// <summary>
/// Extension methods for <see cref="Syringe"/> that enable registering
/// Semantic Kernel infrastructure (namely <see cref="IKernelFactory"/>)
/// as part of the Needlr build pipeline.
/// </summary>
/// <remarks>
/// <para>
/// These helpers defer service registration using the Syringe
/// post-plugin registration callback so that plugin discovery and
/// registration are completed before the Semantic Kernel factory is added.
/// </para>
/// <para>
/// <strong>Note:</strong> Microsoft.SemanticKernel internally uses reflection to discover
/// <c>[KernelFunction]</c> methods and create plugins. This integration therefore requires
/// reflection and is not fully AOT-compatible. For AOT scenarios, consider registering
/// kernel functions explicitly.
/// </para>
/// </remarks>
public static class SyringeExtensionsForSemanticKernel
{
    /// <summary>
    /// Registers an <see cref="IKernelFactory"/> built via a
    /// <see cref="SemanticKernelSyringe"/> instance.
    /// </summary>
    /// <param name="syringe">
    /// The <see cref="Syringe"/> to augment with the registration.
    /// </param>
    /// <returns>
    /// A new <see cref="Syringe"/> instance containing the registration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="syringe"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Use this overload when you do not need to configure Semantic Kernel.
    /// </remarks>
    /// <example>
    /// <code>
    /// var syringe = new Syringe().UsingSemanticKernel();
    /// </code>
    /// </example>
    [RequiresUnreferencedCode("Semantic Kernel uses reflection to discover [KernelFunction] methods.")]
    [RequiresDynamicCode("Semantic Kernel uses reflection APIs that require dynamic code generation.")]
    public static Syringe UsingSemanticKernel(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.UsingSemanticKernel(syringe => syringe);
    }

    /// <summary>
    /// Registers an <see cref="IKernelFactory"/> built via a configurable
    /// <see cref="SemanticKernelSyringe"/> instance.
    /// </summary>
    /// <param name="syringe">
    /// The <see cref="Syringe"/> to augment with the registration.
    /// </param>
    /// <param name="configure">
    /// A delegate that receives a pre-initialized <see cref="SemanticKernelSyringe"/>
    /// (with its <see cref="SemanticKernelSyringe.ServiceProvider"/> set) and
    /// returns the configured instance used to build the kernel factory.
    /// </param>
    /// <returns>
    /// A new <see cref="Syringe"/> instance containing the registration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="syringe"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Use this overload when your Semantic Kernel configuration needs access to
    /// services from the container during configuration.
    /// </remarks>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingSemanticKernel(sk =&gt; sk with
    ///     {
    ///         // e.g., add plugins or configure options requiring the provider
    ///         PluginTypes = new() { typeof(MyPlugin) },
    ///     });
    /// </code>
    /// </example>
    [RequiresUnreferencedCode("Semantic Kernel uses reflection to discover [KernelFunction] methods.")]
    [RequiresDynamicCode("Semantic Kernel uses reflection APIs that require dynamic code generation.")]
    public static Syringe UsingSemanticKernel(
        this Syringe syringe,
        Func<SemanticKernelSyringe, SemanticKernelSyringe> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            services.AddSingleton<IKernelFactory>(provider =>
            {
                SemanticKernelSyringe syringe = new()
                {
                    ServiceProvider = provider,
                };
                syringe = configure.Invoke(syringe);
                var kernelFactory = syringe.BuildKernelFactory();
                return kernelFactory;
            });
        });
    }

    /// <summary>
    /// Registers an <see cref="IKernelFactory"/> built via a <see cref="SemanticKernelSyringe"/>
    /// created by the supplied delegate.
    /// </summary>
    /// <param name="syringe">
    /// The <see cref="Syringe"/> to augment with the registration.
    /// </param>
    /// <param name="configure">
    /// A factory that creates a fully-configured <see cref="SemanticKernelSyringe"/>
    /// used to build the kernel factory. This is useful when configuration does
    /// not need the service provider.
    /// </param>
    /// <returns>
    /// A new <see cref="Syringe"/> instance containing the registration.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="syringe"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingSemanticKernel(() =&gt; new SemanticKernelSyringe
    ///     {
    ///         // Initialize without requiring the service provider
    ///         PluginTypes = new() { typeof(MyPlugin) },
    ///     });
    /// </code>
    /// </example>
    [RequiresUnreferencedCode("Semantic Kernel uses reflection to discover [KernelFunction] methods.")]
    [RequiresDynamicCode("Semantic Kernel uses reflection APIs that require dynamic code generation.")]
    public static Syringe UsingSemanticKernel(
        this Syringe syringe,
        Func<SemanticKernelSyringe> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            services.AddSingleton<IKernelFactory>(provider =>
            {
                var syringe = configure.Invoke();
                var kernelFactory = syringe.BuildKernelFactory();
                return kernelFactory;
            });
        });
    }
}
