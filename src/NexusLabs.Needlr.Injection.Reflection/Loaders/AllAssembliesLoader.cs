using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection.Loaders;

public sealed class AllAssembliesLoader : IAssemblyLoader
{
    private readonly FileMatchAssemblyLoader _fileMatchAssemblyLoader;

    public AllAssembliesLoader()
    {
        _fileMatchAssemblyLoader = new FileMatchAssemblyLoader(
            [AppDomain.CurrentDomain.BaseDirectory],
            fileName =>
            {
                return
                    fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            });
    }

    public IReadOnlyList<Assembly> LoadAssemblies(bool continueOnAssemblyError) =>
        _fileMatchAssemblyLoader.LoadAssemblies(continueOnAssemblyError);
}
