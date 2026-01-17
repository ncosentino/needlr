using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NexusLabs.Needlr.Generators;

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

    private static INamedTypeSymbol GetTypeSymbol(string source, string typeName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var typeSymbol = compilation.GetTypeByMetadataName(typeName);
        Assert.NotNull(typeSymbol);

        return typeSymbol;
    }
}
