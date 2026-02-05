namespace NeedlrCodeLens;

using Microsoft.VisualStudio.Extensibility;

/// <summary>
/// Needlr CodeLens extension entry point.
/// Shows dependency information above Needlr-registered service classes.
/// </summary>
[VisualStudioContribution]
internal class ExtensionEntrypoint : Extension
{
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            "NexusLabs.NeedlrCodeLens.b69b7452-4333-4185-a9cb-e9e78c9be818",
            this.ExtensionAssemblyVersion,
            "Nexus Labs",
            "Needlr CodeLens",
            "Shows dependency injection information above Needlr service classes.")
        {
            // Support VS 2022 17.14+ through VS 2026 (18.x)
            InstallationTargetVersion = "[17.14,19.0)",
        },
    };
}
