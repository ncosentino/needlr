using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Configuration for which Copilot-backed tools to enable.
/// </summary>
public sealed class CopilotToolSetOptions
{
    /// <summary>
    /// Enable the Copilot <c>web_search</c> tool (calls the GitHub MCP server).
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool EnableWebSearch { get; set; }
}

/// <summary>
/// Factory for creating Copilot-backed <see cref="AIFunction"/> instances.
/// Use with Needlr's agent framework tool sets or pass directly to
/// <see cref="ChatOptions.Tools"/>.
/// </summary>
/// <example>
/// <code>
/// // Config-driven tool toggle:
/// var tools = CopilotToolSet.Create(new CopilotToolSetOptions { EnableWebSearch = true });
///
/// // With callback:
/// var tools = CopilotToolSet.Create(t => t.EnableWebSearch = true);
/// </code>
/// </example>
public static class CopilotToolSet
{
    /// <summary>
    /// Creates Copilot-backed <see cref="AIFunction"/> instances based on the options.
    /// The returned functions call the GitHub Copilot MCP server using the provided
    /// OAuth token provider.
    /// </summary>
    /// <param name="oauthProvider">Provides the GitHub OAuth token for MCP authentication.</param>
    /// <param name="toolOptions">Controls which tools are enabled.</param>
    /// <param name="clientOptions">Optional Copilot API configuration.</param>
    /// <returns>A list of enabled <see cref="AIFunction"/> instances.</returns>
    public static IReadOnlyList<AIFunction> Create(
        IGitHubOAuthTokenProvider oauthProvider,
        CopilotToolSetOptions toolOptions,
        CopilotChatClientOptions? clientOptions = null)
    {
        ArgumentNullException.ThrowIfNull(oauthProvider);
        ArgumentNullException.ThrowIfNull(toolOptions);

        var functions = new List<AIFunction>();

        if (toolOptions.EnableWebSearch)
        {
            var mcpClient = new CopilotMcpToolClient(oauthProvider, clientOptions);
            functions.Add(new CopilotWebSearchFunction(mcpClient));
        }

        return functions;
    }

    /// <summary>
    /// Creates Copilot-backed tools with automatic OAuth token discovery.
    /// </summary>
    /// <param name="toolOptions">Controls which tools are enabled.</param>
    /// <param name="clientOptions">Optional Copilot API configuration.</param>
    /// <returns>A list of enabled <see cref="AIFunction"/> instances.</returns>
    public static IReadOnlyList<AIFunction> Create(
        CopilotToolSetOptions toolOptions,
        CopilotChatClientOptions? clientOptions = null)
    {
        var options = clientOptions ?? new CopilotChatClientOptions();
        var oauthProvider = new GitHubOAuthTokenProvider(options);
        return Create(oauthProvider, toolOptions, options);
    }

    /// <summary>
    /// Creates Copilot-backed tools with a configuration callback.
    /// </summary>
    /// <param name="configure">Callback to configure which tools to enable.</param>
    /// <param name="clientOptions">Optional Copilot API configuration.</param>
    /// <returns>A list of enabled <see cref="AIFunction"/> instances.</returns>
    public static IReadOnlyList<AIFunction> Create(
        Action<CopilotToolSetOptions> configure,
        CopilotChatClientOptions? clientOptions = null)
    {
        var toolOptions = new CopilotToolSetOptions();
        configure(toolOptions);
        return Create(toolOptions, clientOptions);
    }
}
