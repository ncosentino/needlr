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

// ============================================================================
// Attribute-Based Plugin Test Types
// These are used to test parity between reflection-based and source-generated
// attribute-based plugin discovery.
// ============================================================================

/// <summary>
/// A custom attribute for marking special plugins.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SpecialPluginAttribute : Attribute
{
    public string Category { get; }

    public SpecialPluginAttribute(string category = "default")
    {
        Category = category;
    }
}

/// <summary>
/// Another custom attribute for testing multiple attributes on a plugin.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PriorityPluginAttribute : Attribute
{
    public int Priority { get; }

    public PriorityPluginAttribute(int priority)
    {
        Priority = priority;
    }
}

/// <summary>
/// A plugin marked with SpecialPluginAttribute.
/// </summary>
[SpecialPlugin("test")]
public sealed class SpecialTestPlugin : ITestPlugin
{
    public string Name => nameof(SpecialTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin marked with both SpecialPluginAttribute and PriorityPluginAttribute.
/// </summary>
[SpecialPlugin("multi")]
[PriorityPlugin(1)]
public sealed class MultiAttributeTestPlugin : ITestPlugin
{
    public string Name => nameof(MultiAttributeTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin with only PriorityPluginAttribute.
/// </summary>
[PriorityPlugin(2)]
public sealed class PriorityOnlyTestPlugin : ITestPlugin
{
    public string Name => nameof(PriorityOnlyTestPlugin);
    public void Execute() { }
}

/// <summary>
/// A plugin with no attributes (for negative testing).
/// </summary>
public sealed class NoAttributeTestPlugin : ITestPlugin
{
    public string Name => nameof(NoAttributeTestPlugin);
    public void Execute() { }
}

/// <summary>
/// Base class with SpecialPluginAttribute for testing inherited attributes.
/// </summary>
[SpecialPlugin("inherited")]
public abstract class SpecialPluginBase : ITestPlugin
{
    public abstract string Name { get; }
    public abstract void Execute();
}

/// <summary>
/// Derived class that should inherit SpecialPluginAttribute from its base.
/// </summary>
public sealed class InheritedAttributeTestPlugin : SpecialPluginBase
{
    public override string Name => nameof(InheritedAttributeTestPlugin);
    public override void Execute() { }
}

// ============================================================================
// Decorator Pattern Test Types
// These test types verify that decorator patterns are handled correctly by
// both reflection and source-gen paths. Decorators should be registered as
// themselves but NOT as the interface they decorate to avoid circular dependencies.
// ============================================================================

/// <summary>
/// Interface for testing decorator pattern auto-registration behavior.
/// </summary>
public interface IDecoratorTestService
{
    string GetValue();
}

/// <summary>
/// The "inner" implementation that should be registered as IDecoratorTestService.
/// </summary>
public sealed class DecoratorTestServiceImpl : IDecoratorTestService
{
    public string GetValue() => "Original";
}

/// <summary>
/// A decorator that implements IDecoratorTestService AND takes IDecoratorTestService
/// as a constructor parameter. This should be registered as itself only, NOT as
/// IDecoratorTestService, to avoid circular dependency issues.
/// </summary>
public sealed class DecoratorTestServiceDecorator : IDecoratorTestService
{
    private readonly IDecoratorTestService _inner;

    public DecoratorTestServiceDecorator(IDecoratorTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Decorated({_inner.GetValue()})";
}

/// <summary>
/// A second decorator to test multiple decorators don't cause issues.
/// </summary>
public sealed class DecoratorTestServiceSecondDecorator : IDecoratorTestService
{
    private readonly IDecoratorTestService _inner;

    public DecoratorTestServiceSecondDecorator(IDecoratorTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"SecondDecorated({_inner.GetValue()})";
}

/// <summary>
/// A class that takes an interface in the constructor but does NOT implement it.
/// This is NOT a decorator and should be registered normally with all its interfaces.
/// </summary>
public interface INonDecoratorTestService
{
    string Process();
}

public interface INonDecoratorDependency
{
    string GetData();
}

public sealed class NonDecoratorDependencyImpl : INonDecoratorDependency
{
    public string GetData() => "Data";
}

public sealed class NonDecoratorTestService : INonDecoratorTestService
{
    private readonly INonDecoratorDependency _dependency;

    public NonDecoratorTestService(INonDecoratorDependency dependency)
    {
        _dependency = dependency;
    }

    public string Process() => $"Processed: {_dependency.GetData()}";
}

// ============================================================================
// Hosting Plugin Test Types
// These test types verify parity between reflection and source-gen for
// hosting plugin discovery (IHostApplicationBuilderPlugin and IHostPlugin).
// ============================================================================

/// <summary>
/// Test implementation of IHostApplicationBuilderPlugin for parity testing.
/// </summary>
public sealed class TestHostApplicationBuilderPlugin : Hosting.IHostApplicationBuilderPlugin
{
    public bool WasConfigured { get; private set; }

    public void Configure(Hosting.HostApplicationBuilderPluginOptions options)
    {
        WasConfigured = true;
    }
}

/// <summary>
/// Second test implementation for testing multiple plugin discovery.
/// </summary>
public sealed class SecondTestHostApplicationBuilderPlugin : Hosting.IHostApplicationBuilderPlugin
{
    public void Configure(Hosting.HostApplicationBuilderPluginOptions options)
    {
    }
}

/// <summary>
/// Test implementation of IHostPlugin for parity testing.
/// </summary>
public sealed class TestHostPlugin : Hosting.IHostPlugin
{
    public bool WasConfigured { get; private set; }

    public void Configure(Hosting.HostPluginOptions options)
    {
        WasConfigured = true;
    }
}

/// <summary>
/// Second test implementation for testing multiple plugin discovery.
/// </summary>
public sealed class SecondTestHostPlugin : Hosting.IHostPlugin
{
    public void Configure(Hosting.HostPluginOptions options)
    {
    }
}