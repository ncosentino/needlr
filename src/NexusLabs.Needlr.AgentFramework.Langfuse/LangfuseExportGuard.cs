namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Shared guard logic for the Langfuse export entry points. Surfaces a diagnostic when credentials
/// are present but no explicit export target was chosen, so the "disabled to avoid cloud egress"
/// decision is visible rather than silent.
/// </summary>
internal static class LangfuseExportGuard
{
    /// <summary>
    /// Invokes <see cref="LangfuseOptions.DiagnosticsCallback"/> when credentials are present and
    /// enabled but neither a <see cref="LangfuseOptions.Host"/> nor a
    /// <see cref="LangfuseOptions.Region"/> was set, explaining why export is disabled.
    /// </summary>
    /// <param name="options">The export options.</param>
    public static void WarnIfCredentialsWithoutTarget(LangfuseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.HasCredentials && !options.HasExplicitTarget)
        {
            options.DiagnosticsCallback?.Invoke(
                "Langfuse credentials were provided but no export target was set, so export is " +
                "disabled. This prevents accidentally sending traces (which may include prompts, " +
                "agent outputs, and customer data) to Langfuse Cloud. Set LangfuseOptions.Host for " +
                "a self-hosted deployment, or LangfuseOptions.Region to opt in to a Langfuse Cloud " +
                "region.");
        }
    }
}
