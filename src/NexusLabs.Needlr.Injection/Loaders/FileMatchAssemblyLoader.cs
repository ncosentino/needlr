using System.Reflection;

namespace NexusLabs.Needlr.Injection.Loaders;

public sealed class FileMatchAssemblyLoader : IAssemblyLoader
{
    private readonly IReadOnlyList<string> _directories;
    private readonly Predicate<string> _fileFilter;

    public FileMatchAssemblyLoader(
        IReadOnlyList<string> directories,
        Predicate<string>? fileFilter)
    {
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(fileFilter);

        _directories = directories;
        _fileFilter = fileFilter;
    }

    public IReadOnlyList<Assembly> LoadAssemblies(
        bool continueOnAssemblyError)
    {
        var sourceAssemblies = _directories
            .SelectMany(Directory.GetFiles)
            .Where(x =>
            {
                var fileName = Path.GetFileName(x);
                return _fileFilter.Invoke(fileName);
            })
            .Select(p =>
            {
                try
                {
                    return Assembly.LoadFrom(p);
                }
                catch (Exception ex)
                {
                    if (continueOnAssemblyError)
                    {
                        return null;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to load assembly from path: {p}", ex);
                    }
                }
            })
            .Where(x => x != null)
            .Cast<Assembly>()
            .ToArray();
        return sourceAssemblies;
    }
}
