using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class CollectionResolutionAnalyzerTests
{
    private const string NeedlrAttributes = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class SingletonAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ScopedAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TransientAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class DoNotInjectAttribute : System.Attribute { }
}

namespace NexusLabs.Needlr.Generators
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public class GenerateTypeRegistryAttribute : System.Attribute
    {
        public string[]? IncludeNamespacePrefixes { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }
}";

    [Fact]
    public async Task NoWarning_WhenGenerateTypeRegistryNotPresent()
    {
        var code = @"
using System.Collections.Generic;

public class Service
{
    public Service(IEnumerable<IPlugin> plugins) { }
}

public interface IPlugin { }
";

        var test = new CSharpAnalyzerTest<CollectionResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenCollectionHasImplementations()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public interface IPlugin { }

public class PluginA : IPlugin { }
public class PluginB : IPlugin { }

[Singleton]
public class Consumer
{
    public Consumer(IEnumerable<IPlugin> plugins) { }
}
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<CollectionResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_WhenCollectionHasNoImplementations()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public interface IMissingPlugin { }

public class Consumer
{
    public Consumer({|#0:IEnumerable<IMissingPlugin>|} plugins) { }
}
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<CollectionResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.CollectionResolutionEmpty, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("IMissingPlugin")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenCollectionIsFrameworkType()
    {
        var code = @"
using System;
using System.Collections.Generic;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public class Consumer
{
    public Consumer(IEnumerable<IDisposable> disposables) { }
}
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<CollectionResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenCollectionOfConcreteTypes()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public class MyService { }

public class Consumer
{
    public Consumer(IEnumerable<MyService> services) { }
}
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<CollectionResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_WhenOnlyImplementationIsExcluded()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public interface IPlugin { }

[DoNotInject]
public class ExcludedPlugin : IPlugin { }

public class Consumer
{
    public Consumer({|#0:IEnumerable<IPlugin>|} plugins) { }
}
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<CollectionResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.CollectionResolutionEmpty, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("IPlugin")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenOneImplementationExcludedButOthersExist()
    {
        var code = @"
using System.Collections.Generic;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public interface IPlugin { }

[DoNotInject]
public class ExcludedPlugin : IPlugin { }

public class IncludedPlugin : IPlugin { }

public class Consumer
{
    public Consumer(IEnumerable<IPlugin> plugins) { }
}
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<CollectionResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
