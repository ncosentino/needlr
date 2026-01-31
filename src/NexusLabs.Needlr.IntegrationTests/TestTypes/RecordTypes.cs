namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Interface for testing record exclusion from auto-registration.
/// </summary>
public interface IRecordService
{
    string GetData();
}

/// <summary>
/// A record that implements an interface - should NOT be auto-registered as a service,
/// but SHOULD be discoverable as a plugin via IPluginFactory.
/// </summary>
public record RecordServiceImplementation(string Data) : IRecordService
{
    public string GetData() => Data;
}

/// <summary>
/// A record with required members - should NOT be auto-registered or discoverable as plugin.
/// </summary>
public record RecordWithRequiredMembers : IRecordService
{
    public required string Data { get; init; }
    public string GetData() => Data;
}

/// <summary>
/// A simple record with no interface - should NOT be auto-registered.
/// </summary>
public record SimpleDataRecord(string Name, int Value);

/// <summary>
/// A class service (not a record) - SHOULD be auto-registered for comparison.
/// </summary>
public sealed class ClassServiceImplementation : IRecordService
{
    public string GetData() => "ClassService";
}

/// <summary>
/// Base record for plugin discovery tests - similar to CacheConfiguration pattern.
/// </summary>
public abstract record PluginConfigurationRecord(string Name);

/// <summary>
/// Concrete record plugin A - should be discoverable via IPluginFactory.
/// </summary>
public sealed record PluginConfigurationRecordA() : PluginConfigurationRecord("RecordA");

/// <summary>
/// Concrete record plugin B - should be discoverable via IPluginFactory.
/// </summary>
public sealed record PluginConfigurationRecordB() : PluginConfigurationRecord("RecordB");
