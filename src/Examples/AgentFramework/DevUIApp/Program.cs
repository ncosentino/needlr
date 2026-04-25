// DevUIApp — Demonstrates Needlr agents appearing in MAF DevUI
//
// Run this app and navigate to http://localhost:5250/devui to see the
// [NeedlrAiAgent]-declared agents (DataAssistant, SummaryAgent) listed
// in the DevUI web interface. The /v1/entities API returns them as
// discoverable keyed AIAgent services.
//
// NOTE: This example does not connect to a real LLM — it demonstrates
// the DevUI discovery and hosting infrastructure only.

using Microsoft.Agents.AI.DevUI;
using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.DevUI;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5250");

// Register a no-op IChatClient for DevUI discovery. In production, this
// would be a real provider (e.g., OpenAI, Azure OpenAI).
builder.Services.AddSingleton<IChatClient>(new DevUIPlaceholderChatClient());

// Bridge Needlr agents → DevUI keyed AIAgent registrations
builder.Services.AddNeedlrDevUI();

// MAF OpenAI hosting + DevUI infrastructure
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();

// Map hosting and DevUI endpoints
app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();

// Verification endpoint: list registered agents as JSON
app.MapGet("/", () =>
{
    return Results.Content("""
        <!DOCTYPE html>
        <html><body>
        <h1>Needlr DevUI Example</h1>
        <ul>
          <li><a href="/devui">/devui</a> — MAF DevUI web interface</li>
          <li><a href="/v1/entities">/v1/entities</a> — Agent discovery API (JSON)</li>
        </ul>
        </body></html>
        """, "text/html");
});

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Needlr DevUI Example                                       ║");
Console.WriteLine("║  Open http://localhost:5250/devui in your browser            ║");
Console.WriteLine("║  Or curl http://localhost:5250/v1/entities for JSON          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

app.Run();

/// <summary>
/// Placeholder <see cref="IChatClient"/> for DevUI discovery. Returns a canned
/// response explaining it's not connected to a real LLM. In production, replace
/// with a real provider registration.
/// </summary>
internal sealed class DevUIPlaceholderChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant,
                "This is a DevUI discovery placeholder. No LLM is connected."))
        {
            ModelId = "placeholder",
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent("This is a DevUI discovery placeholder.")],
        };
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
