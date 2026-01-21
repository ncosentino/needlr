namespace HostBuilderIntegrationSourceGen;

// =============================================================================
// Services registered MANUALLY (not auto-discovered)
// =============================================================================

/// <summary>
/// A service that is registered manually in Program.cs.
/// This demonstrates that manual registrations work alongside Needlr discovery.
/// </summary>
public interface ICustomService
{
    string GetMessage();
}

public sealed class CustomService : ICustomService
{
    public string GetMessage() => "Hello from manually registered service! (SourceGen)";
}

// =============================================================================
// Services registered by NEEDLR (auto-discovered at compile time)
// =============================================================================

/// <summary>
/// A service that Needlr will auto-discover and register at compile time.
/// Needlr finds this because it implements an interface and is a concrete class.
/// </summary>
public interface IAutoDiscoveredService
{
    string GetAutoMessage();
}

public sealed class AutoDiscoveredService : IAutoDiscoveredService
{
    private int _counter;

    public string GetAutoMessage()
    {
        return $"Hello from Needlr auto-discovered service (SourceGen)! (call {++_counter})";
    }
}
