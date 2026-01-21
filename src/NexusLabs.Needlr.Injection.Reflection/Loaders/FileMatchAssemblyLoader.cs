using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection.Loaders;

/// <summary>
/// Assembly loader that loads assemblies from disk based on file name matching.
/// </summary>
/// <remarks>
/// This loader is not compatible with NativeAOT or trimming. For AOT scenarios,
/// use GeneratedAssemblyProvider from NexusLabs.Needlr.Injection.SourceGen instead.
/// </remarks>
[RequiresUnreferencedCode("FileMatchAssemblyLoader uses Assembly.LoadFrom which is not AOT-compatible. Use GeneratedAssemblyProvider for AOT scenarios.")]
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

    /// <inheritdoc />
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
