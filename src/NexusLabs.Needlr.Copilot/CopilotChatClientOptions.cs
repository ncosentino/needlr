namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Configuration options for <see cref="CopilotChatClient"/> and related Copilot API types.
/// </summary>
public sealed class CopilotChatClientOptions
{
    /// <summary>
    /// The default model to use when <see cref="Microsoft.Extensions.AI.ChatOptions.ModelId"/>
    /// is not specified by the caller. Defaults to <c>claude-sonnet-4</c>.
    /// </summary>
    public string DefaultModel { get; set; } = "claude-sonnet-4.6";

    /// <summary>
    /// Controls where the GitHub OAuth token is sourced from.
    /// Defaults to <see cref="CopilotTokenSource.Auto"/>.
    /// </summary>
    public CopilotTokenSource TokenSource { get; set; } = CopilotTokenSource.Auto;

    /// <summary>
    /// Optional explicit GitHub OAuth token. When set, takes precedence over
    /// <see cref="TokenSource"/> discovery.
    /// </summary>
    public string? GitHubToken { get; set; }

    /// <summary>
    /// The Copilot API base URL. Defaults to <c>https://api.githubcopilot.com</c>.
    /// </summary>
    public string CopilotApiBaseUrl { get; set; } = "https://api.githubcopilot.com";

    /// <summary>
    /// The GitHub API base URL for token exchange.
    /// Defaults to <c>https://api.github.com</c>.
    /// </summary>
    public string GitHubApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>
    /// Value sent in the <c>Copilot-Integration-Id</c> header.
    /// Defaults to <c>needlr-copilot</c>.
    /// </summary>
    public string IntegrationId { get; set; } = "needlr-copilot";

    /// <summary>
    /// Value sent in the <c>Editor-Version</c> header.
    /// Defaults to <c>NexusLabs.Needlr/1.0.0</c>.
    /// </summary>
    public string EditorVersion { get; set; } = "NexusLabs.Needlr/1.0.0";

    /// <summary>
    /// How many seconds before the Copilot API token expires to trigger a refresh.
    /// Defaults to 60.
    /// </summary>
    public int TokenRefreshBufferSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of retries on HTTP 429 (Too Many Requests) responses.
    /// Defaults to 3. Set to 0 to disable retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before the first retry on 429.
    /// Subsequent retries use exponential backoff (delay × 2^attempt).
    /// If the response includes a <c>Retry-After</c> header, that value
    /// is used instead. Defaults to 1000ms.
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 1000;
}
