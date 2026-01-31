using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.Benchmarks.TestTypes;

[Options("Benchmark:Cache")]
public sealed class CacheOptions
{
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxItems { get; set; } = 1000;
    public bool EnableLogging { get; set; } = false;
}

[Options("Benchmark:Database")]
public sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    public int MaxConnections { get; set; } = 100;
    public int CommandTimeout { get; set; } = 30;
}
