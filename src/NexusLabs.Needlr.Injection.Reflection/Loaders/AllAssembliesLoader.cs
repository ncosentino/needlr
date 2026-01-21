using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection.Loaders;

/// <summary>
/// An assembly loader that loads all assemblies (.dll and .exe) from the application's base directory.
/// </summary>
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

    /// <inheritdoc />
    public IReadOnlyList<Assembly> LoadAssemblies(bool continueOnAssemblyError) =>
        _fileMatchAssemblyLoader.LoadAssemblies(continueOnAssemblyError);
}
