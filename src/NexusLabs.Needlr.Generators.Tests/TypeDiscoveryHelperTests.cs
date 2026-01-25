using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class TypeDiscoveryHelperTests
{
    [Fact]
    public void IsInjectableType_ConcreteClass_ReturnsTrue()
    {
        var source = @"
namespace TestNamespace
{
    public class ConcreteService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ConcreteService");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.True(result);
    }

    [Fact]
    public void IsInjectableType_AbstractClass_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public abstract class AbstractService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.AbstractService");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_Interface_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public interface IService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.IService");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_StaticClass_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public static class StaticService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.StaticService");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_RecordType_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public record RecordType(string Value);
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.RecordType");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsPluginType_PlainRecord_ReturnsTrue()
    {
        // Records with parameterless constructors ARE valid plugins
        // They can be discovered via IPluginFactory.CreatePluginsFromAssemblies<T>()
        // They are just excluded from IsInjectableType (auto-registration into DI)
        var source = @"
namespace TestNamespace
{
    public interface IPlugin { }
    public record RecordPlugin : IPlugin;
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.RecordPlugin");

        var result = TypeDiscoveryHelper.IsPluginType(typeSymbol, isCurrentAssembly: true);

        Assert.True(result);
    }

    [Fact]
    public void IsPluginType_RecordWithRequiredProperty_ReturnsFalse()
    {
        // Records with required members that aren't satisfied by constructor are excluded
        var source = @"
namespace TestNamespace
{
    public interface IPlugin { }
    public record RecordPlugin : IPlugin
    {
        public required string Name { get; init; }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.RecordPlugin");

        var result = TypeDiscoveryHelper.IsPluginType(typeSymbol, isCurrentAssembly: true);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_NestedClass_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public class OuterClass
    {
        public class NestedClass { }
    }
}";
        // Nested class uses + in metadata name
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.OuterClass+NestedClass");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_Exception_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public class CustomException : System.Exception { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.CustomException");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_Attribute_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public class CustomAttribute : System.Attribute { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.CustomAttribute");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithDoNotAutoRegisterAttribute_ReturnsFalse()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface)]
    public sealed class DoNotAutoRegisterAttribute : System.Attribute { }
}

namespace TestNamespace
{
    [NexusLabs.Needlr.DoNotAutoRegister]
    public class MarkedService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.MarkedService");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_ImplementsInterfaceWithDoNotAutoRegister_ReturnsFalse()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface)]
    public sealed class DoNotAutoRegisterAttribute : System.Attribute { }
}

namespace TestNamespace
{
    [NexusLabs.Needlr.DoNotAutoRegister]
    public interface IMarkedInterface { }

    public class ServiceImplementingMarkedInterface : IMarkedInterface { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceImplementingMarkedInterface");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void GetRegisterableInterfaces_ExcludesSystemInterfaces()
    {
        var source = @"
namespace TestNamespace
{
    public interface ICustomInterface { }
    public class Service : ICustomInterface, System.IDisposable
    {
        public void Dispose() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.Service");

        var interfaces = TypeDiscoveryHelper.GetRegisterableInterfaces(typeSymbol);

        Assert.Single(interfaces);
        Assert.Equal("TestNamespace.ICustomInterface", interfaces[0].ToDisplayString());
    }

    [Fact]
    public void MatchesNamespacePrefix_WithMatchingPrefix_ReturnsTrue()
    {
        var source = @"
namespace MyCompany.Services
{
    public class MyService { }
}";
        var typeSymbol = GetTypeSymbol(source, "MyCompany.Services.MyService");
        var prefixes = new List<string> { "MyCompany", "AnotherPrefix" };

        var result = TypeDiscoveryHelper.MatchesNamespacePrefix(typeSymbol, prefixes);

        Assert.True(result);
    }

    [Fact]
    public void MatchesNamespacePrefix_WithNoMatchingPrefix_ReturnsFalse()
    {
        var source = @"
namespace OtherCompany.Services
{
    public class MyService { }
}";
        var typeSymbol = GetTypeSymbol(source, "OtherCompany.Services.MyService");
        var prefixes = new List<string> { "MyCompany", "AnotherPrefix" };

        var result = TypeDiscoveryHelper.MatchesNamespacePrefix(typeSymbol, prefixes);

        Assert.False(result);
    }

    [Fact]
    public void MatchesNamespacePrefix_WithNullPrefixes_ReturnsTrue()
    {
        var source = @"
namespace AnyNamespace
{
    public class MyService { }
}";
        var typeSymbol = GetTypeSymbol(source, "AnyNamespace.MyService");

        var result = TypeDiscoveryHelper.MatchesNamespacePrefix(typeSymbol, null);

        Assert.True(result);
    }

    [Fact]
    public void MatchesNamespacePrefix_WithEmptyPrefixes_ReturnsTrue()
    {
        var source = @"
namespace AnyNamespace
{
    public class MyService { }
}";
        var typeSymbol = GetTypeSymbol(source, "AnyNamespace.MyService");
        var prefixes = new List<string>();

        var result = TypeDiscoveryHelper.MatchesNamespacePrefix(typeSymbol, prefixes);

        Assert.True(result);
    }

    [Fact]
    public void GetFullyQualifiedName_ReturnsGlobalPrefixedName()
    {
        var source = @"
namespace TestNamespace
{
    public class TestService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.TestService");

        var result = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);

        Assert.Equal("global::TestNamespace.TestService", result);
    }

    [Fact]
    public void DetermineLifetime_ParameterlessConstructor_ReturnsSingleton()
    {
        var source = @"
namespace TestNamespace
{
    public class ServiceWithParameterlessCtor
    {
        public ServiceWithParameterlessCtor() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithParameterlessCtor");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Equal(GeneratorLifetime.Singleton, result);
    }

    [Fact]
    public void DetermineLifetime_InjectableParameters_ReturnsSingleton()
    {
        var source = @"
namespace TestNamespace
{
    public interface IDependency { }
    public class ServiceWithDependency
    {
        public ServiceWithDependency(IDependency dependency) { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithDependency");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Equal(GeneratorLifetime.Singleton, result);
    }

    [Fact]
    public void DetermineLifetime_StringParameter_ReturnsNull()
    {
        var source = @"
namespace TestNamespace
{
    public class ServiceWithStringParam
    {
        public ServiceWithStringParam(string value) { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithStringParam");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Null(result);
    }

    [Fact]
    public void DetermineLifetime_IntParameter_ReturnsNull()
    {
        var source = @"
namespace TestNamespace
{
    public class ServiceWithIntParam
    {
        public ServiceWithIntParam(int value) { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithIntParam");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Null(result);
    }

    [Fact]
    public void DetermineLifetime_DelegateParameter_ReturnsNull()
    {
        var source = @"
namespace TestNamespace
{
    public class ServiceWithDelegateParam
    {
        public ServiceWithDelegateParam(System.Action action) { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithDelegateParam");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Null(result);
    }

    [Fact]
    public void DetermineLifetime_CopyConstructor_ReturnsNull()
    {
        var source = @"
namespace TestNamespace
{
    public class ServiceWithCopyCtor
    {
        public ServiceWithCopyCtor(ServiceWithCopyCtor other) { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithCopyCtor");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Null(result);
    }

    [Fact]
    public void DetermineLifetime_WithDoNotInjectAttribute_ReturnsNull()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DoNotInjectAttribute : System.Attribute { }
}

namespace TestNamespace
{
    [NexusLabs.Needlr.DoNotInject]
    public class MarkedService
    {
        public MarkedService() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.MarkedService");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Null(result);
    }

    [Fact]
    public void DetermineLifetime_MultipleConstructors_UsesValidOne()
    {
        var source = @"
namespace TestNamespace
{
    public interface IDependency { }
    public class ServiceWithMultipleCtors
    {
        public ServiceWithMultipleCtors(string invalid) { }
        public ServiceWithMultipleCtors(IDependency valid) { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithMultipleCtors");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Equal(GeneratorLifetime.Singleton, result);
    }

    [Fact]
    public void HasDeferToContainerAttribute_WithAttribute_ReturnsTrue()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DeferToContainerAttribute : System.Attribute
    {
        public DeferToContainerAttribute(params System.Type[] constructorParameterTypes) { }
    }
}

namespace TestNamespace
{
    public interface IDependency { }

    [NexusLabs.Needlr.DeferToContainer(typeof(IDependency))]
    public partial class DeferredService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.DeferredService");

        var result = TypeDiscoveryHelper.HasDeferToContainerAttribute(typeSymbol);

        Assert.True(result);
    }

    [Fact]
    public void HasDeferToContainerAttribute_WithoutAttribute_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public class NormalService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.NormalService");

        var result = TypeDiscoveryHelper.HasDeferToContainerAttribute(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void GetDeferToContainerParameterTypes_WithSingleType_ReturnsType()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DeferToContainerAttribute : System.Attribute
    {
        public DeferToContainerAttribute(params System.Type[] constructorParameterTypes) { }
    }
}

namespace TestNamespace
{
    public interface IDependency { }

    [NexusLabs.Needlr.DeferToContainer(typeof(IDependency))]
    public partial class DeferredService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.DeferredService");

        var result = TypeDiscoveryHelper.GetDeferToContainerParameterTypes(typeSymbol);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("global::TestNamespace.IDependency", result[0]);
    }

    [Fact]
    public void GetDeferToContainerParameterTypes_WithMultipleTypes_ReturnsAllTypes()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DeferToContainerAttribute : System.Attribute
    {
        public DeferToContainerAttribute(params System.Type[] constructorParameterTypes) { }
    }
}

namespace TestNamespace
{
    public interface IDependency1 { }
    public interface IDependency2 { }
    public interface IDependency3 { }

    [NexusLabs.Needlr.DeferToContainer(typeof(IDependency1), typeof(IDependency2), typeof(IDependency3))]
    public partial class DeferredService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.DeferredService");

        var result = TypeDiscoveryHelper.GetDeferToContainerParameterTypes(typeSymbol);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("global::TestNamespace.IDependency1", result[0]);
        Assert.Equal("global::TestNamespace.IDependency2", result[1]);
        Assert.Equal("global::TestNamespace.IDependency3", result[2]);
    }

    [Fact]
    public void GetDeferToContainerParameterTypes_WithEmptyParams_ReturnsEmptyList()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DeferToContainerAttribute : System.Attribute
    {
        public DeferToContainerAttribute(params System.Type[] constructorParameterTypes) { }
    }
}

namespace TestNamespace
{
    [NexusLabs.Needlr.DeferToContainer]
    public partial class DeferredService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.DeferredService");

        var result = TypeDiscoveryHelper.GetDeferToContainerParameterTypes(typeSymbol);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetDeferToContainerParameterTypes_WithoutAttribute_ReturnsNull()
    {
        var source = @"
namespace TestNamespace
{
    public class NormalService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.NormalService");

        var result = TypeDiscoveryHelper.GetDeferToContainerParameterTypes(typeSymbol);

        Assert.Null(result);
    }

    [Fact]
    public void DetermineLifetime_WithDeferToContainerAttribute_ReturnsSingleton()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DeferToContainerAttribute : System.Attribute
    {
        public DeferToContainerAttribute(params System.Type[] constructorParameterTypes) { }
    }
}

namespace TestNamespace
{
    public interface IDependency { }

    [NexusLabs.Needlr.DeferToContainer(typeof(IDependency))]
    public partial class DeferredService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.DeferredService");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Equal(GeneratorLifetime.Singleton, result);
    }

    [Fact]
    public void GetDeferToContainerParameterTypes_WithGenericType_ReturnsFullyQualifiedName()
    {
        var source = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DeferToContainerAttribute : System.Attribute
    {
        public DeferToContainerAttribute(params System.Type[] constructorParameterTypes) { }
    }
}

namespace TestNamespace
{
    public interface ILogger<T> { }
    public interface ICacheProvider { }

    [NexusLabs.Needlr.DeferToContainer(typeof(ICacheProvider), typeof(ILogger<DeferredService>))]
    public partial class DeferredService { }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.DeferredService");

        var result = TypeDiscoveryHelper.GetDeferToContainerParameterTypes(typeSymbol);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("global::TestNamespace.ICacheProvider", result[0]);
        Assert.Equal("global::TestNamespace.ILogger<global::TestNamespace.DeferredService>", result[1]);
    }

    [Fact]
    public void IsInjectableType_OpenGenericType_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public interface IJob { }
    
    public class JobScheduler<TJob> where TJob : IJob
    {
        public JobScheduler() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.JobScheduler`1");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_OpenGenericWithMultipleParameters_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public class Repository<TEntity, TKey>
    {
        public Repository() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.Repository`2");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsPluginType_OpenGenericType_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public interface IPlugin { }
    
    public class GenericPlugin<T> : IPlugin
    {
        public GenericPlugin() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.GenericPlugin`1");

        var result = TypeDiscoveryHelper.IsPluginType(typeSymbol, isCurrentAssembly: true);

        Assert.False(result);
    }

    [Fact]
    public void DetermineLifetime_OpenGenericType_ReturnsNull()
    {
        var source = @"
namespace TestNamespace
{
    public class GenericService<T>
    {
        public GenericService() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.GenericService`1");

        var result = TypeDiscoveryHelper.DetermineLifetime(typeSymbol);

        Assert.Null(result);
    }

    [Fact]
    public void GetFullyQualifiedName_OpenGenericType_ReturnsOpenGenericSyntax()
    {
        // Verifies that if an open generic type were to be used, 
        // it would produce valid open generic syntax (MyClass<>) not invalid (MyClass<T>)
        var source = @"
namespace TestNamespace
{
    public class GenericService<T>
    {
        public GenericService() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.GenericService`1");

        var result = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);

        // Should produce open generic syntax, not GenericService<T>
        Assert.Equal("global::TestNamespace.GenericService<>", result);
    }

    [Fact]
    public void GetFullyQualifiedName_OpenGenericWithMultipleParams_ReturnsCorrectSyntax()
    {
        var source = @"
namespace TestNamespace
{
    public class Repository<TEntity, TKey>
    {
        public Repository() { }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.Repository`2");

        var result = TypeDiscoveryHelper.GetFullyQualifiedName(typeSymbol);

        // Should produce MyClass<,> for 2 type params
        Assert.Equal("global::TestNamespace.Repository<,>", result);
    }

    [Fact]
    public void GetFullyQualifiedName_ClosedGenericType_ReturnsFullType()
    {
        // Closed generics (with concrete type arguments) should keep their type arguments
        var source = @"
namespace TestNamespace
{
    public interface ILogger<T> { }
    public class MyService { }
    public class Consumer
    {
        public Consumer(ILogger<MyService> logger) { }
    }
}";
        var compilation = CreateCompilation(source);
        var consumerType = compilation.GetTypeByMetadataName("TestNamespace.Consumer")!;
        var constructor = consumerType.InstanceConstructors.First(c => c.Parameters.Length > 0);
        var loggerType = (INamedTypeSymbol)constructor.Parameters[0].Type;

        var result = TypeDiscoveryHelper.GetFullyQualifiedName(loggerType);

        // Should keep the concrete type argument
        Assert.Equal("global::TestNamespace.ILogger<global::TestNamespace.MyService>", result);
    }

    private static INamedTypeSymbol GetTypeSymbol(string source, string typeName)
    {
        var compilation = CreateCompilation(source);
        var typeSymbol = compilation.GetTypeByMetadataName(typeName);
        Assert.NotNull(typeSymbol);

        return typeSymbol;
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void IsInjectableType_WithRequiredProperty_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public class ServiceWithRequiredProperty
    {
        public required string Name { get; set; }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithRequiredProperty");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithRequiredField_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public class ServiceWithRequiredField
    {
        public required string name;
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithRequiredField");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithRequiredPropertyAndSetsRequiredMembersConstructor_ReturnsTrue()
    {
        var source = @"
using System.Diagnostics.CodeAnalysis;

namespace TestNamespace
{
    public class ServiceWithSetsRequiredMembers
    {
        public required string Name { get; set; }

        [SetsRequiredMembers]
        public ServiceWithSetsRequiredMembers()
        {
            Name = ""default"";
        }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithSetsRequiredMembers");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.True(result);
    }

    [Fact]
    public void IsInjectableType_WithInheritedRequiredProperty_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public class BaseWithRequired
    {
        public required string Name { get; set; }
    }

    public class DerivedService : BaseWithRequired
    {
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.DerivedService");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.False(result);
    }

    [Fact]
    public void IsPluginType_WithRequiredProperty_ReturnsFalse()
    {
        var source = @"
namespace TestNamespace
{
    public interface IPlugin { }

    public class PluginWithRequiredProperty : IPlugin
    {
        public required string Config { get; set; }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.PluginWithRequiredProperty");

        var result = TypeDiscoveryHelper.IsPluginType(typeSymbol, isCurrentAssembly: true);

        Assert.False(result);
    }

    [Fact]
    public void IsInjectableType_WithNoRequiredMembers_ReturnsTrue()
    {
        var source = @"
namespace TestNamespace
{
    public class ServiceWithOptionalProperty
    {
        public string? Name { get; set; }
    }
}";
        var typeSymbol = GetTypeSymbol(source, "TestNamespace.ServiceWithOptionalProperty");

        var result = TypeDiscoveryHelper.IsInjectableType(typeSymbol);

        Assert.True(result);
    }
}
