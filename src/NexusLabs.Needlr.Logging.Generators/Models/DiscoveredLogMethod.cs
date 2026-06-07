using System.Collections.Immutable;

namespace NexusLabs.Needlr.Logging.Generators.Models;

/// <summary>
/// A fully-resolved <c>[NeedlrLoggerMessage]</c> method ready for emission. All data is captured as
/// primitive values so the model flows cheaply through the incremental pipeline.
/// </summary>
internal readonly struct DiscoveredLogMethod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveredLogMethod"/> struct.
    /// </summary>
    public DiscoveredLogMethod(
        string @namespace,
        ImmutableArray<ContainingTypeInfo> containingTypes,
        string methodName,
        string modifiers,
        ImmutableArray<LogParameterInfo> parameters,
        string loggerAccess,
        int eventId,
        string eventName,
        string levelName,
        string message,
        bool skipEnabledCheck)
    {
        Namespace = @namespace;
        ContainingTypes = containingTypes;
        MethodName = methodName;
        Modifiers = modifiers;
        Parameters = parameters;
        LoggerAccess = loggerAccess;
        EventId = eventId;
        EventName = eventName;
        LevelName = levelName;
        Message = message;
        SkipEnabledCheck = skipEnabledCheck;
    }

    /// <summary>
    /// Gets the containing namespace, or an empty string when the type is in the global namespace.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Gets the chain of partial containing types, ordered outermost to innermost.
    /// </summary>
    public ImmutableArray<ContainingTypeInfo> ContainingTypes { get; }

    /// <summary>
    /// Gets the method name.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the method modifiers (e.g. <c>public</c>, <c>public static</c>, or an empty string for a
    /// classic private partial method), excluding the <c>partial</c> keyword.
    /// </summary>
    public string Modifiers { get; }

    /// <summary>
    /// Gets every parameter in declaration order, including the logger and exception parameters.
    /// </summary>
    public ImmutableArray<LogParameterInfo> Parameters { get; }

    /// <summary>
    /// Gets the expression used to obtain the <c>ILogger</c> inside the generated body
    /// (a member name for an instance method, or a parameter name for a static method).
    /// </summary>
    public string LoggerAccess { get; }

    /// <summary>
    /// Gets the numeric event id.
    /// </summary>
    public int EventId { get; }

    /// <summary>
    /// Gets the resolved event name (the explicit <c>EventName</c>, otherwise the method name).
    /// </summary>
    public string EventName { get; }

    /// <summary>
    /// Gets the <c>Microsoft.Extensions.Logging.LogLevel</c> member name (e.g. <c>Warning</c>).
    /// </summary>
    public string LevelName { get; }

    /// <summary>
    /// Gets the structured message template.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets a value indicating whether the generated body omits the <c>IsEnabled</c> guard.
    /// </summary>
    public bool SkipEnabledCheck { get; }

    /// <summary>
    /// Gets a key that uniquely identifies the containing type across the compilation, used to group
    /// methods that should be emitted into the same partial type.
    /// </summary>
    public string ContainingTypeKey
    {
        get
        {
            var key = Namespace + "::";
            foreach (var type in ContainingTypes)
            {
                key += type.Name + type.TypeParameterList + ".";
            }

            return key;
        }
    }
}
