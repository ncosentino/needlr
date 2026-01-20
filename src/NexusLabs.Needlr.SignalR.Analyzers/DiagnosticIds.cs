namespace NexusLabs.Needlr.SignalR.Analyzers;

/// <summary>
/// Diagnostic IDs for SignalR-specific Needlr analyzers.
/// </summary>
/// <remarks>
/// SignalR analyzer codes use the NDLRSIG prefix.
/// </remarks>
public static class DiagnosticIds
{
    /// <summary>
    /// NDLRSIG001: HubPath must be a constant expression.
    /// </summary>
    public const string HubPathMustBeConstant = "NDLRSIG001";

    /// <summary>
    /// NDLRSIG002: HubType must be a typeof expression.
    /// </summary>
    public const string HubTypeMustBeTypeOf = "NDLRSIG002";
}
