namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Abstract base class for plugin discovery tests (like CacheConfiguration pattern).
/// This has NO interfaces - only base class inheritance.
/// NOTE: Uses classes, not records - records are NEVER auto-registered as plugins.
/// </summary>
public abstract class PluginConfigurationBase
{
    public string Name { get; }
    protected PluginConfigurationBase(string name) => Name = name;
}

/// <summary>
/// Concrete plugin configuration A - should be discoverable via base class.
/// </summary>
public sealed class PluginConfigurationA : PluginConfigurationBase
{
    public PluginConfigurationA() : base("ConfigA") { }
}

/// <summary>
/// Concrete plugin configuration B - should be discoverable via base class.
/// </summary>
public sealed class PluginConfigurationB : PluginConfigurationBase
{
    public PluginConfigurationB() : base("ConfigB") { }
}

/// <summary>
/// Abstract intermediate class to test multi-level inheritance.
/// </summary>
public abstract class SpecializedPluginConfigurationBase : PluginConfigurationBase
{
    public int Priority { get; }
    protected SpecializedPluginConfigurationBase(string name, int priority) : base(name) => Priority = priority;
}

/// <summary>
/// Concrete plugin with multi-level base class inheritance.
/// </summary>
public sealed class SpecializedPluginConfigurationC : SpecializedPluginConfigurationBase
{
    public SpecializedPluginConfigurationC() : base("ConfigC", 10) { }
}
