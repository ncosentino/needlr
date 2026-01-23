using NexusLabs.Needlr.Injection.AssemblyOrdering;
using NexusLabs.Needlr.Injection.Reflection.Loaders;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection;

/// <summary>
/// Builder for creating assembly providers using reflection-based loaders.
/// </summary>
/// <remarks>
/// This builder uses reflection-based assembly loading and is not compatible with NativeAOT or trimming.
/// For AOT scenarios, use GeneratedAssemblyProvider from NexusLabs.Needlr.Injection.SourceGen instead.
/// </remarks>
[RequiresUnreferencedCode("AssemblyProviderBuilder uses reflection-based assembly loading. Use GeneratedAssemblyProvider for AOT scenarios.")]
public sealed class AssemblyProviderBuilder : IAssemblyProviderBuilder
{
    private IAssemblyLoader _assemblyLoader;
    private AssemblyOrderBuilder? _assemblyOrder;

    public AssemblyProviderBuilder()
    {
        _assemblyLoader = new ReflectionAssemblyLoader();
        _assemblyOrder = null; // No ordering by default - use discovery order
    }

    public AssemblyProviderBuilder UseLoader(IAssemblyLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _assemblyLoader = loader;
        return this;
    }

    /// <inheritdoc />
    public AssemblyProviderBuilder OrderAssemblies(Action<AssemblyOrderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new AssemblyOrderBuilder();
        configure(builder);
        _assemblyOrder = builder;
        return this;
    }

    /// <summary>
    /// Configures assembly ordering using a pre-built order builder.
    /// </summary>
    /// <param name="orderBuilder">The pre-configured order builder.</param>
    /// <returns>The builder for chaining.</returns>
    public AssemblyProviderBuilder OrderAssemblies(AssemblyOrderBuilder orderBuilder)
    {
        ArgumentNullException.ThrowIfNull(orderBuilder);
        _assemblyOrder = orderBuilder;
        return this;
    }

    public IAssemblyProvider Build()
    {
        return new AssemblyProvider(
            _assemblyLoader,
            _assemblyOrder);
    }

    private sealed class AssemblyProvider : IAssemblyProvider
    {
        private readonly Lazy<IReadOnlyList<Assembly>> _lazyAssemblies;

        public AssemblyProvider(
            IAssemblyLoader assemblyLoader,
            AssemblyOrderBuilder? assemblyOrder)
        {
            ArgumentNullException.ThrowIfNull(assemblyLoader);

            _lazyAssemblies = new(() =>
            {
                var assemblies = assemblyLoader.LoadAssemblies(continueOnAssemblyError: true);
                
                if (assemblyOrder != null)
                {
                    return assemblyOrder.Sort(assemblies);
                }

                return assemblies;
            });
        }

        /// <inheritdoc />
        public IReadOnlyList<Assembly> GetCandidateAssemblies()
            => _lazyAssemblies.Value;
    }
}
