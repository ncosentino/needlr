---
description: Use GitHub Copilot as an IChatClient provider in Needlr -- zero-cost local AI for agent development with web search tool support.
---

# GitHub Copilot Integration

`NexusLabs.Needlr.Copilot` provides two capabilities:

1. **`CopilotChatClient`** — an `IChatClient` backed by the GitHub Copilot API (no CLI process, direct HTTP)
2. **`CopilotWebSearchFunction`** — an `AIFunction` wrapping Copilot's MCP `web_search` tool

Because Copilot is free for many developers, this is an excellent local-development alternative to Azure OpenAI or other paid providers.

---

## Installation

```xml
<PackageReference Include="NexusLabs.Needlr.Copilot" />
```

---

## Quick Start

### Standalone usage

```csharp
using Microsoft.Extensions.AI;
using NexusLabs.Needlr.Copilot;

// Automatically discovers your GitHub token from the Copilot CLI's apps.json
using var client = new CopilotChatClient(new CopilotChatClientOptions());

var response = await client.GetResponseAsync(
[
    new(ChatRole.User, "What is dependency injection?"),
]);

Console.WriteLine(response.Messages.Last());
```

### With Needlr Agent Framework

Plug the Copilot client into any Needlr agent setup via the existing `UsingChatClient()` hook — no special extensions needed:

```csharp
var services = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(new CopilotChatClient(new CopilotChatClientOptions
        {
            DefaultModel = "claude-sonnet-4",
        }))
        .AddAgentFunctionsFromAssemblies())
    .BuildServiceProvider(configuration);
```

---

## Authentication

`CopilotChatClient` needs a GitHub OAuth token to exchange for a Copilot API token. The discovery chain (in order of precedence):

| Source | How |
|---|---|
| **Explicit token** | Set `CopilotChatClientOptions.GitHubToken` directly |
| **apps.json** | Auto-discovered from `~/.config/github-copilot/apps.json` (macOS/Linux) or `%LOCALAPPDATA%\github-copilot\apps.json` (Windows) |
| **`GH_TOKEN` env var** | Standard GitHub CLI environment variable |
| **`GITHUB_TOKEN` env var** | Fallback environment variable |

The `TokenSource` property controls which sources are tried:

```csharp
var options = new CopilotChatClientOptions
{
    // Default: tries all sources in order
    TokenSource = CopilotTokenSource.Auto,
};
```

| `CopilotTokenSource` | Behaviour |
|---|---|
| `Auto` | Explicit → apps.json → env vars |
| `AppsJson` | apps.json only |
| `EnvironmentVariable` | `GH_TOKEN` / `GITHUB_TOKEN` only |

!!! tip "Copilot CLI login"
    If you have the GitHub Copilot CLI extension installed and have run `gh copilot auth login`, the apps.json file is already populated. No additional configuration is needed.

---

## Web Search Tool

`CopilotToolSet` creates `AIFunction` instances backed by Copilot's MCP endpoint. Currently the only available tool is `web_search`:

```csharp
using NexusLabs.Needlr.Copilot;

var chatOptions = new CopilotChatClientOptions();
var tools = CopilotToolSet.Create(
    opts => opts.EnableWebSearch = true,
    chatOptions);

// tools[0] is an AIFunction named "web_search"
// Pass it to ChatOptions.Tools for any IChatClient
```

### With the Agent Framework

```csharp
var chatOptions = new CopilotChatClientOptions();
var copilotTools = CopilotToolSet.Create(
    opts => opts.EnableWebSearch = true,
    chatOptions);

var services = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af
        .UsingChatClient(new CopilotChatClient(chatOptions))
        .AddAgentFunctionsFromAssemblies())
    .BuildServiceProvider(configuration);

var agentFactory = services.GetRequiredService<IAgentFactory>();
var agent = agentFactory.CreateAgent(opts =>
{
    opts.Instructions = "You are a research assistant with web access.";
    opts.AdditionalTools = copilotTools;
});
```

---

## Streaming

`CopilotChatClient` supports SSE streaming:

```csharp
await foreach (var update in client.GetStreamingResponseAsync(messages))
{
    Console.Write(update.Text);
}
```

Malformed SSE chunks are silently skipped — the stream continues without interruption.

---

## Configuration Reference

All settings are on `CopilotChatClientOptions`:

| Property | Default | Description |
|---|---|---|
| `DefaultModel` | `"claude-sonnet-4"` | Model to use when `ChatOptions.ModelId` is not set |
| `CopilotApiBaseUrl` | `"https://api.githubcopilot.com"` | Base URL for the Copilot chat API |
| `GitHubApiBaseUrl` | `"https://api.github.com"` | Base URL for the GitHub API (token exchange) |
| `IntegrationId` | `"needlr-copilot"` | Sent as `Copilot-Integration-Id` header |
| `GitHubToken` | `null` | Explicit GitHub OAuth token (bypasses discovery) |
| `TokenSource` | `Auto` | Which token sources to try |
| `MaxRetries` | `3` | Maximum retry attempts for 429 responses |
| `RetryBaseDelayMs` | `1000` | Base delay for exponential backoff (ms) |
| `TokenRefreshBufferSeconds` | `60` | Refresh the Copilot token this many seconds before expiry |

---

## Retry Behaviour

The client retries only on HTTP 429 (Too Many Requests):

- Uses the `Retry-After` header when present
- Falls back to exponential backoff: `RetryBaseDelayMs * 2^attempt`
- Gives up after `MaxRetries` attempts

Other HTTP errors (401, 500, etc.) are thrown immediately.
