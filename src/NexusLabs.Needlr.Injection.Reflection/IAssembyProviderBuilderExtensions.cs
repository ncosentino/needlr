using NexusLabs.Needlr.Injection.Reflection.Loaders;
using NexusLabs.Needlr.Injection.Reflection.Sorters;
using NexusLabs.Needlr.Injection.Sorters;

using System.Reflection;

namespace NexusLabs.Needlr.Injection.Reflection;

public static class IAssembyProviderBuilderExtensions
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
    public static IAssembyProviderBuilder MatchingAssemblies(
        this IAssembyProviderBuilder builder,
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
    public static IAssembyProviderBuilder MatchingFiles(
        this IAssembyProviderBuilder builder,
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
    public static IAssembyProviderBuilder MatchingAssemblies(
        this IAssembyProviderBuilder builder,
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
    public static IAssembyProviderBuilder MatchingFiles(
        this IAssembyProviderBuilder builder,
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
    public static IAssembyProviderBuilder MatchingAssemblies(
        this IAssembyProviderBuilder builder,
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
    public static IAssembyProviderBuilder MatchingFiles(
        this IAssembyProviderBuilder builder,
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
    /// Configures the builder to sort assemblies alphabetically by their location path.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <returns>The configured assembly provider builder with alphabetical sorting enabled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static IAssembyProviderBuilder UseAlphabeticalSorting(
        this IAssembyProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseSorter(new AlphabeticalAssemblySorter());
    }

    /// <summary>
    /// Configures the builder to use a custom sorting callback for ordering assemblies.
    /// The callback receives the loaded assemblies and should return them in the desired order.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <param name="sortCallback">A function that takes a list of assemblies and returns them in the desired order.</param>
    /// <returns>The configured assembly provider builder with custom sorting enabled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="sortCallback"/> is null.</exception>
    /// <example>
    /// <code>
    /// builder.UseSortingCallback(assemblies => assemblies.OrderBy(a => a.GetName().Name));
    /// </code>
    /// </example>
    public static IAssembyProviderBuilder UseSortingCallback(
        this IAssembyProviderBuilder builder,
        Func<IReadOnlyList<Assembly>, IEnumerable<Assembly>> sortCallback)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(sortCallback);
        var sorter = new AssemblySorter(
            new ReflectionAssemblySorter(),
            sortCallback);
        return builder.UseSorter(sorter);
    }

    /// <summary>
    /// Configures the builder to sort assemblies in a specific order: libraries first, then test assemblies, then entry point executables.
    /// Test assemblies are identified by having "Tests" in their name (case-insensitive).
    /// Libraries are .dll files, and entry points are .exe files.
    /// </summary>
    /// <param name="builder">The assembly provider builder to configure.</param>
    /// <returns>The configured assembly provider builder with lib-test-entry sorting enabled.</returns>
    public static IAssembyProviderBuilder UseLibTestEntrySorting(
        this IAssembyProviderBuilder builder)
    {
        return builder.UseSorter(new LibTestEntryAssemblySorter(
            a => a.GetName().Name?.Contains("Tests", StringComparison.OrdinalIgnoreCase) == true,
            a => a.Location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase),
            a => a.Location.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)));
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
