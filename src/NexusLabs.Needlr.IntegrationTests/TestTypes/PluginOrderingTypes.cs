namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Test plugin interface for ordering parity tests.
/// </summary>
public interface IOrderedTestPlugin
{
    string Name { get; }
}

/// <summary>
/// Plugin with Order = -100 (should execute first).
/// </summary>
[PluginOrder(-100)]
public sealed class FirstOrderPlugin : IOrderedTestPlugin
{
    public string Name => nameof(FirstOrderPlugin);
}

/// <summary>
/// Plugin with Order = -50.
/// </summary>
[PluginOrder(-50)]
public sealed class SecondOrderPlugin : IOrderedTestPlugin
{
    public string Name => nameof(SecondOrderPlugin);
}

/// <summary>
/// Plugin with default Order = 0 (no attribute).
/// </summary>
public sealed class DefaultOrderPlugin : IOrderedTestPlugin
{
    public string Name => nameof(DefaultOrderPlugin);
}

/// <summary>
/// Another plugin with default Order = 0 to test alphabetical sorting.
/// Named with 'A' prefix to come before 'Default' alphabetically.
/// </summary>
public sealed class AnotherDefaultOrderPlugin : IOrderedTestPlugin
{
    public string Name => nameof(AnotherDefaultOrderPlugin);
}

/// <summary>
/// Plugin with Order = 50.
/// </summary>
[PluginOrder(50)]
public sealed class LaterOrderPlugin : IOrderedTestPlugin
{
    public string Name => nameof(LaterOrderPlugin);
}

/// <summary>
/// Plugin with Order = 100 (should execute last).
/// </summary>
[PluginOrder(100)]
public sealed class LastOrderPlugin : IOrderedTestPlugin
{
    public string Name => nameof(LastOrderPlugin);
}

/// <summary>
/// Static class to track the order in which IServiceCollectionPlugin.Configure() is called.
/// </summary>
public static class PluginExecutionTracker
{
    private static readonly List<string> _executionOrder = [];
    private static readonly object _lock = new();

    public static void RecordExecution(string pluginTypeName)
    {
        lock (_lock)
        {
            _executionOrder.Add(pluginTypeName);
        }
    }

    public static IReadOnlyList<string> GetExecutionOrder()
    {
        lock (_lock)
        {
            return _executionOrder.ToArray();
        }
    }

    public static void Reset()
    {
        lock (_lock)
        {
            _executionOrder.Clear();
        }
    }
}

/// <summary>
/// Interface for ordered service collection plugins for testing.
/// </summary>
public interface IOrderedServiceCollectionPlugin : IServiceCollectionPlugin
{
}

/// <summary>
/// Service collection plugin with Order = -100 (executes first).
/// </summary>
[PluginOrder(-100)]
public sealed class OrderMinus100Plugin : IOrderedServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        PluginExecutionTracker.RecordExecution(nameof(OrderMinus100Plugin));
    }
}

/// <summary>
/// Service collection plugin with Order = -50.
/// </summary>
[PluginOrder(-50)]
public sealed class OrderMinus50Plugin : IOrderedServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        PluginExecutionTracker.RecordExecution(nameof(OrderMinus50Plugin));
    }
}

/// <summary>
/// Service collection plugin with default Order = 0.
/// Named with 'A' prefix to come first alphabetically among Order=0 plugins.
/// </summary>
public sealed class ADefaultOrderPlugin : IOrderedServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        PluginExecutionTracker.RecordExecution(nameof(ADefaultOrderPlugin));
    }
}

/// <summary>
/// Service collection plugin with default Order = 0.
/// Named with 'Z' prefix to come last alphabetically among Order=0 plugins.
/// </summary>
public sealed class ZDefaultOrderPlugin : IOrderedServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        PluginExecutionTracker.RecordExecution(nameof(ZDefaultOrderPlugin));
    }
}

/// <summary>
/// Service collection plugin with Order = 50.
/// </summary>
[PluginOrder(50)]
public sealed class Order50Plugin : IOrderedServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        PluginExecutionTracker.RecordExecution(nameof(Order50Plugin));
    }
}

/// <summary>
/// Service collection plugin with Order = 100 (executes last).
/// </summary>
[PluginOrder(100)]
public sealed class Order100Plugin : IOrderedServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        PluginExecutionTracker.RecordExecution(nameof(Order100Plugin));
    }
}
