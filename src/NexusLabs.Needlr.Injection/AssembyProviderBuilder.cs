using NexusLabs.Needlr.Injection.Loaders;
using NexusLabs.Needlr.Injection.Sorters;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

public sealed class AssembyProviderBuilder : IAssembyProviderBuilder
{
    private IAssemblyLoader _assemblyLoader;
    private IAssemblySorter _assemblySorter;

    public AssembyProviderBuilder()
    {
        _assemblyLoader = new DefaultAssemblyLoader();
        _assemblySorter = new DefaultAssemblySorter();
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
