using HttpClientExample;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider(configuration);

Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine("  Needlr [HttpClientOptions] source-generated HttpClient registration example  ");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("Three named clients are source-generated from the [HttpClientOptions] records");
Console.WriteLine("in HttpClientOptions.cs. The plugin code that would normally hand-write five");
Console.WriteLine("AddHttpClient(...) blocks is gone — the records ARE the registration.");
Console.WriteLine();

var factory = provider.GetRequiredService<IHttpClientFactory>();

DumpClient(factory, "WebFetch",
    "inferred from type name (WebFetchHttpClientOptions) — appsettings overrides applied");

DumpClient(factory, "brave",
    "explicit attribute Name — overrides type-name inference");

DumpClient(factory, "Tavily",
    "timeout-only capability set — no UserAgent / BaseAddress wiring emitted");

Console.WriteLine();
Console.WriteLine("You can also resolve the underlying options record directly as IOptions<T>");
Console.WriteLine("alongside the HttpClient, for runtime access to the typed config:");
Console.WriteLine();

var webFetchOptions = provider.GetRequiredService<IOptions<WebFetchHttpClientOptions>>().Value;
Console.WriteLine($"  IOptions<WebFetchHttpClientOptions>.Value.Timeout   = {webFetchOptions.Timeout}");
Console.WriteLine($"  IOptions<WebFetchHttpClientOptions>.Value.UserAgent = \"{webFetchOptions.UserAgent}\"");
Console.WriteLine();
Console.WriteLine("Edit appsettings.json and re-run — the WebFetch values will change without");
Console.WriteLine("any rebuild.");

static void DumpClient(IHttpClientFactory factory, string name, string note)
{
    var client = factory.CreateClient(name);
    Console.WriteLine($"--- {name} ({note}) ---");
    Console.WriteLine($"  Timeout     : {client.Timeout}");
    Console.WriteLine($"  BaseAddress : {client.BaseAddress?.ToString() ?? "(unset)"}");
    Console.WriteLine($"  UserAgent   : {(client.DefaultRequestHeaders.UserAgent.Count == 0 ? "(unset)" : client.DefaultRequestHeaders.UserAgent.ToString())}");
    Console.WriteLine();
}
