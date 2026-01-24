using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr;

/// <summary>
/// Options for customizing the output of <see cref="DumpExtensions.Dump(IServiceCollection, DumpOptions?)"/>.
/// </summary>
public sealed record DumpOptions
{
    /// <summary>
    /// Gets or sets an optional filter to only include services with a specific lifetime.
    /// When null, all lifetimes are included.
    /// </summary>
    public ServiceLifetime? LifetimeFilter { get; init; }

    /// <summary>
    /// Gets or sets an optional predicate to filter services by their service type.
    /// When null, all service types are included.
    /// </summary>
    public Func<Type, bool>? ServiceTypeFilter { get; init; }

    /// <summary>
    /// Gets or sets whether to group registrations by lifetime in the output.
    /// Default is false.
    /// </summary>
    public bool GroupByLifetime { get; init; }

    /// <summary>
    /// Gets the default dump options with no filtering.
    /// </summary>
    public static DumpOptions Default => new();
}
