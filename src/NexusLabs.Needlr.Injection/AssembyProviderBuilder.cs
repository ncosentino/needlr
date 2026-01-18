using NexusLabs.Needlr.Injection.Loaders;
using NexusLabs.Needlr.Injection.Sorters;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Builder for creating assembly providers using reflection-based loaders.
/// </summary>
/// <remarks>
/// This builder uses reflection-based assembly loading and is not compatible with NativeAOT or trimming.
/// For AOT scenarios, use <see cref="Loaders.GeneratedAssemblyProvider"/> with the Needlr source generator instead.
/// </remarks>
[RequiresUnreferencedCode("AssembyProviderBuilder uses reflection-based assembly loading. Use GeneratedAssemblyProvider for AOT scenarios.")]
public sealed class AssembyProviderBuilder : IAssembyProviderBuilder
{
    private IAssemblyLoader _assemblyLoader;
    private IAssemblySorter _assemblySorter;

    public AssembyProviderBuilder()
    {
        _assemblyLoader = new ReflectionAssemblyLoader();
        _assemblySorter = new ReflectionAssemblySorter();
    }

    public AssembyProviderBuilder UseLoader(IAssemblyLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _assemblyLoader = loader;
        return this;
    }

    public AssembyProviderBuilder UseSorter(IAssemblySorter sorter)
    {
        ArgumentNullException.ThrowIfNull(sorter);
        _assemblySorter = sorter;
        return this;
    }

    public IAssemblyProvider Build()
    {
        return new AssemblyProvider(
            _assemblyLoader,
            _assemblySorter);
    }

    private sealed class AssemblyProvider : IAssemblyProvider
    {
        private readonly Lazy<IReadOnlyList<Assembly>> _lazyAssemblies;

        public AssemblyProvider(
            IAssemblyLoader assemblyLoader,
            IAssemblySorter assemblySorter)
        {
            ArgumentNullException.ThrowIfNull(assemblyLoader);
            ArgumentNullException.ThrowIfNull(assemblySorter);

            _lazyAssemblies = new(() =>
            {
                var assemblies = assemblyLoader.LoadAssemblies(continueOnAssemblyError: true);
                var sortedAssemblies = assemblySorter.Sort(assemblies).ToArray();
                return sortedAssemblies;
            });
        }

        public IReadOnlyList<Assembly> GetCandidateAssemblies()
            => _lazyAssemblies.Value;
    }
}
