namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Identifies the Langfuse deployment that traces and scores are sent to. Used to derive
/// the base URL of the OTLP ingestion endpoint when <see cref="LangfuseOptions.Host"/> is
/// not set explicitly.
/// </summary>
public enum LangfuseRegion
{
    /// <summary>
    /// Langfuse Cloud, EU data region (<c>https://cloud.langfuse.com</c>). The default.
    /// </summary>
    Eu = 0,

    /// <summary>
    /// Langfuse Cloud, US data region (<c>https://us.cloud.langfuse.com</c>).
    /// </summary>
    Us = 1,

    /// <summary>
    /// Langfuse Cloud, Japan data region (<c>https://jp.cloud.langfuse.com</c>).
    /// </summary>
    Jp = 2,

    /// <summary>
    /// Langfuse Cloud, HIPAA data region (<c>https://hipaa.cloud.langfuse.com</c>).
    /// </summary>
    Hipaa = 3,
}
