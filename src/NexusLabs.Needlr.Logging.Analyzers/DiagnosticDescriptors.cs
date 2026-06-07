using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Logging.Analyzers;

/// <summary>
/// Diagnostic descriptors for logging-specific Needlr analyzers.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "NexusLabs.Needlr.Logging";
    private const string HelpLinkBase = "https://github.com/nexus-labs/needlr/blob/main/docs/analyzers/";

    /// <summary>
    /// NDLRLOG001: a <c>[NeedlrLoggerMessage]</c> method must be <c>partial</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor MustBePartial = new(
        id: DiagnosticIds.MustBePartial,
        title: "NeedlrLoggerMessage method must be partial",
        messageFormat: "Logging method '{0}' must be partial so its body can be generated",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A method marked with [NeedlrLoggerMessage] must be declared partial so the source generator can supply its implementation.",
        helpLinkUri: HelpLinkBase + "NDLRLOG001.md");

    /// <summary>
    /// NDLRLOG002: a <c>[NeedlrLoggerMessage]</c> method must return <c>void</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor MustReturnVoid = new(
        id: DiagnosticIds.MustReturnVoid,
        title: "NeedlrLoggerMessage method must return void",
        messageFormat: "Logging method '{0}' must return void",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A method marked with [NeedlrLoggerMessage] must return void.",
        helpLinkUri: HelpLinkBase + "NDLRLOG002.md");

    /// <summary>
    /// NDLRLOG003: a <c>[NeedlrLoggerMessage]</c> method must not be generic.
    /// </summary>
    public static readonly DiagnosticDescriptor MustNotBeGeneric = new(
        id: DiagnosticIds.MustNotBeGeneric,
        title: "NeedlrLoggerMessage method must not be generic",
        messageFormat: "Logging method '{0}' must not declare type parameters",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A method marked with [NeedlrLoggerMessage] must not be generic.",
        helpLinkUri: HelpLinkBase + "NDLRLOG003.md");

    /// <summary>
    /// NDLRLOG004: the type containing a <c>[NeedlrLoggerMessage]</c> method must be <c>partial</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
        id: DiagnosticIds.ContainingTypeMustBePartial,
        title: "Type containing a NeedlrLoggerMessage method must be partial",
        messageFormat: "Type '{0}' must be partial because it contains a [NeedlrLoggerMessage] method",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The type containing a [NeedlrLoggerMessage] method must be declared partial so the generated implementation can be added to it.",
        helpLinkUri: HelpLinkBase + "NDLRLOG004.md");

    /// <summary>
    /// NDLRLOG005: a <c>[NeedlrLoggerMessage]</c> method has no accessible <c>ILogger</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor LoggerNotFound = new(
        id: DiagnosticIds.LoggerNotFound,
        title: "NeedlrLoggerMessage method has no accessible ILogger",
        messageFormat: "Logging method '{0}' has no accessible ILogger; an instance method needs an ILogger field or property and a static method needs an ILogger parameter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [NeedlrLoggerMessage] method needs a logger: an instance method must have an ILogger field or property on its type, and a static method must take an ILogger parameter.",
        helpLinkUri: HelpLinkBase + "NDLRLOG005.md");

    /// <summary>
    /// NDLRLOG006: a <c>[NeedlrLoggerMessage]</c> method has more than six non-exception parameters.
    /// </summary>
    public static readonly DiagnosticDescriptor TooManyParameters = new(
        id: DiagnosticIds.TooManyParameters,
        title: "NeedlrLoggerMessage method exceeds the LoggerMessage.Define parameter limit",
        messageFormat: "Logging method '{0}' has {1} non-exception parameters; the allocation-free fast path supports at most six, so a slower fallback is generated",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "LoggerMessage.Define supports at most six message parameters. Methods with more parameters still work but fall back to a slower logging call; reduce the parameter count to keep the allocation-free fast path.",
        helpLinkUri: HelpLinkBase + "NDLRLOG006.md");
}
