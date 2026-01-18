using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NexusLabs.Needlr.Injection.Loaders;

/// <summary>
/// Assembly loader that uses reflection to discover the entry assembly.
/// </summary>
/// <remarks>
/// This loader is not compatible with NativeAOT or trimming. For AOT scenarios,
/// use <see cref="GeneratedAssemblyProvider"/> with the Needlr source generator instead.
/// </remarks>
[RequiresUnreferencedCode("ReflectionAssemblyLoader uses reflection to load assemblies. Use GeneratedAssemblyProvider for AOT scenarios.")]
public sealed class ReflectionAssemblyLoader : IAssemblyLoader
{
    private readonly FileMatchAssemblyLoader _fileMatchAssemblyLoader;

    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file",
        Justification = "This class is only used for non-AOT scenarios where Assembly.Location is available.")]
    public ReflectionAssemblyLoader()
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
