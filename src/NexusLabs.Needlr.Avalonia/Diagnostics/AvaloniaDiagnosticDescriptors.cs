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
}
