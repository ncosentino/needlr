using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class GlobalNamespaceTypeAnalyzerTests
{
    private const string NeedlrAttributes = @"
namespace NexusLabs.Needlr.Generators
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class GenerateTypeRegistryAttribute : System.Attribute
    {
        public string[]? IncludeNamespacePrefixes { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }
}

namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class SingletonAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DoNotInjectAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface)]
    public sealed class DoNotAutoRegisterAttribute : System.Attribute { }
}";

    [Fact]
    public async Task NoWarning_WhenNoPrefixesSet()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

// Global namespace type - should be included when no prefixes set
public class GlobalService { }
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
