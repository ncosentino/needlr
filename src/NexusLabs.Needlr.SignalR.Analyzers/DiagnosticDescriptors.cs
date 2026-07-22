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
    /// NDLRSIG001: HubPath must be a constant expression for AOT compatibility.
    /// </summary>
    public static readonly DiagnosticDescriptor HubPathMustBeConstant = new(
        id: DiagnosticIds.HubPathMustBeConstant,
        title: "HubPath must be a constant",
        messageFormat: "HubPath '{0}' must be a constant expression for AOT compatibility",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The HubPath parameter of HubPathAttribute must be a compile-time constant string for AOT compatibility.",
        helpLinkUri: HelpLinkBase + "NDLRSIG001.md");

    /// <summary>
    /// NDLRSIG002: HubType must be a typeof expression for AOT compatibility.
    /// </summary>
    public static readonly DiagnosticDescriptor HubTypeMustBeTypeOf = new(
        id: DiagnosticIds.HubTypeMustBeTypeOf,
        title: "HubType must be a typeof expression",
        messageFormat: "HubType must be a typeof expression, not '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The HubType parameter of HubPathAttribute must be a typeof expression for AOT compatibility.",
        helpLinkUri: HelpLinkBase + "NDLRSIG002.md");

    /// <summary>
    /// NDLRSIG003: An IHubRegistrationPlugin implementation is eligible for
    /// generated-constructor generation, which prevents it from being instantiated.
    /// </summary>
    public static readonly DiagnosticDescriptor HubRegistrationPluginRequiresParameterlessActivation = new(
        id: DiagnosticIds.HubRegistrationPluginRequiresParameterlessActivation,
        title: "IHubRegistrationPlugin implementation cannot use generated-constructor generation",
        messageFormat: "Type '{0}' implements IHubRegistrationPlugin and is eligible for generated-constructor generation, but hub-registration plugins require parameterless activation; remove [GenerateConstructor] and every field-level constructor guard trigger, or add a hand-written parameterless constructor",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The SignalR hub-registration generator activates every IHubRegistrationPlugin implementation with a parameterless constructor, so it deliberately excludes any type eligible for generated-constructor generation from registration. Such a type is never activated and its hub is never registered.",
        helpLinkUri: HelpLinkBase + "NDLRSIG003.md");
}
