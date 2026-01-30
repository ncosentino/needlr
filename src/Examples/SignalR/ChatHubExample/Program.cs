using ChatHubExample.Generated;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// Build the web application using Needlr's source-generated discovery
var webApplication = new Syringe()
    .UsingSourceGen()
    .BuildWebApplication();

// Map SignalR hubs using source-generated registration (AOT-safe)
webApplication.MapGeneratedHubs();

// Serve static files for the chat client
webApplication.UseDefaultFiles();
webApplication.UseStaticFiles();

Console.WriteLine("SignalR Chat Example");
Console.WriteLine("====================");
Console.WriteLine("Open http://localhost:5000 in your browser to start chatting.");
Console.WriteLine();

await webApplication.RunAsync();
