using NexusLabs.Needlr.Injection.Reflection.Loaders;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection;

/// <summary>
/// Builder for creating assembly providers using reflection-based loaders.
/// </summary>
/// <remarks>
/// <para>
/// This builder uses reflection-based assembly loading and is not compatible with NativeAOT or trimming.
/// For AOT scenarios, use GeneratedAssemblyProvider from NexusLabs.Needlr.Injection.SourceGen instead.
/// </para>
/// <para>
/// For assembly ordering, use <c>SyringeExtensions.OrderAssemblies</c> after configuring the Syringe.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("AssemblyProviderBuilder uses reflection-based assembly loading. Use GeneratedAssemblyProvider for AOT scenarios.")]
public sealed class AssemblyProviderBuilder : IAssemblyProviderBuilder
{
    private IAssemblyLoader _assemblyLoader;

    public AssemblyProviderBuilder()
    {
        _assemblyLoader = new ReflectionAssemblyLoader();
    }

    public AssemblyProviderBuilder UseLoader(IAssemblyLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _assemblyLoader = loader;
        return this;
    }

    public IAssemblyProvider Build()
    {
        return new AssemblyProvider(_assemblyLoader);
    }

    private sealed class AssemblyProvider : IAssemblyProvider
    {
        private readonly Lazy<IReadOnlyList<Assembly>> _lazyAssemblies;

        public AssemblyProvider(IAssemblyLoader assemblyLoader)
        {
            ArgumentNullException.ThrowIfNull(assemblyLoader);

            _lazyAssemblies = new(() =>
            {
                return assemblyLoader.LoadAssemblies(continueOnAssemblyError: true);
            });
        }

        /// <inheritdoc />
        public IReadOnlyList<Assembly> GetCandidateAssemblies()
            => _lazyAssemblies.Value;
    }
}
