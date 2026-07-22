using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="HubRegistrationPluginConstructorAnalyzer"/> (NDLRSIG003): an
/// <c>IHubRegistrationPlugin</c> implementation eligible for generated-constructor
/// generation cannot be activated by the SignalR hub-registration generator, which
/// requires parameterless activation and deliberately excludes such a type.
/// </summary>
public sealed class HubRegistrationPluginConstructorAnalyzerTests
{
    private const string HubRegistrationPluginDefinition = @"
namespace NexusLabs.Needlr.SignalR
{
    public interface IHubRegistrationPlugin
    {
        string HubPath { get; }
        System.Type HubType { get; }
    }
}";

    private const string GeneratedConstructorAttributes = @"
namespace NexusLabs.Needlr.Generators
{
    public enum ConstructorNullGuardMode
    {
        None = 0,
        NonNullableReferences = 1,
    }

    public enum ConstructorGuardKind
    {
        None = 0,
        NotNull = 1,
        NotNullOrEmpty = 2,
        NotNullOrWhiteSpace = 3,
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateConstructorAttribute : System.Attribute
    {
        public GenerateConstructorAttribute() : this(ConstructorNullGuardMode.None) { }
        public GenerateConstructorAttribute(ConstructorNullGuardMode mode) => Mode = mode;
        public ConstructorNullGuardMode Mode { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class ConstructorGuardAttribute : System.Attribute
    {
        public ConstructorGuardAttribute(ConstructorGuardKind kind) => Kind = kind;
        public ConstructorGuardKind Kind { get; }
    }
}";

    private static string Attributes => HubRegistrationPluginDefinition + GeneratedConstructorAttributes;

    [Fact]
    public async Task Error_WhenPluginIsEligibleForGeneratedConstructorViaClassAttribute()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class {|#0:ChatHubRegistration|} : IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
" + Attributes;

        var test = new CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRSIG003", DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("ChatHubRegistration")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenPluginIsEligibleForGeneratedConstructorViaFieldGuardTrigger()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public partial class {|#0:ChatHubRegistration|} : IHubRegistrationPlugin
{
    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _hubName;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
" + Attributes;

        var test = new CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRSIG003", DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("ChatHubRegistration")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenPluginHasNoGenerationTrigger()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

public class ChatHubRegistration : IHubRegistrationPlugin
{
    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
" + Attributes;

        var test = new CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenGeneratedConstructorTypeDoesNotImplementHubRegistrationPlugin()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class UnrelatedService
{
    private readonly IRepository _repository;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenUnrelatedSameNamedInterfaceInDifferentNamespace()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

namespace OtherVendor
{
    public interface IHubRegistrationPlugin { }
}

public interface IRepository { }

[GenerateConstructor]
public partial class NotAHubRegistration : OtherVendor.IHubRegistrationPlugin
{
    private readonly IRepository _repository;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_IsReportedOnceForMultiPartialPlugin()
    {
        var test = new CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier>();
        test.TestState.Sources.Add(("A.Plugin.cs", @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class {|#0:ChatHubRegistration|} : IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
"));
        test.TestState.Sources.Add(("B.Plugin.cs", @"
public partial class ChatHubRegistration
{
}
"));
        test.TestState.Sources.Add(("Attributes.cs", Attributes));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRSIG003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("ChatHubRegistration"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
