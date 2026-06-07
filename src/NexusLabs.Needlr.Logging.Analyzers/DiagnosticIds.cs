namespace NexusLabs.Needlr.Logging.Analyzers;

/// <summary>
/// Diagnostic IDs for logging-specific Needlr analyzers.
/// </summary>
/// <remarks>
/// Logging analyzer codes use the NDLRLOG prefix.
/// </remarks>
public static class DiagnosticIds
{
    /// <summary>
    /// NDLRLOG001: a method marked with <c>[NeedlrLoggerMessage]</c> must be <c>partial</c>.
    /// </summary>
    public const string MustBePartial = "NDLRLOG001";

    /// <summary>
    /// NDLRLOG002: a method marked with <c>[NeedlrLoggerMessage]</c> must return <c>void</c>.
    /// </summary>
    public const string MustReturnVoid = "NDLRLOG002";

    /// <summary>
    /// NDLRLOG003: a method marked with <c>[NeedlrLoggerMessage]</c> must not be generic.
    /// </summary>
    public const string MustNotBeGeneric = "NDLRLOG003";

    /// <summary>
    /// NDLRLOG004: the type containing a <c>[NeedlrLoggerMessage]</c> method must be <c>partial</c>.
    /// </summary>
    public const string ContainingTypeMustBePartial = "NDLRLOG004";

    /// <summary>
    /// NDLRLOG005: a <c>[NeedlrLoggerMessage]</c> method has no accessible <c>ILogger</c>.
    /// </summary>
    public const string LoggerNotFound = "NDLRLOG005";

    /// <summary>
    /// NDLRLOG006: a <c>[NeedlrLoggerMessage]</c> method has more than six non-exception parameters
    /// and therefore cannot use the allocation-free <c>LoggerMessage.Define</c> fast path.
    /// </summary>
    public const string TooManyParameters = "NDLRLOG006";
}
