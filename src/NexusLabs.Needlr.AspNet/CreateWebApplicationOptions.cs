using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Represents options for creating a web application with logging configuration.
/// </summary>
/// <param name="Options">
/// The web application options to use when creating the application.
/// </param>
/// <param name="PostPluginRegistrationCallbacks">
/// The callbacks to execute after plugin registration, allowing for additional service configuration.
/// </param>
/// <param name="Logger">
/// The logger instance to use for logging during application creation.
/// </param>
public sealed record CreateWebApplicationOptions(
    WebApplicationOptions Options,
    IReadOnlyList<Action<IServiceCollection>> PostPluginRegistrationCallbacks,
    ILogger Logger)
{
    private static readonly CreateWebApplicationOptions _defaultOptions = new(options: new());

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWebApplicationOptions"/> 
    /// record with a <see cref="NullLogger"/>.
    /// </summary>
    /// <param name="options">The web application options to use.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is null.
    /// </exception>
    public CreateWebApplicationOptions(
        WebApplicationOptions options)
        : this(options, PostPluginRegistrationCallbacks: [], NullLogger.Instance)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    /// <summary>
    /// Gets the default instance of <see cref="CreateWebApplicationOptions"/> with empty 
    /// <see cref="WebApplicationOptions"/> and a <see cref="NullLogger"/>.
    /// </summary>
    public static CreateWebApplicationOptions Default => _defaultOptions;
}
