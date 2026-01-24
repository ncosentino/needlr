using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection.TypeFilterers;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// The base container for configuring Needlr service discovery.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Syringe"/> is a starting point that must be configured with a strategy before building.
/// Use one of these approaches to get a <see cref="ConfiguredSyringe"/> that can build a service provider:
/// </para>
/// <list type="bullet">
/// <item>Reference <c>NexusLabs.Needlr.Injection.SourceGen</c> and call <c>.UsingSourceGen()</c></item>
/// <item>Reference <c>NexusLabs.Needlr.Injection.Reflection</c> and call <c>.UsingReflection()</c></item>
/// <item>Reference <c>NexusLabs.Needlr.Injection.Bundle</c> and call <c>.UsingAutoConfiguration()</c></item>
/// </list>
/// <para>
/// The strategy methods return a <see cref="ConfiguredSyringe"/> which has the <c>BuildServiceProvider()</c> method.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using reflection
/// var provider = new Syringe()
///     .UsingReflection()
///     .BuildServiceProvider();
/// 
/// // Using source generation (AOT-compatible)
/// var provider = new Syringe()
///     .UsingSourceGen()
///     .BuildServiceProvider();
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed record Syringe
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
}