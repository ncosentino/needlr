using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class CollectionResolutionAnalyzerTests
{
    // Use shared test attributes that match the real package
    private static string Attributes => NeedlrTestAttributes.All;

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
" + Attributes;

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
" + Attributes;

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
" + Attributes;

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
" + Attributes;

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
" + Attributes;

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
" + Attributes;

        var test = new CSharpAnalyzerTest<CollectionResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
