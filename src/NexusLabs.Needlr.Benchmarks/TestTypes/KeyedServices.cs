using NexusLabs.Needlr;

namespace NexusLabs.Needlr.Benchmarks.TestTypes;

public interface IKeyedService
{
    string GetKey();
}

[Keyed("primary")]
public sealed class PrimaryKeyedService : IKeyedService
{
    public string GetKey() => "primary";
}

[Keyed("secondary")]
public sealed class SecondaryKeyedService : IKeyedService
{
    public string GetKey() => "secondary";
}

[Keyed("tertiary")]
public sealed class TertiaryKeyedService : IKeyedService
{
    public string GetKey() => "tertiary";
}
