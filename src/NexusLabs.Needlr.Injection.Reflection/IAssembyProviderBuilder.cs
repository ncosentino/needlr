namespace NexusLabs.Needlr.Injection.Reflection;

[DoNotAutoRegister]
public interface IAssembyProviderBuilder
{
    IAssemblyProvider Build();
    AssembyProviderBuilder UseLoader(IAssemblyLoader loader);
    AssembyProviderBuilder UseSorter(IAssemblySorter sorter);
}