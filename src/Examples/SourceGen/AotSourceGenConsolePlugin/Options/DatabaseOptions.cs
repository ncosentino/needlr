using NexusLabs.Needlr.Generators;

namespace AotSourceGenConsolePlugin.Options;

/// <summary>
/// Database configuration options.
/// The source generator automatically infers section name "Database" from the class name.
/// </summary>
[Options]
public class DatabaseOptions
{
    /// <summary>Gets or sets the connection string.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Gets or sets the command timeout in seconds.</summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>Gets or sets whether to enable retry logic.</summary>
    public bool EnableRetry { get; set; } = true;
}
