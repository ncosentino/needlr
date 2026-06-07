using System;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Logging;

/// <summary>
/// Global, process-wide policy that Needlr source-generated logging methods consult when their
/// exception argument is a cancellation. Provides an escape hatch from the opinionated default
/// (<see cref="CancellationLoggingBehavior.Skip"/>).
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberate <em>global feature switch</em> (comparable to an
/// <see cref="AppContext"/> switch), not a service-locator: it must be reachable from allocation-free
/// generated method bodies without threading state through every call site. Set the properties once at
/// application startup — for example, to differ between Debug and Release builds.
/// </para>
/// <para>
/// The initial defaults are read <strong>once</strong>, lazily, the first time they are needed, from
/// environment variables, and then cached for the lifetime of the process:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="BehaviorEnvironmentVariable"/> — <c>skip</c> (default), <c>log</c>, or <c>demote</c>
/// (case-insensitive).
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="DemotedLevelEnvironmentVariable"/> — a <see cref="LogLevel"/> name (e.g. <c>Debug</c>).
/// </description>
/// </item>
/// </list>
/// <para>
/// Assigning any property overrides the environment-derived default from that point on.
/// </para>
/// </remarks>
public static class NeedlrCancellationLogging
{
    /// <summary>
    /// The environment variable that supplies the initial <see cref="Behavior"/> default:
    /// <c>skip</c>, <c>log</c>, or <c>demote</c> (case-insensitive). Unrecognized or missing values
    /// fall back to <see cref="CancellationLoggingBehavior.Skip"/>.
    /// </summary>
    public const string BehaviorEnvironmentVariable = "__NEEDLR_CANCELLATION_LOGGING_BEHAVIOR";

    /// <summary>
    /// The environment variable that supplies the initial <see cref="DemotedLevel"/> default: any
    /// <see cref="LogLevel"/> name (case-insensitive). Unrecognized or missing values fall back to
    /// <see cref="LogLevel.Debug"/>.
    /// </summary>
    public const string DemotedLevelEnvironmentVariable = "__NEEDLR_CANCELLATION_LOGGING_DEMOTED_LEVEL";

    private static Lazy<CancellationLoggingBehavior> _defaultBehavior =
        new(ReadBehaviorFromEnvironment, LazyThreadSafetyMode.ExecutionAndPublication);

    private static Lazy<LogLevel> _defaultDemotedLevel =
        new(ReadDemotedLevelFromEnvironment, LazyThreadSafetyMode.ExecutionAndPublication);

    private static CancellationLoggingBehavior? _behaviorOverride;
    private static LogLevel? _demotedLevelOverride;
    private static Func<Exception, bool>? _isCancellationOverride;

    /// <summary>
    /// Gets or sets how generated logging methods treat a cancellation exception. Defaults to the
    /// value of <see cref="BehaviorEnvironmentVariable"/>, or
    /// <see cref="CancellationLoggingBehavior.Skip"/> when unset.
    /// </summary>
    public static CancellationLoggingBehavior Behavior
    {
        get => _behaviorOverride ?? _defaultBehavior.Value;
        set => _behaviorOverride = value;
    }

    /// <summary>
    /// Gets or sets the level used when <see cref="Behavior"/> is
    /// <see cref="CancellationLoggingBehavior.Demote"/>. Defaults to the value of
    /// <see cref="DemotedLevelEnvironmentVariable"/>, or <see cref="LogLevel.Debug"/> when unset.
    /// </summary>
    public static LogLevel DemotedLevel
    {
        get => _demotedLevelOverride ?? _defaultDemotedLevel.Value;
        set => _demotedLevelOverride = value;
    }

    /// <summary>
    /// Gets or sets the predicate that decides whether an exception counts as a cancellation. The
    /// default treats any <see cref="OperationCanceledException"/> (which includes
    /// <see cref="System.Threading.Tasks.TaskCanceledException"/>) as a cancellation.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
    public static Func<Exception, bool> IsCancellationPredicate
    {
        get => _isCancellationOverride ?? DefaultIsCancellation;
        set => _isCancellationOverride = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Determines whether the supplied exception is considered a cancellation, according to
    /// <see cref="IsCancellationPredicate"/>. This is the entry point called by generated code.
    /// </summary>
    /// <param name="exception">The exception argument passed to a logging method; may be <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="exception"/> is non-null and the predicate
    /// classifies it as a cancellation; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsCancellation(Exception? exception) =>
        exception is not null && IsCancellationPredicate(exception);

    private static bool DefaultIsCancellation(Exception exception) =>
        exception is OperationCanceledException;

    internal static CancellationLoggingBehavior ParseBehavior(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return CancellationLoggingBehavior.Skip;
        }

        return raw!.Trim().ToLowerInvariant() switch
        {
            "skip" => CancellationLoggingBehavior.Skip,
            "log" => CancellationLoggingBehavior.Log,
            "demote" => CancellationLoggingBehavior.Demote,
            _ => CancellationLoggingBehavior.Skip,
        };
    }

    internal static LogLevel ParseDemotedLevel(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw) &&
            Enum.TryParse(raw!.Trim(), ignoreCase: true, out LogLevel level) &&
            Enum.IsDefined(typeof(LogLevel), level))
        {
            return level;
        }

        return LogLevel.Debug;
    }

    private static CancellationLoggingBehavior ReadBehaviorFromEnvironment() =>
        ParseBehavior(Environment.GetEnvironmentVariable(BehaviorEnvironmentVariable));

    private static LogLevel ReadDemotedLevelFromEnvironment() =>
        ParseDemotedLevel(Environment.GetEnvironmentVariable(DemotedLevelEnvironmentVariable));

    /// <summary>
    /// Resets all overrides and re-arms the lazy environment reads. Intended for test isolation so a
    /// test can configure environment variables and observe the freshly-derived defaults.
    /// </summary>
    internal static void ResetForTests()
    {
        _behaviorOverride = null;
        _demotedLevelOverride = null;
        _isCancellationOverride = null;
        _defaultBehavior = new Lazy<CancellationLoggingBehavior>(
            ReadBehaviorFromEnvironment, LazyThreadSafetyMode.ExecutionAndPublication);
        _defaultDemotedLevel = new Lazy<LogLevel>(
            ReadDemotedLevelFromEnvironment, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}
