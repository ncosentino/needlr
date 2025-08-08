using NexusLabs.Needlr.Injection.Loaders;
using NexusLabs.Needlr.Injection.Sorters;

using System.Reflection;

namespace NexusLabs.Needlr.Injection;

public static class IAssembyProviderBuilderExtensions
{
    public static IAssembyProviderBuilder MatchingAssemblies(
        this IAssembyProviderBuilder builder,
        Predicate<string> fileFilter)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(fileFilter);

        return builder.MatchingFiles(
            x => IsAssemblyFilePath(x) && fileFilter(x));
    }

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

    public static IAssembyProviderBuilder UseAlphabeticalSorting(
        this IAssembyProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseSorter(new AlphabeticalAssemblySorter());
    }

    public static IAssembyProviderBuilder UseSortingCallback(
        this IAssembyProviderBuilder builder,
        Func<IReadOnlyList<Assembly>, IEnumerable<Assembly>> sortCallback)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(sortCallback);
        var sorter = new AssemblySorter(
            new DefaultAssemblySorter(),
            sortCallback);
        return builder.UseSorter(sorter);
    }

    public static IAssembyProviderBuilder UseLibTestEntrySorting(
        this IAssembyProviderBuilder builder)
    {
        return builder.UseSorter(new LibTestEntryAssemblySorter(
            a => a.GetName().Name?.Contains("Tests", StringComparison.OrdinalIgnoreCase) == true,
            a => a.Location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase),
            a => a.Location.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsAssemblyFilePath(string filePath) =>
        filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
        filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
}
