namespace NexusLabs.Needlr.SignalR.Analyzers;

/// <summary>
/// Diagnostic IDs for SignalR-specific Needlr analyzers.
/// </summary>
/// <remarks>
/// SignalR analyzer codes use the NDLR1xxx range.
/// </remarks>
public static class DiagnosticIds
{
    /// <summary>
    /// NDLR1001: HubPath must be a constant expression.
    /// </summary>
    public const string HubPathMustBeConstant = "NDLR1001";

    /// <summary>
    /// NDLR1002: HubType must be a typeof expression.
    /// </summary>
    public const string HubTypeMustBeTypeOf = "NDLR1002";
}
