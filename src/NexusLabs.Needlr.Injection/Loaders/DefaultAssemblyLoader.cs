using System.Reflection;

namespace NexusLabs.Needlr.Injection.Loaders;

public sealed class DefaultAssemblyLoader : IAssemblyLoader
{
    private readonly FileMatchAssemblyLoader _fileMatchAssemblyLoader;

    public DefaultAssemblyLoader()
    {
        var filePath = Assembly.GetEntryAssembly()?.Location ??
            throw new InvalidOperationException("Entry assembly location is null.");
        var entrypointPath = Path.GetFileName(filePath);
        _fileMatchAssemblyLoader = new FileMatchAssemblyLoader(
            [AppDomain.CurrentDomain.BaseDirectory],
            fileName =>
            {
                return
                    fileName.Equals(entrypointPath, StringComparison.OrdinalIgnoreCase);
            });
    }

    public IReadOnlyList<Assembly> LoadAssemblies(bool continueOnAssemblyError) =>
        _fileMatchAssemblyLoader.LoadAssemblies(continueOnAssemblyError);
}
