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
        public ConstructorGuardAttribute(System.Type guardType) => GuardType = guardType;
        public ConstructorGuardKind Kind { get; }
        public System.Type GuardType { get; }
    }

    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class ConstructorIgnoreAttribute : System.Attribute
    {
    }

    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ConstructorGuardDefinitionAttribute : System.Attribute
    {
        public ConstructorGuardDefinitionAttribute(System.Type guardType) => GuardType = guardType;
        public System.Type GuardType { get; }
    }
}";

    private const string GuardDefinitions = @"
public static class HubNameGuards
{
    public static void Validate(string value, string parameterName) { }
}

[NexusLabs.Needlr.Generators.ConstructorGuardDefinition(typeof(HubNameGuards))]
[System.AttributeUsage(System.AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ValidHubNameAttribute : System.Attribute
{
}";

    private static string Attributes => HubRegistrationPluginDefinition + GeneratedConstructorAttributes;

    private static CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier> CreateTest(string code)
    {
        return new CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier>
        {
            TestCode = code + Attributes
        };
    }

    private static async Task VerifySingleDiagnosticAsync(string code, string typeName)
    {
        var test = CreateTest(code);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRSIG003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(typeName));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static async Task VerifyNoDiagnosticAsync(string code)
    {
        await CreateTest(code).RunAsync(TestContext.Current.CancellationToken);
    }

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

    [Fact]
    public async Task Error_WhenPluginIsEligibleViaCustomGuardTypeTrigger()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public partial class {|#0:ChatHubRegistration|} : IHubRegistrationPlugin
{
    [ConstructorGuard(typeof(HubNameGuards))]
    private readonly string _hubName;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
" + GuardDefinitions;

        await VerifySingleDiagnosticAsync(code, "ChatHubRegistration");
    }

    [Fact]
    public async Task Error_WhenPluginIsEligibleViaAliasGuardTrigger()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

public partial class {|#0:ChatHubRegistration|} : IHubRegistrationPlugin
{
    [ValidHubName]
    private readonly string _hubName;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
" + GuardDefinitions;

        await VerifySingleDiagnosticAsync(code, "ChatHubRegistration");
    }

    [Theory]
    [InlineData("public")]
    [InlineData("protected")]
    [InlineData("protected internal")]
    public async Task Error_WhenBaseTypeHasAccessibleParameterlessConstructor(string accessibility)
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

public class RegistrationBase
{
    " + accessibility + @" RegistrationBase() { }
}

[GenerateConstructor]
public partial class {|#0:ChatHubRegistration|} : RegistrationBase, IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifySingleDiagnosticAsync(code, "ChatHubRegistration");
    }

    [Fact]
    public async Task NoError_WhenBaseTypeOnlyHasInternalParameterlessConstructor()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

public class RegistrationBase
{
    internal RegistrationBase() { }
}

[GenerateConstructor]
public partial class ChatHubRegistration : RegistrationBase, IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenBaseTypeRequiresConstructorArguments()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

public class RegistrationBase
{
    public RegistrationBase(int id) { }
}

[GenerateConstructor]
public partial class ChatHubRegistration : RegistrationBase, IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public ChatHubRegistration(IRepository repository) : base(0) { _repository = repository; }

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenOnlyGuardIsBuiltInNoneKind()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public partial class ChatHubRegistration : IHubRegistrationPlugin
{
    [ConstructorGuard(ConstructorGuardKind.None)]
    private readonly string _hubName;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenOnlyEligibleFieldIsConstructorIgnored()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class ChatHubRegistration : IHubRegistrationPlugin
{
    [ConstructorIgnore]
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Theory]
    [InlineData("private readonly IRepository _repository = null;")]
    [InlineData("private static readonly IRepository _repository;")]
    [InlineData("private const string _repository = \"\";")]
    [InlineData("private IRepository _repository;")]
    [InlineData("internal readonly IRepository _repository;")]
    [InlineData("public readonly IRepository _repository;")]
    public async Task NoError_WhenNoFieldIsEligible(string fieldDeclaration)
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class ChatHubRegistration : IHubRegistrationPlugin
{
    " + fieldDeclaration + @"

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenPluginHasNoFieldsAtAll()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

[GenerateConstructor]
public partial class ChatHubRegistration : IHubRegistrationPlugin
{
    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenPluginIsARecord()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial record ChatHubRegistration : IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenPluginIsAStruct()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public partial struct ChatHubRegistration : IHubRegistrationPlugin
{
    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _hubName;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenPluginIsANestedType()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

public static class Registrations
{
    [GenerateConstructor]
    public partial class ChatHubRegistration : IHubRegistrationPlugin
    {
        private readonly IRepository _repository;

        public string HubPath => ""/chat"";
        public System.Type HubType => typeof(object);
    }
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenPluginIsNotPartial()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public class ChatHubRegistration : IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task NoError_WhenPluginDeclaresExplicitInstanceConstructor()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class ChatHubRegistration : IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public ChatHubRegistration() { }

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifyNoDiagnosticAsync(code);
    }

    [Fact]
    public async Task Error_WhenOnlyConstructorIsDeclaredInGeneratedConstructorFile()
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

        // Mirrors the constructor GeneratedConstructorGenerator itself emits: it must not
        // be mistaken for a hand-written constructor that would suppress generation.
        test.TestState.Sources.Add(("Z.ChatHubRegistration.GeneratedConstructor.g.cs", @"
public partial class ChatHubRegistration
{
    public ChatHubRegistration(IRepository repository)
    {
        _repository = repository;
    }
}
"));
        test.TestState.Sources.Add(("Attributes.cs", Attributes));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRSIG003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("ChatHubRegistration"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_IsReportedOnCanonicalDeclarationWhenBothPartialsDeclareFields()
    {
        var test = new CSharpAnalyzerTest<HubRegistrationPluginConstructorAnalyzer, DefaultVerifier>();
        test.TestState.Sources.Add(("B.Plugin.cs", @"
public partial class ChatHubRegistration
{
    private readonly IRepository _second;
}
"));
        test.TestState.Sources.Add(("A.Plugin.cs", @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class {|#0:ChatHubRegistration|} : IHubRegistrationPlugin
{
    private readonly IRepository _first;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
"));
        test.TestState.Sources.Add(("Attributes.cs", Attributes));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRSIG003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("ChatHubRegistration"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenPluginIsFileLocal()
    {
        // A file-local plugin is still excluded from hub registration by the generator
        // (it applies the exact same eligibility rule), so the diagnostic is reported
        // rather than the plugin being silently dropped.
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
file partial class {|#0:ChatHubRegistration|} : IHubRegistrationPlugin
{
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifySingleDiagnosticAsync(code, "ChatHubRegistration");
    }

    [Fact]
    public async Task Error_WhenPluginImplementsInterfaceInheritingHubRegistrationPlugin()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;
using NexusLabs.Needlr.Generators;

public interface IRepository { }

public interface IChatRegistration : IHubRegistrationPlugin { }

[GenerateConstructor]
public partial class {|#0:ChatHubRegistration|} : IChatRegistration
{
    private readonly IRepository _repository;

    public string HubPath => ""/chat"";
    public System.Type HubType => typeof(object);
}
";

        await VerifySingleDiagnosticAsync(code, "ChatHubRegistration");
    }
}
