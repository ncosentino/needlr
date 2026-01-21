using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Represents options for creating a host application with logging configuration.
/// </summary>
/// <param name="Settings">
/// The host application builder settings to use when creating the host.
/// </param>
/// <param name="PostPluginRegistrationCallbacks">
/// The callbacks to execute after plugin registration, allowing for additional service configuration.
/// </param>
/// <param name="Logger">
/// The logger instance to use for logging during host creation.
/// </param>
public sealed record CreateHostOptions(
    HostApplicationBuilderSettings Settings,
    IReadOnlyList<Action<IServiceCollection>> PostPluginRegistrationCallbacks,
    ILogger Logger)
{
    private static readonly CreateHostOptions _defaultOptions = new(settings: new());

    /// <summary>
    /// Callbacks to execute before plugin registration to allow configuring the service collection.
    /// </summary>
    public IReadOnlyList<Action<IServiceCollection>> PrePluginRegistrationCallbacks { get; init; } = Array.Empty<Action<IServiceCollection>>();

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateHostOptions"/> 
    /// record with a <see cref="NullLogger"/>.
    /// </summary>
    /// <param name="settings">The host application builder settings to use.</param>
    public CreateHostOptions(HostApplicationBuilderSettings? settings = null)
        : this(settings ?? new(), PostPluginRegistrationCallbacks: [], NullLogger.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance with pre-plugin registration callbacks.
    /// </summary>
    /// <param name="settings">The host application builder settings to use.</param>
    /// <param name="prePluginRegistrationCallbacks">Callbacks to execute before plugin registration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prePluginRegistrationCallbacks"/> is null.</exception>
    public CreateHostOptions(
        HostApplicationBuilderSettings settings,
        IEnumerable<Action<IServiceCollection>> prePluginRegistrationCallbacks)
        : this(settings)
    {
        ArgumentNullException.ThrowIfNull(prePluginRegistrationCallbacks);
        PrePluginRegistrationCallbacks = prePluginRegistrationCallbacks.ToArray();
    }

    /// <summary>
    /// Initializes a new instance with a post-plugin registration callback.
    /// </summary>
    /// <param name="settings">The host application builder settings to use.</param>
    /// <param name="postPluginRegistrationCallback">
    /// The callback to execute after plugin registration, allowing for additional service configuration.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="postPluginRegistrationCallback"/> is null.
    /// </exception>
    public CreateHostOptions(
        HostApplicationBuilderSettings settings,
        Action<IServiceCollection> postPluginRegistrationCallback)
        : this(settings, PostPluginRegistrationCallbacks: [postPluginRegistrationCallback], NullLogger.Instance)
    {
        ArgumentNullException.ThrowIfNull(postPluginRegistrationCallback);
    }

    /// <summary>
    /// Initializes a new instance with pre- and post-plugin registration callbacks.
    /// </summary>
    /// <param name="settings">The host application builder settings to use.</param>
    /// <param name="prePluginRegistrationCallbacks">Callbacks to execute before plugin registration.</param>
    /// <param name="postPluginRegistrationCallbacks">Callbacks to execute after plugin registration.</param>
    /// <exception cref="ArgumentNullException">Thrown when a parameter is null.</exception>
    public CreateHostOptions(
        HostApplicationBuilderSettings settings,
        IEnumerable<Action<IServiceCollection>> prePluginRegistrationCallbacks,
        IEnumerable<Action<IServiceCollection>> postPluginRegistrationCallbacks)
        : this(settings, PostPluginRegistrationCallbacks: (postPluginRegistrationCallbacks ?? throw new ArgumentNullException(nameof(postPluginRegistrationCallbacks))).ToArray(), NullLogger.Instance)
    {
        ArgumentNullException.ThrowIfNull(prePluginRegistrationCallbacks);
        PrePluginRegistrationCallbacks = prePluginRegistrationCallbacks.ToArray();
    }

    /// <summary>
    /// Initializes a new instance with a logger.
    /// </summary>
    /// <param name="settings">The host application builder settings to use.</param>
    /// <param name="logger">
    /// The logger instance to use for logging during host creation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> is null.
    /// </exception>
    public CreateHostOptions(
        HostApplicationBuilderSettings settings,
        ILogger logger)
        : this(settings, PostPluginRegistrationCallbacks: [], logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
    }

    /// <summary>
    /// Initializes a new instance with pre-plugin registration callbacks and logger.
    /// </summary>
    /// <param name="settings">The host application builder settings to use.</param>
    /// <param name="prePluginRegistrationCallbacks">Callbacks to execute before plugin registration.</param>
    /// <param name="logger">The logger instance to use for logging during host creation.</param>
    /// <exception cref="ArgumentNullException">Thrown when a parameter is null.</exception>
    public CreateHostOptions(
        HostApplicationBuilderSettings settings,
        IEnumerable<Action<IServiceCollection>> prePluginRegistrationCallbacks,
        ILogger logger)
        : this(settings, PostPluginRegistrationCallbacks: [], logger)
    {
        ArgumentNullException.ThrowIfNull(prePluginRegistrationCallbacks);
        PrePluginRegistrationCallbacks = prePluginRegistrationCallbacks.ToArray();
    }

    /// <summary>
    /// Initializes a new instance with a post-plugin registration callback and logger.
    /// </summary>
    /// <param name="settings">The host application builder settings to use.</param>
    /// <param name="postPluginRegistrationCallback">
    /// The callback to execute after plugin registration, allowing for additional service configuration.
    /// </param>
    /// <param name="logger">
    /// The logger instance to use for logging during host creation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="postPluginRegistrationCallback"/> or <paramref name="logger"/> is null.
    /// </exception>
    public CreateHostOptions(
        HostApplicationBuilderSettings settings,
        Action<IServiceCollection> postPluginRegistrationCallback,
        ILogger logger)
        : this(settings, PostPluginRegistrationCallbacks: [postPluginRegistrationCallback], logger)
    {
        ArgumentNullException.ThrowIfNull(postPluginRegistrationCallback);
        ArgumentNullException.ThrowIfNull(logger);
    }

    /// <summary>
    /// Initializes a new instance with pre- and post-plugin registration callbacks and logger.
    /// </summary>
    /// <param name="settings">The host application builder settings to use.</param>
    /// <param name="prePluginRegistrationCallbacks">Callbacks to execute before plugin registration.</param>
    /// <param name="postPluginRegistrationCallbacks">Callbacks to execute after plugin registration.</param>
    /// <param name="logger">The logger instance to use for logging during host creation.</param>
    /// <exception cref="ArgumentNullException">Thrown when a parameter is null.</exception>
    public CreateHostOptions(
        HostApplicationBuilderSettings settings,
        IEnumerable<Action<IServiceCollection>> prePluginRegistrationCallbacks,
        IEnumerable<Action<IServiceCollection>> postPluginRegistrationCallbacks,
        ILogger logger)
        : this(settings, PostPluginRegistrationCallbacks: (postPluginRegistrationCallbacks ?? throw new ArgumentNullException(nameof(postPluginRegistrationCallbacks))).ToArray(), logger)
    {
        ArgumentNullException.ThrowIfNull(prePluginRegistrationCallbacks);
        PrePluginRegistrationCallbacks = prePluginRegistrationCallbacks.ToArray();
    }

    /// <summary>
    /// Gets the default instance of <see cref="CreateHostOptions"/> with empty 
    /// settings and a <see cref="NullLogger"/>.
    /// </summary>
    public static CreateHostOptions Default => _defaultOptions;
}
