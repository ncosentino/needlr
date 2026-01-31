using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests that verify the source generator correctly handles decorator patterns.
/// Decorators (types that implement an interface AND take that interface as a 
/// constructor parameter) should be registered as themselves but NOT as the 
/// interface they decorate.
/// </summary>
public sealed class DecoratorPatternTests
{
    [Fact]
    public void DecoratorType_IsNotRegisteredAsDecoratedInterface()
    {
        // Arrange - A decorator that implements IService and takes IService in constructor
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IService
                {
                    string GetValue();
                }

                public sealed class ServiceImpl : IService
                {
                    public string GetValue() => "Original";
                }

                public sealed class ServiceDecorator : IService
                {
                    private readonly IService _inner;

                    public ServiceDecorator(IService inner)
                    {
                        _inner = inner;
                    }

                    public string GetValue() => $"Decorated({_inner.GetValue()})";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - ServiceDecorator should have Array.Empty<Type>() for interfaces
        Assert.Contains("typeof(global::TestNamespace.ServiceDecorator)", generatedCode);
        Assert.Contains("typeof(global::TestNamespace.ServiceImpl), [typeof(global::TestNamespace.IService)]", generatedCode);
        
        // The decorator should NOT be registered with IService
        var lines = generatedCode.Split('\n');
        var decoratorLine = lines.FirstOrDefault(l => l.Contains("ServiceDecorator"));
        Assert.NotNull(decoratorLine);
        Assert.Contains("Array.Empty<Type>()", decoratorLine);
    }

    [Fact]
    public void NonDecoratorWithDependency_IsRegisteredAsInterface()
    {
        // Arrange - A type that takes an interface but implements a DIFFERENT interface
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IDependency
                {
                    string GetData();
                }

                public interface IProcessor
                {
                    string Process();
                }

                public sealed class DependencyImpl : IDependency
                {
                    public string GetData() => "Data";
                }

                public sealed class Processor : IProcessor
                {
                    private readonly IDependency _dependency;

                    public Processor(IDependency dependency)
                    {
                        _dependency = dependency;
                    }

                    public string Process() => $"Processed: {_dependency.GetData()}";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Processor should be registered as IProcessor (not a decorator)
        Assert.Contains("typeof(global::TestNamespace.Processor), [typeof(global::TestNamespace.IProcessor)]", generatedCode);
    }

    [Fact]
    public void MultipleDecorators_AreAllExcludedFromInterfaceRegistration()
    {
        // Arrange - Multiple decorators for the same interface
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IService
                {
                    string GetValue();
                }

                public sealed class ServiceImpl : IService
                {
                    public string GetValue() => "Original";
                }

                public sealed class FirstDecorator : IService
                {
                    private readonly IService _inner;
                    public FirstDecorator(IService inner) { _inner = inner; }
                    public string GetValue() => $"First({_inner.GetValue()})";
                }

                public sealed class SecondDecorator : IService
                {
                    private readonly IService _inner;
                    public SecondDecorator(IService inner) { _inner = inner; }
                    public string GetValue() => $"Second({_inner.GetValue()})";
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - Both decorators should have Array.Empty<Type>() for interfaces
        var lines = generatedCode.Split('\n');
        
        var firstDecoratorLine = lines.FirstOrDefault(l => l.Contains("FirstDecorator"));
        Assert.NotNull(firstDecoratorLine);
        Assert.Contains("Array.Empty<Type>()", firstDecoratorLine);

        var secondDecoratorLine = lines.FirstOrDefault(l => l.Contains("SecondDecorator"));
        Assert.NotNull(secondDecoratorLine);
        Assert.Contains("Array.Empty<Type>()", secondDecoratorLine);

        // Only ServiceImpl should be registered as IService
        Assert.Contains("typeof(global::TestNamespace.ServiceImpl), [typeof(global::TestNamespace.IService)]", generatedCode);
    }

    [Fact]
    public void DecoratorImplementingMultipleInterfaces_OnlyExcludesDecoratedInterface()
    {
        // Arrange - A decorator that implements multiple interfaces but only decorates one
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IService
                {
                    string GetValue();
                }

                public interface ILoggable
                {
                    void Log();
                }

                public sealed class ServiceImpl : IService
                {
                    public string GetValue() => "Original";
                }

                public sealed class LoggingDecorator : IService, ILoggable
                {
                    private readonly IService _inner;

                    public LoggingDecorator(IService inner)
                    {
                        _inner = inner;
                    }

                    public string GetValue() => _inner.GetValue();
                    public void Log() { }
                }
            }
            """;

        // Act
        var generatedCode = RunGenerator(source);

        // Assert - LoggingDecorator should be registered as ILoggable but NOT as IService
        // The line looks like: new InjectableTypeInfo(typeof(global::TestNamespace.LoggingDecorator), [typeof(global::TestNamespace.ILoggable)], ...
        var lines = generatedCode.Split('\n');
        var decoratorLine = lines.FirstOrDefault(l => l.Contains("LoggingDecorator"));
        Assert.NotNull(decoratorLine);
        
        // Check that ILoggable is in the interfaces array (between the first [ and ])
        var bracketStart = decoratorLine.IndexOf('[');
        var bracketEnd = decoratorLine.IndexOf(']');
        Assert.True(bracketStart > 0 && bracketEnd > bracketStart, "Should have interfaces array");
        
        var interfacesArray = decoratorLine.Substring(bracketStart, bracketEnd - bracketStart + 1);
        
        // Should have ILoggable but not IService in the interfaces array
        Assert.Contains("ILoggable", interfacesArray);
        Assert.DoesNotContain("IService", interfacesArray);
    }

    private static string RunGenerator(string source)
    {
        return GeneratorTestRunner.ForDecoratorWithInlineTypes()
            .WithSource(source)
            .GetTypeRegistryOutput();
    }
}
