using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class KeyedServiceResolutionAnalyzerTests
{
    [Fact]
    public async Task NoKeyedServices_NoDiagnostic()
    {
        var source = """
            using System;
            
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
            
            namespace NexusLabs.Needlr.Generators
            {
                public sealed class GenerateTypeRegistryAttribute : Attribute { }
            }
            
            namespace TestNamespace
            {
                public interface IMyService { }
                
                public sealed class MyService : IMyService
                {
                    public MyService() { }
                }
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task KeyedService_UnknownKey_ReportsDiagnostic()
    {
        var source = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class FromKeyedServicesAttribute : Attribute
                {
                    public FromKeyedServicesAttribute(object key) => Key = key;
                    public object Key { get; }
                }
            }
            
            namespace NexusLabs.Needlr.Generators
            {
                public sealed class GenerateTypeRegistryAttribute : Attribute { }
            }
            
            namespace TestNamespace
            {
                public interface IPaymentProcessor { }
                
                public sealed class OrderService
                {
                    public OrderService({|#0:[FromKeyedServices("primary")] IPaymentProcessor processor|}) { }
                }
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.KeyedServiceUnknownKey)
            .WithLocation(0)
            .WithArguments("IPaymentProcessor", "primary");

        await VerifyAnalyzer.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task KeyedService_KnownKey_NoDiagnostic()
    {
        var source = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class FromKeyedServicesAttribute : Attribute
                {
                    public FromKeyedServicesAttribute(object key) => Key = key;
                    public object Key { get; }
                }
            }
            
            namespace NexusLabs.Needlr.Generators
            {
                public sealed class GenerateTypeRegistryAttribute : Attribute { }
            }
            
            namespace NexusLabs.Needlr
            {
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                public sealed class KeyedAttribute : Attribute
                {
                    public KeyedAttribute(string key) => Key = key;
                    public string Key { get; }
                }
            }
            
            namespace TestNamespace
            {
                public interface IPaymentProcessor { }
                
                // This registers "primary" key
                [NexusLabs.Needlr.Keyed("primary")]
                public sealed class StripeProcessor : IPaymentProcessor { }
                
                public sealed class OrderService
                {
                    // No diagnostic - "primary" key is discovered from StripeProcessor
                    public OrderService([FromKeyedServices("primary")] IPaymentProcessor processor) { }
                }
            }
            """;

        await VerifyAnalyzer.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task KeyedService_SomeKnown_SomeUnknown_OnlyReportsUnknown()
    {
        var source = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class FromKeyedServicesAttribute : Attribute
                {
                    public FromKeyedServicesAttribute(object key) => Key = key;
                    public object Key { get; }
                }
            }
            
            namespace NexusLabs.Needlr.Generators
            {
                public sealed class GenerateTypeRegistryAttribute : Attribute { }
            }
            
            namespace NexusLabs.Needlr
            {
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                public sealed class KeyedAttribute : Attribute
                {
                    public KeyedAttribute(string key) => Key = key;
                    public string Key { get; }
                }
            }
            
            namespace TestNamespace
            {
                public interface IPaymentProcessor { }
                
                [NexusLabs.Needlr.Keyed("primary")]
                public sealed class StripeProcessor : IPaymentProcessor { }
                
                public sealed class PaymentService
                {
                    public PaymentService(
                        [FromKeyedServices("primary")] IPaymentProcessor primary,
                        {|#0:[FromKeyedServices("backup")] IPaymentProcessor backup|}) { }
                }
            }
            """;

        // Only "backup" should report - "primary" is discovered from StripeProcessor
        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.KeyedServiceUnknownKey)
            .WithLocation(0)
            .WithArguments("IPaymentProcessor", "backup");

        await VerifyAnalyzer.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task KeyedService_WithoutSourceGen_NoDiagnostic()
    {
        var source = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class FromKeyedServicesAttribute : Attribute
                {
                    public FromKeyedServicesAttribute(object key) => Key = key;
                    public object Key { get; }
                }
            }
            
            namespace TestNamespace
            {
                public interface IPaymentProcessor { }
                
                public sealed class OrderService
                {
                    public OrderService([FromKeyedServices("primary")] IPaymentProcessor processor) { }
                }
            }
            """;

        // No GenerateTypeRegistry attribute, so analyzer should not report
        await VerifyAnalyzer.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task KeyedService_FrameworkType_NoDiagnostic()
    {
        var source = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class FromKeyedServicesAttribute : Attribute
                {
                    public FromKeyedServicesAttribute(object key) => Key = key;
                    public object Key { get; }
                }
            }
            
            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger { }
            }
            
            namespace NexusLabs.Needlr.Generators
            {
                public sealed class GenerateTypeRegistryAttribute : Attribute { }
            }
            
            namespace TestNamespace
            {
                public sealed class MyService
                {
                    public MyService([FromKeyedServices("custom")] Microsoft.Extensions.Logging.ILogger logger) { }
                }
            }
            """;

        // Framework types (Microsoft.Extensions.*) are skipped
        await VerifyAnalyzer.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task MultipleUnknownKeyedServices_ReportsMultipleDiagnostics()
    {
        var source = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class FromKeyedServicesAttribute : Attribute
                {
                    public FromKeyedServicesAttribute(object key) => Key = key;
                    public object Key { get; }
                }
            }
            
            namespace NexusLabs.Needlr.Generators
            {
                public sealed class GenerateTypeRegistryAttribute : Attribute { }
            }
            
            namespace TestNamespace
            {
                public interface IPaymentProcessor { }
                
                public sealed class PaymentService
                {
                    public PaymentService(
                        {|#0:[FromKeyedServices("primary")] IPaymentProcessor primary|},
                        {|#1:[FromKeyedServices("backup")] IPaymentProcessor backup|}) { }
                }
            }
            """;

        var expected1 = VerifyAnalyzer.Diagnostic(DiagnosticIds.KeyedServiceUnknownKey)
            .WithLocation(0)
            .WithArguments("IPaymentProcessor", "primary");
        var expected2 = VerifyAnalyzer.Diagnostic(DiagnosticIds.KeyedServiceUnknownKey)
            .WithLocation(1)
            .WithArguments("IPaymentProcessor", "backup");

        await VerifyAnalyzer.VerifyAnalyzerAsync(source, expected1, expected2);
    }

    [Fact]
    public async Task MixedKeyedAndUnkeyed_OnlyReportsUnknownKeyed()
    {
        var source = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class FromKeyedServicesAttribute : Attribute
                {
                    public FromKeyedServicesAttribute(object key) => Key = key;
                    public object Key { get; }
                }
            }
            
            namespace NexusLabs.Needlr.Generators
            {
                public sealed class GenerateTypeRegistryAttribute : Attribute { }
            }
            
            namespace TestNamespace
            {
                public interface IPaymentProcessor { }
                public interface ILogger { }
                
                public sealed class OrderService
                {
                    public OrderService(
                        {|#0:[FromKeyedServices("primary")] IPaymentProcessor processor|},
                        ILogger logger) { }
                }
            }
            """;

        var expected = VerifyAnalyzer.Diagnostic(DiagnosticIds.KeyedServiceUnknownKey)
            .WithLocation(0)
            .WithArguments("IPaymentProcessor", "primary");

        await VerifyAnalyzer.VerifyAnalyzerAsync(source, expected);
    }

    private static class VerifyAnalyzer
    {
        public static DiagnosticResult Diagnostic(string diagnosticId) =>
            CSharpAnalyzerVerifier<KeyedServiceResolutionAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);

        public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<KeyedServiceResolutionAnalyzer, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            };

            test.ExpectedDiagnostics.AddRange(expected);

            return test.RunAsync();
        }
    }
}
