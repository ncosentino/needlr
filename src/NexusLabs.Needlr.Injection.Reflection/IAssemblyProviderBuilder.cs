namespace NexusLabs.Needlr.Injection.Reflection;

[DoNotAutoRegister]
public interface IAssemblyProviderBuilder
{
    IAssemblyProvider Build();
    AssemblyProviderBuilder UseLoader(IAssemblyLoader loader);
    AssemblyProviderBuilder UseSorter(IAssemblySorter sorter);
}