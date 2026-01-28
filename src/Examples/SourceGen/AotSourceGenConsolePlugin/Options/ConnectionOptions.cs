// Example of named options - multiple configurations of the same type
using NexusLabs.Needlr.Generators;

namespace AotSourceGenConsolePlugin.Options;

/// <summary>
/// Named options example - multiple database configurations.
/// Each [Options] attribute creates a separate named configuration.
/// </summary>
/// <remarks>
/// Usage in code:
/// <code>
/// // Inject named options
/// public class MyService(IOptionsSnapshot&lt;ConnectionOptions&gt; options)
/// {
///     var primary = options.Get("Primary");
///     var replica = options.Get("Replica");
/// }
/// </code>
/// 
/// Configuration in appsettings.json:
/// <code>
/// {
///   "Connections": {
///     "Primary": {
///       "Host": "primary.example.com",
///       "Port": 5432,
///       "Database": "maindb"
///     },
///     "Replica": {
///       "Host": "replica.example.com",
///       "Port": 5432,
///       "Database": "maindb"
///     }
///   }
/// }
/// </code>
/// </remarks>
[Options("Connections:Primary", Name = "Primary")]
[Options("Connections:Replica", Name = "Replica")]
public class ConnectionOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>
    /// Builds a connection string from the options.
    /// </summary>
    public string ToConnectionString()
        => $"Host={Host};Port={Port};Database={Database}"
           + (Username != null ? $";Username={Username}" : "")
           + (Password != null ? $";Password={Password}" : "");
}
