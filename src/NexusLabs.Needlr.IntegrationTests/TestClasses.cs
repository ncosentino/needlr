namespace NexusLabs.Needlr.IntegrationTests;

public interface IMyManualService
{
    void DoSomething();
}

public interface IMyManualService2
{
    void DoSomething();
}

public interface IMyAutomaticService
{
    void DoSomething();
}

public interface IMyAutomaticService2
{
    void DoSomething();
}

public sealed class MyAutomaticService :
    IMyAutomaticService,
    IMyAutomaticService2
{
    public void DoSomething()
    {
    }
}

[DoNotAutoRegister]
public sealed class MyManualService : 
    IMyManualService, 
    IMyManualService2
{
    public void DoSomething()
    {
    }
}

[DoNotAutoRegister]
public sealed class MyManualDecorator(
    IMyManualService _wrapped) :
    IMyManualService
{
    public void DoSomething()
    {
        _wrapped.DoSomething();
    }
}

public interface IInterfaceWithMultipleImplementations
{
}

public sealed class ImplementationA : IInterfaceWithMultipleImplementations
{
}

public sealed class ImplementationB : IInterfaceWithMultipleImplementations
{
}

public interface ITestServiceForDecoration
{
    string DoSomething();
}

[DoNotAutoRegister]
public sealed class TestServiceToBeDecorated : ITestServiceForDecoration
{
    public string DoSomething()
    {
        return "Original";
    }
}

[DoNotAutoRegister]
public sealed class TestServiceDecorator : ITestServiceForDecoration
{
    private readonly ITestServiceForDecoration _wrapped;

    public TestServiceDecorator(ITestServiceForDecoration wrapped)
    {
        _wrapped = wrapped;
    }

    public string DoSomething()
    {
        return $"Decorated: {_wrapped.DoSomething()}";
    }
}

// ============================================================================
// Plugin Test Types
// These are used to test parity between reflection-based and source-generated
// plugin discovery.
// ============================================================================

/// <summary>
/// A simple plugin interface for testing plugin discovery.
/// </summary>
public interface ITestPlugin
{
    string Name { get; }
    void Execute();
}

/// <summary>
/// A second plugin interface for testing multi-interface plugins.
/// </summary>
public interface ITestPluginWithOutput
{
    string GetOutput();
}

/// <summary>
/// A simple plugin implementation for testing.
/// Has a parameterless constructor and implements ITestPlugin.
/// </summary>
public sealed class SimpleTestPlugin : ITestPlugin
{
    public string Name => nameof(SimpleTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin that implements multiple interfaces.
/// </summary>
public sealed class MultiInterfaceTestPlugin : ITestPlugin, ITestPluginWithOutput
{
    public string Name => nameof(MultiInterfaceTestPlugin);
    public void Execute() { }
    public string GetOutput() => "MultiOutput";
}

/// <summary>
/// Another simple plugin for testing that multiple plugins are discovered.
/// </summary>
public sealed class AnotherTestPlugin : ITestPlugin
{
    public string Name => nameof(AnotherTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin with DoNotAutoRegister attribute - should still be discoverable as a plugin
/// but excluded from auto-registration as a service. Plugin discovery doesn't use this attribute.
/// </summary>
[DoNotAutoRegister]
public sealed class ManualRegistrationTestPlugin : ITestPlugin
{
    public string Name => nameof(ManualRegistrationTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin that requires constructor parameters - should NOT be discovered
/// by either reflection or source generation since plugins need parameterless constructors.
/// </summary>
public sealed class PluginWithDependency : ITestPlugin
{
    private readonly IMyAutomaticService _service;

    public PluginWithDependency(IMyAutomaticService service)
    {
        _service = service;
    }

    public string Name => nameof(PluginWithDependency);
    public void Execute() { }
}

/// <summary>
/// An abstract plugin base - should NOT be discovered as it's not concrete.
/// </summary>
public abstract class AbstractTestPlugin : ITestPlugin
{
    public abstract string Name { get; }
    public abstract void Execute();
}

/// <summary>
/// Concrete implementation of abstract plugin - SHOULD be discovered.
/// </summary>
public sealed class ConcreteTestPlugin : AbstractTestPlugin
{
    public override string Name => nameof(ConcreteTestPlugin);
    public override void Execute() { }
}