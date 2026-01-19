using NexusLabs.Needlr.Injection.Reflection.Loaders;
using NexusLabs.Needlr.Injection.Reflection.Sorters;

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
    private IAssemblySorter _assemblySorter;

    public AssemblyProviderBuilder()
    {
        _assemblyLoader = new ReflectionAssemblyLoader();
        _assemblySorter = new ReflectionAssemblySorter();
    }

    public AssemblyProviderBuilder UseLoader(IAssemblyLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _assemblyLoader = loader;
        return this;
    }

    public AssemblyProviderBuilder UseSorter(IAssemblySorter sorter)
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
