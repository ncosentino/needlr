using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Avalonia.Diagnostics;

internal static class AvaloniaDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor ClassMustBePartial = new(
        id: AvaloniaDiagnosticIds.ClassMustBePartial,
        title: "Class must be partial",
        messageFormat: "[GenerateAvaloniaDesignTimeConstructor] requires '{0}' to be partial. Add the 'partial' modifier.",
        category: "NexusLabs.Needlr.Avalonia",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AlreadyHasParameterlessCtor = new(
        id: AvaloniaDiagnosticIds.AlreadyHasParameterlessCtor,
        title: "Parameterless constructor already exists",
        messageFormat: "'{0}' already has a parameterless constructor. Remove it or remove [GenerateAvaloniaDesignTimeConstructor].",
        category: "NexusLabs.Needlr.Avalonia",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoParameterizedCtor = new(
        id: AvaloniaDiagnosticIds.NoParameterizedCtor,
        title: "No parameterized constructor found",
        messageFormat: "'{0}' has no constructor with parameters. [GenerateAvaloniaDesignTimeConstructor] has no effect — the class already has an implicit parameterless constructor.",
        category: "NexusLabs.Needlr.Avalonia",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PrimaryConstructorNotSupported = new(
        id: AvaloniaDiagnosticIds.PrimaryConstructorNotSupported,
        title: "Primary constructors are not supported",
        messageFormat: "'{0}' uses a primary constructor. [GenerateAvaloniaDesignTimeConstructor] cannot generate a safe design-time constructor for classes with primary constructors — the generated constructor would pass null for all parameters, causing NullReferenceExceptions during design-time preview. Use a regular constructor with fields instead.",
        category: "NexusLabs.Needlr.Avalonia",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
