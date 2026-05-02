using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests that verify internal types and interfaces within the SAME assembly are
/// correctly registered by the source generator. This is the standard .NET DI
/// pattern: an internal implementation class and its internal interface both live
/// in the same project. The generated TypeRegistry is emitted into that same
/// compilation, so it CAN legally reference internal types via typeof().
///
/// Cross-assembly internal types (e.g., Avalonia's IContentPresenterHost from
/// Avalonia.Controls.dll) MUST be skipped because the generated code lives in the
/// consuming assembly and cannot access them (CS0122).
///
/// Regression context: commit 83c8bfb94 introduced a blanket filter that skipped
/// ALL internal interfaces — including same-assembly ones — breaking the standard
/// "internal class Foo : IFoo" DI pattern used by every non-trivial Needlr consumer.
/// These tests prevent that regression from ever recurring.
/// </summary>
public sealed class InternalInterfaceRegistrationTests
{
    [Fact]
    public void Generator_InternalClass_ImplementingInternalInterface_SameAssembly_RegistersInterface()
    {
        // This is the bread-and-butter DI pattern: an internal service class
        // implements an internal interface, both in the same project. The generated
        // TypeRegistry lives in the same compilation and CAN emit typeof(IMyService).
        //
        // If this test fails, every project with internal DI contracts (which is
        // most real-world .NET projects) will have broken DI resolution at runtime.
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    internal interface IAuthConfiguration
    {
        string EncryptionKey { get; }
    }

    internal sealed class AuthConfiguration : IAuthConfiguration
    {
        public string EncryptionKey => ""key"";
    }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .GetTypeRegistryOutput();

        // The generated TypeRegistry MUST include typeof(IAuthConfiguration)
        // in the interface array for the AuthConfiguration registration.
        Assert.Contains("typeof(global::TestApp.IAuthConfiguration)", output);
    }

    [Fact]
    public void Generator_InternalClass_ImplementingMultipleInternalInterfaces_SameAssembly_RegistersAllInterfaces()
    {
        // A type implementing multiple internal interfaces should have ALL of them
        // registered, not just the first one or none.
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    internal interface IReader { string Read(); }
    internal interface IWriter { void Write(string data); }

    internal sealed class FileStore : IReader, IWriter
    {
        public string Read() => """";
        public void Write(string data) { }
    }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .GetTypeRegistryOutput();

        Assert.Contains("typeof(global::TestApp.IReader)", output);
        Assert.Contains("typeof(global::TestApp.IWriter)", output);
    }

    [Fact]
    public void Generator_InternalClass_ImplementingPublicAndInternalInterfaces_SameAssembly_RegistersBoth()
    {
        // Mixed accessibility: a type implements both a public interface and an
        // internal interface. Both should be registered.
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    public interface IPublicService { void DoWork(); }
    internal interface IInternalMetrics { int GetCount(); }

    internal sealed class MyService : IPublicService, IInternalMetrics
    {
        public void DoWork() { }
        public int GetCount() => 42;
    }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .GetTypeRegistryOutput();

        Assert.Contains("typeof(global::TestApp.IPublicService)", output);
        Assert.Contains("typeof(global::TestApp.IInternalMetrics)", output);
    }
    [Fact]
    public void Generator_PublicClass_InheritingCrossAssemblyInternalInterface_SkipsInternalInterface()
    {
        // Avalonia scenario: a public class (e.g., MainWindow) inherits from a
        // framework base class (Window) that implements internal interfaces like
        // IContentPresenterHost. The generated code lives in the consuming app,
        // NOT in Avalonia.Controls.dll, so typeof(IContentPresenterHost) would
        // produce CS0122.
        //
        // We simulate this by creating a "framework" assembly with an internal
        // interface, then a "consumer" assembly whose type inherits from a
        // framework base class that implements that internal interface.
        var frameworkSource = @"
namespace Framework
{
    internal interface IInternalFrameworkHook { }

    public class BaseControl : IInternalFrameworkHook
    {
        // Framework base class implements internal interface —
        // consumer classes inheriting from BaseControl get it via AllInterfaces
    }
}";

        var consumerSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""ConsumerApp"" })]

namespace ConsumerApp
{
    public class MyControl : Framework.BaseControl
    {
        // Inherits Framework.IInternalFrameworkHook via AllInterfaces,
        // but it's internal to Framework.dll — must NOT appear in generated code.
    }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithAssemblyName("ConsumerApp")
            .WithCrossAssemblySource("Framework", frameworkSource)
            .WithSource(consumerSource)
            .GetTypeRegistryOutput();

        // The cross-assembly internal interface MUST NOT appear in the generated code
        Assert.DoesNotContain("IInternalFrameworkHook", output);
    }

    [Fact]
    public void Generator_CrossAssemblyInternalInterface_ProducesNoCompileErrors()
    {
        // Stronger assertion than string absence: verify the generated compilation
        // has NO CS0122 errors. This is what actually broke in the Avalonia scenario —
        // the generated typeof(IContentPresenterHost) produced a compile error because
        // the interface was internal to a different assembly.
        var frameworkSource = @"
namespace Framework
{
    internal interface IInternalFrameworkHook { }

    public class BaseControl : IInternalFrameworkHook { }
}";

        var consumerSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""ConsumerApp"" })]

namespace ConsumerApp
{
    public class MyControl : Framework.BaseControl { }
}";

        var errors = GeneratorTestRunner.ForTypeRegistry()
            .WithAssemblyName("ConsumerApp")
            .WithCrossAssemblySource("Framework", frameworkSource)
            .WithSource(consumerSource)
            .RunTypeRegistryGeneratorCompilationErrors();

        // No CS0122 ("type is inaccessible due to its protection level")
        Assert.Empty(errors);
    }

    [Fact]
    public void Generator_SameAssemblyInternalInterface_ProducesNoCompileErrors()
    {
        // The generated TypeRegistry referencing typeof(IAuthConfiguration) must compile
        // cleanly because the generated code is in the same assembly as the interface.
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    internal interface IAuthConfiguration { string Key { get; } }
    internal sealed class AuthConfiguration : IAuthConfiguration
    {
        public string Key => ""k"";
    }
}";

        var errors = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorCompilationErrors();

        Assert.Empty(errors);
    }
    [Fact]
    public void Generator_InternalPlugin_ImplementingInternalInterface_SameAssembly_RegistersInterface()
    {
        // Plugins are types with parameterless constructors and at least one non-system
        // interface. When the plugin and its interface are both internal in the same
        // assembly, the generated TypeRegistry MUST include the interface — same
        // accessibility logic as injectable types.
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    internal interface IMyPlugin { void Execute(); }

    internal sealed class MyPlugin : IMyPlugin
    {
        public void Execute() { }
    }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // The internal plugin interface MUST appear in either the injectable or plugin
        // section of the generated code
        Assert.Contains("typeof(global::TestApp.IMyPlugin)", output);
    }

    [Fact]
    public void Generator_Plugin_CrossAssemblyInternalInterface_Skipped()
    {
        // Avalonia scenario for the plugin path: a public plugin type inherits a framework
        // base class with internal interfaces. The generated code must NOT reference those
        // cross-assembly internal interfaces.
        var frameworkSource = @"
namespace Framework
{
    internal interface IInternalPluginHook { }
    public interface IPublicPlugin { }

    public abstract class PluginBase : IPublicPlugin, IInternalPluginHook { }
}";

        var consumerSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""ConsumerApp"" })]

namespace ConsumerApp
{
    public class MyPlugin : Framework.PluginBase { }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithAssemblyName("ConsumerApp")
            .WithCrossAssemblySource("Framework", frameworkSource)
            .WithSource(consumerSource)
            .RunTypeRegistryGenerator();

        // Cross-assembly internal interface MUST NOT appear
        Assert.DoesNotContain("IInternalPluginHook", output);
        // Public interface from framework MUST still appear
        Assert.Contains("typeof(global::Framework.IPublicPlugin)", output);
    }
    [Fact]
    public void Generator_Factory_InternalClassWithInternalInterface_SameAssembly_GeneratesFactory()
    {
        // Factory-generated types use GetRegisterableInterfaces during discovery.
        // When an internal class with [GenerateFactory] implements an internal
        // interface, the factory itself must still be generated correctly.
        // (Note: [GenerateFactory] types are NOT registered directly in the TypeRegistry —
        // the factory interface + implementation are generated instead.)
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    internal interface IProcessor { void Process(); }

    [GenerateFactory]
    internal sealed class Processor : IProcessor
    {
        public Processor(IProcessor other, string name) { }

        public void Process() { }
    }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // The factory interface should be generated for the internal type
        Assert.Contains("IProcessorFactory", output);
    }
    [Fact]
    public void Generator_Interceptor_InternalClassWithInternalInterface_SameAssembly_RegistersInterface()
    {
        // Intercepted services also use GetRegisterableInterfaces. When an internal
        // class with interceptors implements an internal interface, the interface
        // must still appear in the generated output.
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    internal interface IDataService { string GetData(); }

    internal class LoggingInterceptor : IMethodInterceptor
    {
        public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
            => invocation.ProceedAsync();
    }

    [Intercept<LoggingInterceptor>]
    internal sealed class DataService : IDataService
    {
        public string GetData() => ""data"";
    }
}";

        var output = GeneratorTestRunner.ForInterceptorWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.Contains("typeof(global::TestApp.IDataService)", output);
    }

    [Fact]
    public void Generator_InternalClass_IsRegisteredAsConcreteType()
    {
        // Internal classes in the same assembly MUST be registered. The generated
        // code is in the same compilation and can reference typeof(InternalClass).
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    internal interface ICacheProvider { }

    internal sealed class RedisCacheProvider : ICacheProvider { }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .GetTypeRegistryOutput();

        Assert.Contains("typeof(global::TestApp.RedisCacheProvider)", output);
    }
}
