using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.SignalR.Analyzers;

/// <summary>
/// Diagnostic descriptors for SignalR-specific Needlr analyzers.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "NexusLabs.Needlr.SignalR";
    private const string HelpLinkBase = "https://github.com/nexus-labs/needlr/blob/main/docs/analyzers/";

    /// <summary>
    /// NDLR1001: HubPath must be a constant expression for AOT compatibility.
    /// </summary>
    public static readonly DiagnosticDescriptor HubPathMustBeConstant = new(
        id: DiagnosticIds.HubPathMustBeConstant,
        title: "HubPath must be a constant",
        messageFormat: "HubPath '{0}' must be a constant expression for AOT compatibility",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The HubPath parameter of HubPathAttribute must be a compile-time constant string for AOT compatibility.",
        helpLinkUri: HelpLinkBase + "NDLR1001.md");

    /// <summary>
    /// NDLR1002: HubType must be a typeof expression for AOT compatibility.
    /// </summary>
    public static readonly DiagnosticDescriptor HubTypeMustBeTypeOf = new(
        id: DiagnosticIds.HubTypeMustBeTypeOf,
        title: "HubType must be a typeof expression",
        messageFormat: "HubType must be a typeof expression, not '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The HubType parameter of HubPathAttribute must be a typeof expression for AOT compatibility.",
        helpLinkUri: HelpLinkBase + "NDLR1002.md");
}
