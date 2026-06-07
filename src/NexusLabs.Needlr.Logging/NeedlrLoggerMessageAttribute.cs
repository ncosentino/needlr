using System;

using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Logging;

/// <summary>
/// Marks a <c>partial</c> method whose body Needlr will source-generate into a
/// high-performance, cancellation-aware logging method.
/// </summary>
/// <remarks>
/// <para>
/// This attribute mirrors the surface of <c>Microsoft.Extensions.Logging.LoggerMessageAttribute</c>
/// (<see cref="EventId"/>, <see cref="EventName"/>, <see cref="Level"/>, <see cref="Message"/>,
/// <see cref="SkipEnabledCheck"/>) so migrating an existing <c>[LoggerMessage]</c> method is a
/// near drop-in swap. The generated body reuses the public
/// <see cref="LoggerMessage.Define(LogLevel, EventId, string)"/> primitive — the same fast path the
/// built-in generator targets — so there is no performance penalty for the common case.
/// </para>
/// <para>
/// The difference from the built-in attribute is the <em>cancellation guard</em>: when the method
/// has a parameter assignable to <see cref="System.Exception"/> and that argument is a cancellation
/// (an <see cref="OperationCanceledException"/> by default), the generated body consults
/// <see cref="NeedlrCancellationLogging"/> and, by default, <strong>skips the log entirely</strong>.
/// Cancellation is normal control flow in production code, so logging it as a warning or error is
/// usually noise. The behavior can be globally overridden at startup (force-log or demote to a lower
/// level) via <see cref="NeedlrCancellationLogging"/>.
/// </para>
/// <para>
/// The decorated method must be <c>partial</c>, return <c>void</c>, be non-generic, and live in a
/// <c>partial</c> type. For an <c>instance</c> method, the containing type must expose an
/// <see cref="ILogger"/> field or property; for a <c>static</c> method, an <see cref="ILogger"/>
/// parameter supplies the logger.
/// </para>
/// <para>
/// This is a source-generation only feature. It requires the
/// <c>NexusLabs.Needlr.Logging</c> package and the generator that ships with it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using Microsoft.Extensions.Logging;
/// using NexusLabs.Needlr.Logging;
///
/// public partial class OrderService
/// {
///     private readonly ILogger&lt;OrderService&gt; _logger;
///
///     public OrderService(ILogger&lt;OrderService&gt; logger) =&gt; _logger = logger;
///
///     // When 'error' is an OperationCanceledException, this logs nothing by default.
///     [NeedlrLoggerMessage(EventId = 42, Level = LogLevel.Warning, Message = "Order {OrderId} failed")]
///     public partial void LogOrderFailed(int orderId, Exception error);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class NeedlrLoggerMessageAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NeedlrLoggerMessageAttribute"/> class.
    /// All values are supplied via the named properties.
    /// </summary>
    public NeedlrLoggerMessageAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NeedlrLoggerMessageAttribute"/> class with a
    /// message template. The <see cref="Level"/> defaults to <see cref="LogLevel.Information"/>.
    /// </summary>
    /// <param name="message">The structured message template (e.g. <c>"Order {OrderId} failed"</c>).</param>
    public NeedlrLoggerMessageAttribute(string message)
    {
        Message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NeedlrLoggerMessageAttribute"/> class with a
    /// log level and message template.
    /// </summary>
    /// <param name="level">The level at which the message is logged.</param>
    /// <param name="message">The structured message template (e.g. <c>"Order {OrderId} failed"</c>).</param>
    public NeedlrLoggerMessageAttribute(LogLevel level, string message)
    {
        Level = level;
        Message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NeedlrLoggerMessageAttribute"/> class with an
    /// event id, log level, and message template.
    /// </summary>
    /// <param name="eventId">The numeric event id assigned to the log entry.</param>
    /// <param name="level">The level at which the message is logged.</param>
    /// <param name="message">The structured message template (e.g. <c>"Order {OrderId} failed"</c>).</param>
    public NeedlrLoggerMessageAttribute(int eventId, LogLevel level, string message)
    {
        EventId = eventId;
        Level = level;
        Message = message;
    }

    /// <summary>
    /// Gets or sets the numeric event id assigned to the generated log entry. Defaults to <c>0</c>.
    /// </summary>
    public int EventId { get; set; }

    /// <summary>
    /// Gets or sets the event name assigned to the generated log entry. When <see langword="null"/>,
    /// the decorated method's name is used.
    /// </summary>
    public string? EventName { get; set; }

    /// <summary>
    /// Gets or sets the level at which the message is logged. Defaults to
    /// <see cref="LogLevel.Information"/> when not supplied via a constructor.
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the structured message template. Placeholders (e.g. <c>{OrderId}</c>) bind to the
    /// method's non-exception parameters in declaration order, exactly as with the built-in attribute.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the generated body omits the
    /// <see cref="ILogger.IsEnabled(LogLevel)"/> guard. Set to <see langword="true"/> only when the
    /// caller has already checked that the level is enabled. Defaults to <see langword="false"/>.
    /// </summary>
    public bool SkipEnabledCheck { get; set; }
}
