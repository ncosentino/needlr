using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests that verify the source generator correctly handles [DecoratorFor&lt;TService&gt;] attributes.
/// These tests ensure decorators marked with the attribute are discovered and the ApplyDecorators
/// method is generated correctly.
/// </summary>
public sealed class DecoratorForAttributeTests
{
    [Fact]
    public void DecoratorForAttribute_SingleDecorator_GeneratesApplyDecoratorsMethod()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IMyService
                {
                    string GetValue();
                }

                public class MyService : IMyService
                {
                    public string GetValue() => "Original";
                }

                [DecoratorFor<IMyService>(Order = 1)]
                public class MyDecorator : IMyService
                {
                    private readonly IMyService _inner;
                    public MyDecorator(IMyService inner) => _inner = inner;
                    public string GetValue() => $"Decorated({_inner.GetValue()})";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - ApplyDecorators method should be generated with the decorator
        Assert.Contains("ApplyDecorators", generatedCode);
        Assert.Contains("services.AddDecorator<global::TestNamespace.IMyService, global::TestNamespace.MyDecorator>", generatedCode);
        Assert.Contains("Order: 1", generatedCode);
    }

    [Fact]
    public void DecoratorForAttribute_MultipleDecoratorsWithOrder_GeneratesInCorrectOrder()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IMyService
                {
                    string GetValue();
                }

                public class MyService : IMyService
                {
                    public string GetValue() => "Original";
                }

                [DecoratorFor<IMyService>(Order = 2)]
                public class SecondDecorator : IMyService
                {
                    private readonly IMyService _inner;
                    public SecondDecorator(IMyService inner) => _inner = inner;
                    public string GetValue() => $"Second({_inner.GetValue()})";
                }

                [DecoratorFor<IMyService>(Order = 1)]
                public class FirstDecorator : IMyService
                {
                    private readonly IMyService _inner;
                    public FirstDecorator(IMyService inner) => _inner = inner;
                    public string GetValue() => $"First({_inner.GetValue()})";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Both decorators should be generated, FirstDecorator before SecondDecorator
        Assert.Contains("ApplyDecorators", generatedCode);

        var firstIndex = generatedCode.IndexOf("FirstDecorator");
        var secondIndex = generatedCode.IndexOf("SecondDecorator");

        Assert.True(firstIndex > 0, "FirstDecorator should be in generated code");
        Assert.True(secondIndex > 0, "SecondDecorator should be in generated code");
        Assert.True(firstIndex < secondIndex, "FirstDecorator (Order=1) should appear before SecondDecorator (Order=2)");
    }

    [Fact]
    public void DecoratorForAttribute_MultipleServicesDecorated_GeneratesGroupedByService()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IServiceA
                {
                    string GetA();
                }

                public interface IServiceB
                {
                    string GetB();
                }

                public class ServiceA : IServiceA
                {
                    public string GetA() => "A";
                }

                public class ServiceB : IServiceB
                {
                    public string GetB() => "B";
                }

                [DecoratorFor<IServiceA>(Order = 1)]
                public class DecoratorA : IServiceA
                {
                    private readonly IServiceA _inner;
                    public DecoratorA(IServiceA inner) => _inner = inner;
                    public string GetA() => $"DecA({_inner.GetA()})";
                }

                [DecoratorFor<IServiceB>(Order = 1)]
                public class DecoratorB : IServiceB
                {
                    private readonly IServiceB _inner;
                    public DecoratorB(IServiceB inner) => _inner = inner;
                    public string GetB() => $"DecB({_inner.GetB()})";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Both service decorators should be generated
        Assert.Contains("Decorators for global::TestNamespace.IServiceA", generatedCode);
        Assert.Contains("Decorators for global::TestNamespace.IServiceB", generatedCode);
        Assert.Contains("AddDecorator<global::TestNamespace.IServiceA, global::TestNamespace.DecoratorA>", generatedCode);
        Assert.Contains("AddDecorator<global::TestNamespace.IServiceB, global::TestNamespace.DecoratorB>", generatedCode);
    }

    [Fact]
    public void DecoratorForAttribute_DecoratorExcludedFromInterfaceRegistration()
    {
        // Arrange
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IMyService
                {
                    string GetValue();
                }

                public class MyService : IMyService
                {
                    public string GetValue() => "Original";
                }

                [DecoratorFor<IMyService>(Order = 1)]
                public class MyDecorator : IMyService
                {
                    private readonly IMyService _inner;
                    public MyDecorator(IMyService inner) => _inner = inner;
                    public string GetValue() => $"Decorated({_inner.GetValue()})";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - MyDecorator should NOT be registered with IMyService interface
        // (either via Array.Empty<Type>() or by virtue of decorator pattern detection)
        var lines = generatedCode.Split('\n');
        
        // Find any line with MyDecorator and IMyService together in interface registration
        // This would look like: typeof(MyDecorator), [typeof(IMyService)]
        var invalidRegistration = lines.Any(l => 
            l.Contains("MyDecorator") && 
            l.Contains("InjectableTypeInfo") && 
            l.Contains("IMyService") &&
            l.Contains("[typeof"));
        
        Assert.False(invalidRegistration, "Decorator should NOT be registered with IMyService interface");

        // MyService should still be registered as IMyService
        Assert.Contains("typeof(global::TestNamespace.MyService), [typeof(global::TestNamespace.IMyService)]", generatedCode);
    }

    [Fact]
    public void DecoratorForAttribute_SameDecoratorMultipleServices_GeneratesMultipleEntries()
    {
        // Arrange - A decorator that decorates multiple services
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IServiceA
                {
                    string GetA();
                }

                public interface IServiceB
                {
                    string GetB();
                }

                public class ServiceA : IServiceA
                {
                    public string GetA() => "A";
                }

                public class ServiceB : IServiceB
                {
                    public string GetB() => "B";
                }

                [DecoratorFor<IServiceA>(Order = 1)]
                [DecoratorFor<IServiceB>(Order = 2)]
                public class MultiDecorator : IServiceA, IServiceB
                {
                    private readonly IServiceA _innerA;
                    private readonly IServiceB _innerB;
                    public MultiDecorator(IServiceA innerA, IServiceB innerB)
                    {
                        _innerA = innerA;
                        _innerB = innerB;
                    }
                    public string GetA() => $"Multi({_innerA.GetA()})";
                    public string GetB() => $"Multi({_innerB.GetB()})";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Both decorator registrations should be generated
        Assert.Contains("AddDecorator<global::TestNamespace.IServiceA, global::TestNamespace.MultiDecorator>", generatedCode);
        Assert.Contains("AddDecorator<global::TestNamespace.IServiceB, global::TestNamespace.MultiDecorator>", generatedCode);
    }

    private static string RunGenerator(string source)
    {
        // Create the attribute source with all required types
        var attributeSource = """
            namespace NexusLabs.Needlr
            {
                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class DoNotAutoRegisterAttribute : System.Attribute { }

                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
                public sealed class DecoratorForAttribute<TService> : System.Attribute
                    where TService : class
                {
                    public int Order { get; set; } = 0;
                    public System.Type ServiceType => typeof(TService);
                }
            }

            namespace NexusLabs.Needlr.Generators
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly)]
                public sealed class GenerateTypeRegistryAttribute : System.Attribute
                {
                    public string[]? IncludeNamespacePrefixes { get; set; }
                    public bool IncludeSelf { get; set; } = true;
                }

                public enum InjectableLifetime
                {
                    Singleton = 0,
                    Scoped = 1,
                    Transient = 2
                }

                public readonly struct InjectableTypeInfo
                {
                    public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces)
                        : this(type, interfaces, null) { }

                    public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces, InjectableLifetime? lifetime)
                    {
                        Type = type;
                        Interfaces = interfaces;
                        Lifetime = lifetime;
                    }

                    public System.Type Type { get; }
                    public System.Collections.Generic.IReadOnlyList<System.Type> Interfaces { get; }
                    public InjectableLifetime? Lifetime { get; }
                }

                public readonly struct PluginTypeInfo
                {
                    public PluginTypeInfo(System.Type pluginType, System.Collections.Generic.IReadOnlyList<System.Type> pluginInterfaces, System.Func<object> factory)
                    {
                        PluginType = pluginType;
                        PluginInterfaces = pluginInterfaces;
                        Factory = factory;
                    }

                    public System.Type PluginType { get; }
                    public System.Collections.Generic.IReadOnlyList<System.Type> PluginInterfaces { get; }
                    public System.Func<object> Factory { get; }
                }

                public static class NeedlrSourceGenBootstrap
                {
                    public static void Register(
                        System.Func<System.Collections.Generic.IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
                        System.Func<System.Collections.Generic.IReadOnlyList<PluginTypeInfo>> pluginTypeProvider,
                        System.Action<object>? decoratorApplier = null)
                    {
                    }
                }
            }
            """;

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(source)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .OrderBy(t => t.FilePath)
            .ToList();

        if (generatedTrees.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n\n", generatedTrees.Select(t => t.GetText().ToString()));
    }
}
