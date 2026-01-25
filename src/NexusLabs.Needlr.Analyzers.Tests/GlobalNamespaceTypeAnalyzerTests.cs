using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class GlobalNamespaceTypeAnalyzerTests
{
    // Use shared test attributes that match the real package
    private static string Attributes => NeedlrTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenNoPrefixesSet()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

// Global namespace type - should be included when no prefixes set
public class GlobalService { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenEmptyStringPrefixIncluded()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"", """" })]

// Global namespace type - should be included because empty string prefix is set
public class GlobalService { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenTypeIsInNamespace()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

namespace MyCompany.Services
{
    // Namespaced type - not affected by this analyzer
    public class NamespacedService { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenTypeHasDoNotInject()
    {
        var code = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

// Global namespace but marked DoNotInject
[DoNotInject]
public class GlobalService { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenTypeHasDoNotAutoRegister()
    {
        var code = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

// Global namespace but marked DoNotAutoRegister
[DoNotAutoRegister]
public class GlobalService { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenNoGenerateTypeRegistryAttribute()
    {
        var code = @"
// No [GenerateTypeRegistry] attribute - not using Needlr source gen
public class GlobalService { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenTypeIsAbstract()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

// Abstract types are not injectable
public abstract class AbstractGlobalService { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenTypeIsInterface()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

// Interfaces are not injectable as concrete types
public interface IGlobalService { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenGlobalNamespaceTypeWithInterface()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

public interface IService { }

// Global namespace type implementing interface - likely injectable
public class {|#0:GlobalService|} : IService { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.GlobalNamespaceTypeNotDiscovered, DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("GlobalService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenGlobalNamespaceTypeWithSingletonAttribute()
    {
        var code = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

// Global namespace type with [Singleton] attribute
[Singleton]
public class {|#0:GlobalSingletonService|} { }
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.GlobalNamespaceTypeNotDiscovered, DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("GlobalSingletonService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenGlobalNamespaceTypeWithDependencyConstructor()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

namespace MyCompany
{
    public interface IConfiguration { }
}

// Global namespace type with constructor dependency
public class {|#0:GlobalConfiguration|}
{
    public GlobalConfiguration(MyCompany.IConfiguration config) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GlobalNamespaceTypeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.GlobalNamespaceTypeNotDiscovered, DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("GlobalConfiguration")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
