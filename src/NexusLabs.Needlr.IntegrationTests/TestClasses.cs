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
/// A plugin with DoNotAutoRegister attribute - should NOT be discovered as a plugin.
/// This attribute is used to explicitly opt out of plugin discovery (e.g., when a plugin
/// should only be used with explicit manual registration or reflection-based approaches).
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

// ============================================================================
// Except<T>() Parity Test Types
// These test types verify that Except<T>() works identically for both
// reflection-based and source-generated type registration.
// ============================================================================

/// <summary>
/// Marker interface for types that should be excluded via Except&lt;T&gt;().
/// </summary>
public interface IExcludableService
{
}

/// <summary>
/// A service that implements IExcludableService and should be excluded
/// when Except&lt;IExcludableService&gt;() is used.
/// </summary>
public sealed class ExcludableServiceA : IExcludableService
{
}

/// <summary>
/// Another service implementing IExcludableService for testing.
/// </summary>
public sealed class ExcludableServiceB : IExcludableService
{
}

/// <summary>
/// A service that does NOT implement IExcludableService.
/// Should NOT be excluded by Except&lt;IExcludableService&gt;().
/// </summary>
public interface INonExcludableService
{
}

/// <summary>
/// Implementation that should remain registered after Except&lt;IExcludableService&gt;().
/// </summary>
public sealed class RegularServiceImpl : INonExcludableService
{
}

/// <summary>
/// A service that implements BOTH IExcludableService and INonExcludableService.
/// Should be excluded because it implements IExcludableService.
/// </summary>
public sealed class MixedExcludableService : IExcludableService, INonExcludableService
{
}

#region Base Class Plugin Discovery Tests

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

#endregion

#region Lifetime Override Tests (UsingOnlyAsTransient, UsingOnlyAsSingleton)

/// <summary>
/// Marker interface for job-like types that should be registered as transient.
/// Simulates IJob from Quartz.
/// </summary>
public interface ITestJob
{
    void Execute();
}

/// <summary>
/// A singleton service that also implements ITestJob.
/// Default lifetime is singleton (parameterless ctor), but should be transient when ITestJob is overridden.
/// </summary>
public sealed class SingletonJobService : ITestJob
{
    public void Execute() { }
}

/// <summary>
/// Another job implementation with singleton default.
/// </summary>
public sealed class AnotherSingletonJob : ITestJob
{
    public void Execute() { }
}

/// <summary>
/// A regular singleton service that does NOT implement ITestJob.
/// Should remain singleton even when ITestJob types are overridden to transient.
/// </summary>
public sealed class RegularSingletonService
{
    public void DoWork() { }
}

#endregion

#region DecoratorFor<T> Attribute Tests

/// <summary>
/// Interface for testing [DecoratorFor&lt;T&gt;] attribute-based decorator discovery.
/// </summary>
public interface IDecoratorForTestService
{
    string GetValue();
}

/// <summary>
/// Base service implementation that will be decorated via [DecoratorFor&lt;T&gt;] attributes.
/// </summary>
public sealed class DecoratorForTestServiceImpl : IDecoratorForTestService
{
    public string GetValue() => "Original";
}

/// <summary>
/// First decorator using [DecoratorFor&lt;T&gt;] attribute with Order = 1.
/// Lower order decorators are applied first (closer to the original service).
/// </summary>
[DecoratorFor<IDecoratorForTestService>(Order = 1)]
public sealed class DecoratorForFirstDecorator : IDecoratorForTestService
{
    private readonly IDecoratorForTestService _inner;

    public DecoratorForFirstDecorator(IDecoratorForTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"First({_inner.GetValue()})";
}

/// <summary>
/// Second decorator using [DecoratorFor&lt;T&gt;] attribute with Order = 2.
/// Higher order decorators wrap lower order ones.
/// Expected chain: SecondDecorator -> FirstDecorator -> Original
/// </summary>
[DecoratorFor<IDecoratorForTestService>(Order = 2)]
public sealed class DecoratorForSecondDecorator : IDecoratorForTestService
{
    private readonly IDecoratorForTestService _inner;

    public DecoratorForSecondDecorator(IDecoratorForTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Second({_inner.GetValue()})";
}

/// <summary>
/// Third decorator with Order = 0 (applied first, closest to the original service).
/// </summary>
[DecoratorFor<IDecoratorForTestService>(Order = 0)]
public sealed class DecoratorForZeroOrderDecorator : IDecoratorForTestService
{
    private readonly IDecoratorForTestService _inner;

    public DecoratorForZeroOrderDecorator(IDecoratorForTestService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Zero({_inner.GetValue()})";
}

/// <summary>
/// Interface for testing decorator alongside manually registered decorator.
/// Note: This tests manual decorator wiring, not [DecoratorFor] attribute.
/// </summary>
public interface IManualAndAttributeDecoratorService
{
    string GetValue();
}

/// <summary>
/// Base service for manual + attribute decorator testing.
/// </summary>
[DoNotAutoRegister]
public sealed class ManualAndAttributeDecoratorServiceImpl : IManualAndAttributeDecoratorService
{
    public string GetValue() => "Base";
}

/// <summary>
/// Manually applied decorator (not using DecoratorFor attribute).
/// </summary>
[DoNotAutoRegister]
public sealed class ManualDecorator : IManualAndAttributeDecoratorService
{
    private readonly IManualAndAttributeDecoratorService _inner;

    public ManualDecorator(IManualAndAttributeDecoratorService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Manual({_inner.GetValue()})";
}

/// <summary>
/// Manually applied attribute-style decorator.
/// Note: Not using [DecoratorFor] because the base service is [DoNotAutoRegister],
/// which would cause ApplyDecorators to fail.
/// </summary>
[DoNotAutoRegister]
public sealed class AttributeDecorator : IManualAndAttributeDecoratorService
{
    private readonly IManualAndAttributeDecoratorService _inner;

    public AttributeDecorator(IManualAndAttributeDecoratorService inner)
    {
        _inner = inner;
    }

    public string GetValue() => $"Attribute({_inner.GetValue()})";
}

#endregion

#region Record Exclusion Tests

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

#endregion

#region Record Plugin Discovery Tests

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

#endregion

// ============================================================================
// Interceptor Test Types
// These test types verify that interceptors are correctly discovered, proxied,
// and executed by the source generator.
// ============================================================================

#region Interceptor Test Types

/// <summary>
/// Interface for testing basic interceptor functionality with logging interceptor.
/// </summary>
public interface ILoggingInterceptedService
{
    string GetValue();
    string Process(string input);
    Task<string> GetValueAsync();
    void DoWork();
}

/// <summary>
/// Interface for testing modifying interceptor functionality.
/// </summary>
public interface IModifyingInterceptedService
{
    string GetValue();
    string Process(string input);
    Task<string> GetValueAsync();
    void DoWork();
}

/// <summary>
/// Interface for testing multi-interceptor chains.
/// </summary>
public interface IMultiInterceptedService
{
    string GetValue();
    string Process(string input);
    Task<string> GetValueAsync();
    void DoWork();
}

/// <summary>
/// A logging interceptor that captures method calls for testing.
/// </summary>
public sealed class TestLoggingInterceptor : IMethodInterceptor
{
    private readonly List<string> _log = new();

    public IReadOnlyList<string> Log => _log;

    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        _log.Add($"Before:{invocation.Method.Name}");
        var result = await invocation.ProceedAsync();
        _log.Add($"After:{invocation.Method.Name}");
        return result;
    }
}

/// <summary>
/// An interceptor that modifies return values.
/// </summary>
public sealed class TestModifyingInterceptor : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var result = await invocation.ProceedAsync();
        if (result is string str)
        {
            return $"[Modified:{str}]";
        }
        return result;
    }
}

/// <summary>
/// An interceptor that wraps results with order info.
/// </summary>
public sealed class TestOrderedInterceptor1 : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var result = await invocation.ProceedAsync();
        if (result is string str)
        {
            return $"Order1({str})";
        }
        return result;
    }
}

/// <summary>
/// Second ordered interceptor for testing chain order.
/// </summary>
public sealed class TestOrderedInterceptor2 : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var result = await invocation.ProceedAsync();
        if (result is string str)
        {
            return $"Order2({str})";
        }
        return result;
    }
}

/// <summary>
/// Service with class-level logging interceptor (non-modifying).
/// </summary>
[Intercept<TestLoggingInterceptor>]
public sealed class InterceptedTestService : ILoggingInterceptedService
{
    public string GetValue() => "Original";
    public string Process(string input) => $"Processed:{input}";
    public Task<string> GetValueAsync() => Task.FromResult("AsyncOriginal");
    public void DoWork() { }
}

/// <summary>
/// Service with multiple ordered interceptors.
/// Order 1 executes first (outermost), Order 2 executes second (inner).
/// Result: Order1(Order2(Original))
/// </summary>
[Intercept<TestOrderedInterceptor1>(Order = 1)]
[Intercept<TestOrderedInterceptor2>(Order = 2)]
public sealed class MultiInterceptedTestService : IMultiInterceptedService
{
    public string GetValue() => "Original";
    public string Process(string input) => $"Processed:{input}";
    public Task<string> GetValueAsync() => Task.FromResult("AsyncOriginal");
    public void DoWork() { }
}

/// <summary>
/// Service with modifying interceptor for testing return value modification.
/// </summary>
[Intercept<TestModifyingInterceptor>]
public sealed class ModifyingInterceptedTestService : IModifyingInterceptedService
{
    public string GetValue() => "Original";
    public string Process(string input) => $"Processed:{input}";
    public Task<string> GetValueAsync() => Task.FromResult("AsyncOriginal");
    public void DoWork() { }
}

#endregion

// ============================================================================
// Plugin Ordering Tests
// These test types verify that [PluginOrder] works identically for both
// reflection-based and source-generated plugin discovery.
// ============================================================================

#region Plugin Ordering Test Types

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

// ============================================================================
// RegisterAs<T> Attribute Test Types
// These test types verify that [RegisterAs<T>] works identically for both
// reflection-based and source-generated type registration.
// ============================================================================

/// <summary>
/// First interface for RegisterAs testing.
/// </summary>
public interface IRegisterAsReader
{
    string Read();
}

/// <summary>
/// Second interface for RegisterAs testing.
/// </summary>
public interface IRegisterAsWriter
{
    void Write(string data);
}

/// <summary>
/// Third interface that will NOT be registered when using [RegisterAs].
/// </summary>
public interface IRegisterAsLogger
{
    void Log(string message);
}

/// <summary>
/// Service that implements three interfaces but is only registered as one.
/// Using [RegisterAs&lt;IRegisterAsReader&gt;] means only IRegisterAsReader is resolvable,
/// not IRegisterAsWriter or IRegisterAsLogger.
/// </summary>
[RegisterAs<IRegisterAsReader>]
public sealed class SingleRegisterAsService : IRegisterAsReader, IRegisterAsWriter, IRegisterAsLogger
{
    public string Read() => "Read";
    public void Write(string data) { }
    public void Log(string message) { }
}

/// <summary>
/// Service registered as multiple specific interfaces (but not all).
/// [RegisterAs&lt;IRegisterAsReader&gt;] and [RegisterAs&lt;IRegisterAsWriter&gt;] means
/// both are resolvable, but NOT IRegisterAsLogger.
/// </summary>
[RegisterAs<IRegisterAsReader>]
[RegisterAs<IRegisterAsWriter>]
public sealed class MultipleRegisterAsService : IRegisterAsReader, IRegisterAsWriter, IRegisterAsLogger
{
    public string Read() => "MultiRead";
    public void Write(string data) { }
    public void Log(string message) { }
}

/// <summary>
/// Service with NO [RegisterAs] attribute - all interfaces should be registered.
/// Used as a control case.
/// </summary>
public sealed class NoRegisterAsService : IRegisterAsReader, IRegisterAsWriter
{
    public string Read() => "DefaultRead";
    public void Write(string data) { }
}

/// <summary>
/// Base interface for testing RegisterAs with interface hierarchies.
/// </summary>
public interface IRegisterAsBaseService
{
    string GetBase();
}

/// <summary>
/// Child interface that extends base.
/// </summary>
public interface IRegisterAsChildService : IRegisterAsBaseService
{
    string GetChild();
}

/// <summary>
/// Service implementing child interface, registered only as base.
/// The [RegisterAs&lt;IRegisterAsBaseService&gt;] means we register as base,
/// even though the class implements the child interface.
/// </summary>
[RegisterAs<IRegisterAsBaseService>]
public sealed class RegisterAsBaseOnlyService : IRegisterAsChildService
{
    public string GetBase() => "Base";
    public string GetChild() => "Child";
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
/// Allows tests to verify plugins execute in the expected order.
/// </summary>
public static class PluginExecutionTracker
{
    private static readonly List<string> _executionOrder = [];
    private static readonly object _lock = new();

    /// <summary>
    /// Records that a plugin has been executed.
    /// </summary>
    /// <param name="pluginTypeName">The type name of the executed plugin.</param>
    public static void RecordExecution(string pluginTypeName)
    {
        lock (_lock)
        {
            _executionOrder.Add(pluginTypeName);
        }
    }

    /// <summary>
    /// Gets the list of plugin type names in the order they were executed.
    /// </summary>
    public static IReadOnlyList<string> GetExecutionOrder()
    {
        lock (_lock)
        {
            return _executionOrder.ToArray();
        }
    }

    /// <summary>
    /// Clears the execution order. Call before each test.
    /// </summary>
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

#endregion

#region Open Generic Decorator Test Classes

/// <summary>
/// Open generic interface for testing [OpenDecoratorFor] attribute.
/// </summary>
public interface IOpenGenericHandler<T>
{
    string Handle(T message);
}

/// <summary>
/// Message type for testing open generic decorators.
/// </summary>
public sealed class OrderMessage
{
    public string OrderId { get; init; } = string.Empty;
}

/// <summary>
/// Message type for testing open generic decorators.
/// </summary>
public sealed class PaymentMessage
{
    public string PaymentId { get; init; } = string.Empty;
}

/// <summary>
/// Concrete implementation of IOpenGenericHandler for OrderMessage.
/// </summary>
[Singleton]
public sealed class OrderMessageHandler : IOpenGenericHandler<OrderMessage>
{
    public string Handle(OrderMessage message) => $"OrderHandler({message.OrderId})";
}

/// <summary>
/// Concrete implementation of IOpenGenericHandler for PaymentMessage.
/// </summary>
[Singleton]
public sealed class PaymentMessageHandler : IOpenGenericHandler<PaymentMessage>
{
    public string Handle(PaymentMessage message) => $"PaymentHandler({message.PaymentId})";
}

/// <summary>
/// Open generic decorator that decorates all IOpenGenericHandler implementations.
/// Uses [OpenDecoratorFor] to automatically decorate IHandler{OrderMessage} and IHandler{PaymentMessage}.
/// </summary>
[Generators.OpenDecoratorFor(typeof(IOpenGenericHandler<>), Order = 1)]
public sealed class LoggingOpenDecorator<T> : IOpenGenericHandler<T>
{
    private readonly IOpenGenericHandler<T> _inner;

    public LoggingOpenDecorator(IOpenGenericHandler<T> inner)
    {
        _inner = inner;
    }

    public string Handle(T message) => $"Logged({_inner.Handle(message)})";
}

/// <summary>
/// Second open generic decorator with higher order (wraps LoggingOpenDecorator).
/// </summary>
[Generators.OpenDecoratorFor(typeof(IOpenGenericHandler<>), Order = 2)]
public sealed class MetricsOpenDecorator<T> : IOpenGenericHandler<T>
{
    private readonly IOpenGenericHandler<T> _inner;

    public MetricsOpenDecorator(IOpenGenericHandler<T> inner)
    {
        _inner = inner;
    }

    public string Handle(T message) => $"Metrics({_inner.Handle(message)})";
}

#endregion