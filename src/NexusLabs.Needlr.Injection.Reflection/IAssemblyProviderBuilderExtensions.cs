using NexusLabs.Needlr.Injection.Reflection.Loaders;

namespace NexusLabs.Needlr.Injection.Reflection;

/// <summary>
/// Extension methods for <see cref="IAssemblyProviderBuilder"/> providing fluent configuration of assembly loading.
/// For assembly ordering, use <c>SyringeExtensions.OrderAssemblies</c> instead.
/// </summary>
public static class IAssemblyProviderBuilderExtensions
{
    /// <summary>
    /// Configures the builder to load assemblies from the application's base directory that match the specified filter criteria.
    /// Only files with .dll or .exe extensions will be considered.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <param name="fileFilter">A predicate that determines which assembly files to include based on their file path.</param>
    /// <returns>The configured assembly provider builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="fileFilter"/> is null.</exception>
    /// <example>
    /// <code>
    /// builder.MatchingAssemblies(path => path.Contains("MyProject"));
    /// </code>
    /// </example>
    public static IAssemblyProviderBuilder MatchingAssemblies(
        this IAssemblyProviderBuilder builder,
        Predicate<string> fileFilter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(fileFilter);

        return builder.MatchingFiles(
            x => IsAssemblyFilePath(x) && fileFilter(x));
    }

    /// <summary>
    /// Configures the builder to load files from the application's base directory that match the specified filter criteria.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <param name="fileFilter">A predicate that determines which files to include based on their file path.</param>
    /// <returns>The configured assembly provider builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="fileFilter"/> is null.</exception>
    public static IAssemblyProviderBuilder MatchingFiles(
        this IAssemblyProviderBuilder builder,
        Predicate<string> fileFilter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(fileFilter);

        return builder.MatchingFiles(
            AppDomain.CurrentDomain.BaseDirectory,
            fileFilter);
    }

    /// <summary>
    /// Configures the builder to load assemblies from the specified directory that match the filter criteria.
    /// Only files with .dll or .exe extensions will be considered.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <param name="directory">The directory path to scan for assembly files.</param>
    /// <param name="fileFilter">A predicate that determines which assembly files to include based on their file path.</param>
    /// <returns>The configured assembly provider builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="directory"/>, or <paramref name="fileFilter"/> is null.</exception>
    public static IAssemblyProviderBuilder MatchingAssemblies(
        this IAssemblyProviderBuilder builder,
        string directory,
        Predicate<string> fileFilter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(fileFilter);

        return builder.MatchingFiles(
            directory,
            x => IsAssemblyFilePath(x) && fileFilter(x));
    }

    /// <summary>
    /// Configures the builder to load files from the specified directory that match the filter criteria.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <param name="directory">The directory path to scan for files.</param>
    /// <param name="fileFilter">A predicate that determines which files to include based on their file path.</param>
    /// <returns>The configured assembly provider builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="directory"/>, or <paramref name="fileFilter"/> is null.</exception>
    public static IAssemblyProviderBuilder MatchingFiles(
        this IAssemblyProviderBuilder builder,
        string directory,
        Predicate<string> fileFilter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(fileFilter);

        return builder.MatchingFiles(
            [directory],
            fileFilter);
    }

    /// <summary>
    /// Configures the builder to load assemblies from the specified directories that match the filter criteria.
    /// Only files with .dll or .exe extensions will be considered.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <param name="directories">The collection of directory paths to scan for assembly files.</param>
    /// <param name="fileFilter">A predicate that determines which assembly files to include based on their file path.</param>
    /// <returns>The configured assembly provider builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="directories"/>, or <paramref name="fileFilter"/> is null.</exception>
    public static IAssemblyProviderBuilder MatchingAssemblies(
        this IAssemblyProviderBuilder builder,
        IReadOnlyList<string> directories,
        Predicate<string> fileFilter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(fileFilter);

        return builder.MatchingFiles(
            directories,
            x => IsAssemblyFilePath(x) && fileFilter(x));
    }

    /// <summary>
    /// Configures the builder to load files from the specified directories that match the filter criteria.
    /// This is the core method that sets up the FileMatchAssemblyLoader.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <param name="directories">The collection of directory paths to scan for files.</param>
    /// <param name="fileFilter">A predicate that determines which files to include based on their file path.</param>
    /// <returns>The configured assembly provider builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="directories"/>, or <paramref name="fileFilter"/> is null.</exception>
    public static IAssemblyProviderBuilder MatchingFiles(
        this IAssemblyProviderBuilder builder,
        IReadOnlyList<string> directories,
        Predicate<string> fileFilter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(directories);
        ArgumentNullException.ThrowIfNull(fileFilter);

        return builder.UseLoader(new FileMatchAssemblyLoader(
            directories,
            fileFilter));
    }

    /// <summary>
    /// Determines whether the specified file path represents an assembly file (.dll or .exe).
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file path ends with .dll or .exe (case-insensitive); otherwise, false.</returns>
    private static bool IsAssemblyFilePath(string filePath) =>
        filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
        filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
}
