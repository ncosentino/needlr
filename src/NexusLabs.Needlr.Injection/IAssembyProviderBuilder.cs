namespace NexusLabs.Needlr.Injection;

[DoNotAutoRegister]
public interface IAssembyProviderBuilder
{
    IAssemblyProvider Build();
    AssembyProviderBuilder UseLoader(IAssemblyLoader loader);
    AssembyProviderBuilder UseSorter(IAssemblySorter sorter);
}