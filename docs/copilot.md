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
| `IntegrationId` | `"copilot-developer-cli"` | Sent as `Copilot-Integration-Id` header |
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

---

## Needlr Copilot vs GitHub Copilot SDK

The official [GitHub Copilot SDK](https://github.com/github/copilot-sdk) (`GitHub.Copilot.SDK` on NuGet) provides a superset of what `NexusLabs.Needlr.Copilot` offers. Before choosing Needlr Copilot, understand the overlap:

| Capability | Needlr Copilot | GitHub Copilot SDK |
|---|---|---|
| `IChatClient` for Copilot models | ✅ `CopilotChatClient` (direct HTTP) | ✅ `CopilotClient` → `AsAIAgent()` (via CLI process) |
| Token discovery (apps.json, env vars) | ✅ Manual implementation | ✅ Built-in, plus OAuth App and BYOK |
| Streaming responses | ✅ SSE parsing | ✅ Full event stream with deltas |
| Web search | ✅ `CopilotWebSearchFunction` (MCP endpoint) | ✅ Built-in CLI tool (same backend) |
| Structured search results (citations, URLs) | ✅ `WebSearchResult` with `Citations` and `SearchQueries` | ❌ Consumed inside the agent loop, not exposed to your code |
| Full agent loop (file edits, code search, bash) | ❌ | ✅ Full CLI tool set |
| Multi-agent orchestration (MAF) | ❌ | ✅ `AsAIAgent()` with sequential/concurrent orchestrators |
| Session persistence & resume | ❌ | ✅ Built-in |
| Custom agents & skills | Via Needlr Agent Framework | ✅ Built-in |
| Binary size | ~0 (HTTP-only, no CLI bundled) | ~100MB+ (bundles the Copilot CLI) |
| Rate limits | Same (20 MCP req/min, premium request quota) | Same |

### When to use Needlr Copilot

- **You need structured web search results.** `WebSearchResult.Citations` gives your application code programmatic access to source URLs, titles, and character offsets — the SDK's agent loop consumes this data internally and only returns the agent's final text.
- **You need a tiered provider fallback.** `CopilotRateLimitException` integrates with `ITieredProviderSelector` so rate-limited queries fall through to alternative search providers (DuckDuckGo, Bing API). The SDK doesn't expose rate-limit exceptions for provider-level routing.
- **You want a lightweight `IChatClient`.** `CopilotChatClient` is a pure HTTP client (~0 binary overhead). The SDK bundles the entire Copilot CLI binary.
- **You're already using Needlr's agent framework.** `CopilotToolSet` produces `AIFunction` instances that plug directly into `IterativeLoopOptions.AdditionalTools`.

### When to use the GitHub Copilot SDK instead

- **You want the full agent experience.** The SDK runs the same agent loop as the Copilot CLI — file editing, code search, `web_fetch` (actual HTTP GET), bash/PowerShell, and more.
- **You need multi-agent orchestration.** The SDK integrates with Microsoft Agent Framework for sequential, concurrent, and handoff workflows.
- **You want session persistence.** The SDK persists conversation state to disk and supports resuming sessions.
- **You want officially supported auth.** The SDK handles token management, OAuth app flows, and BYOK (bring your own API keys from OpenAI, Anthropic, etc.).
- **You don't need programmatic access to search citations.** If the agent just needs to produce grounded research text, the SDK's built-in `web_search` tool gives the model the same citation data — you just can't inspect it from your code.

!!! info "Both use the same backend"
    Whether you call `web_search` via Needlr Copilot or the SDK, you're hitting the same GitHub Copilot MCP endpoint. The LLM decides whether to perform a Bing search — see the next section.

---

## Web Search Limitations

!!! warning "web_search is not a search engine"
    The Copilot `web_search` tool is **not a reliable web search provider**. It is an LLM-mediated endpoint that may or may not trigger a Bing search depending on the query.

**How it actually works:**

1. Your query is sent to the Copilot MCP server
2. An LLM on the server side evaluates the query
3. The LLM **decides** whether it needs web data or can answer from training knowledge
4. If it searches, the response includes structured `annotations` (citations with URLs) and `bing_searches` (the queries it ran)
5. If it doesn't search, the response is training-data-sourced text with no citations

**What this means in practice:**

| Query type | LLM behaviour | Citations? |
|---|---|---|
| Time-sensitive, specific, factual | Triggers Bing search | ✅ Real URLs with titles |
| General knowledge ("what is dependency injection?") | Answers from training data | ❌ None |
| General but LLM feels helpful | Answers from training data, may embed inline URLs from memory | ❌ No structured citations (inline URLs may be hallucinated) |

**There is no way to force a web search.** The LLM decides. Phrasing queries with time-sensitive language ("in 2026", "latest", "current") increases the likelihood of triggering a search, but it's never guaranteed.

**Implications for tiered providers:**

If your use case requires **guaranteed web search with verifiable sources**, use a real search API (Bing Web Search API, DuckDuckGo, Google Custom Search) as your primary provider. Copilot's `web_search` is better suited as a synthesis/fallback provider that sometimes includes grounded citations.

`WebSearchResult.Citations.Count == 0` does not mean the search failed — it means the LLM answered from training data. The `Text` may still be accurate; it's just not verifiable from a source URL.

