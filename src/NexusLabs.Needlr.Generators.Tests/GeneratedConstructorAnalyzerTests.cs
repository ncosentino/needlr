using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for <see cref="GeneratedConstructorAnalyzer"/> diagnostics NDLRGEN039-054:
/// generated-constructor type-shape validation, field-level guard-attribute
/// eligibility, built-in guard compatibility, custom guard type/method resolution, and
/// <c>[ConstructorGuardDefinition]</c> alias validation.
/// </summary>
public sealed class GeneratedConstructorAnalyzerTests
{
    private static string Attributes => NeedlrTestAttributes.AllWithGeneratedConstructor;

    private static CSharpAnalyzerTest<GeneratedConstructorAnalyzer, DefaultVerifier> CreateTest(string code) => new()
    {
        TestCode = code + Attributes,
    };


    [Fact]
    public async Task NoDiagnostic_ForBareGenerateConstructorWithEligibleField()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForFieldTriggeredGenerationWithBuiltInGuard()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class TenantService
{
    private readonly string _tenantName;

    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _guardedTenantName;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenUnrelatedSameNamedAttributeInDifferentNamespace()
    {
        // A same-named GenerateConstructorAttribute in a different namespace is not the
        // Needlr attribute and must never trigger any generated-constructor diagnostic.
        var test = CreateTest(@"
namespace OtherVendor
{
    public sealed class GenerateConstructorAttribute : System.Attribute { }
}

[OtherVendor.GenerateConstructor]
public class NotAGeneratedConstructorType
{
    public int Value;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN039_WhenClassIsNotPartial()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public class {|#0:UserService|}
{
    private readonly IRepository _repository;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN039", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("UserService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN040_WhenTypeIsARecord()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial record {|#0:UserServiceRecord|}
{
    private readonly IRepository _repository;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN040", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("UserServiceRecord", "a record type"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN040_WhenTypeIsNested()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

public partial class Outer
{
    [GenerateConstructor]
    public partial class {|#0:Inner|}
    {
        private readonly IRepository _repository;
    }
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN040", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Inner", "a nested type"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN041_WhenExplicitConstructorExists()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class {|#0:UserService|}
{
    private readonly IRepository _repository;

    public UserService(IRepository repository)
    {
        _repository = repository;
    }
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN041", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("UserService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN041_HandAuthoredConstructorInUnrelatedGDotCsSuffixedFileStillConflicts()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class {|#0:UserService|}
{
    private readonly IRepository _repository;
}
");
        test.TestState.Sources.Add(("Service.g.cs", @"
public partial class UserService
{
    public UserService(IRepository repository)
    {
        _repository = repository;
    }
}
"));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN041", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("UserService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenConstructorIsDeclaredInTheGeneratedConstructorGeneratorsOwnOutputFile()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;
}
");
        test.TestState.Sources.Add(("UserService.GeneratedConstructor.g.cs", @"
public partial class UserService
{
    public UserService(IRepository repository)
    {
        _repository = repository;
    }
}
"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task MultiPartialClass_ReportsExactlyOneClassShapeDiagnostic()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class {|#0:UserService|}
{
    private readonly IRepository _repository;
}

public partial class UserService
{
    public UserService(IRepository repository)
    {
        _repository = repository;
    }
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN041", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("UserService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MultiPartialClass_FieldGuardDiagnosticsStillReportedInEachPart()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class TenantService
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)|}]
    public readonly string TenantNameA;
}

public partial class TenantService
{
    [{|#1:ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)|}]
    public readonly string TenantNameB;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN046", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("TenantNameA", "not private"));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN046", DiagnosticSeverity.Error)
                .WithLocation(1)
                .WithArguments("TenantNameB", "not private"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN042_WhenBaseTypeHasNoParameterlessConstructor()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

public class BaseWithRequiredArgs
{
    public BaseWithRequiredArgs(int required) { }
}

[GenerateConstructor]
public partial class {|#0:UserService|} : BaseWithRequiredArgs
{
    private readonly IRepository _repository;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN042", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("UserService", "BaseWithRequiredArgs"));
        test.ExpectedDiagnostics.Add(
            DiagnosticResult.CompilerError("CS7036").WithLocation(0)
                .WithArguments("required", "BaseWithRequiredArgs.BaseWithRequiredArgs(int)"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN043_WhenNoEligibleFieldExists()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

[GenerateConstructor]
public partial class {|#0:EmptyService|}
{
    public int PublicField;
    private static int _staticField;
    private readonly int _initialized = 1;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN043", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("EmptyService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN044_WhenNormalizedParameterNamesCollide()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

[GenerateConstructor]
public partial class {|#0:OrderService|}
{
    private readonly string _value;
    private readonly string value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN044", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("OrderService", "value"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN045_WhenConstructorIgnoreHasNoGenerationTrigger()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class CacheEntry
{
    [{|#0:ConstructorIgnore|}]
    private readonly string _serializedPayload;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN045", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("_serializedPayload", "[ConstructorIgnore]"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN045_WhenConstructorGuardNoneHasNoGenerationTrigger()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class CacheEntry
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.None)|}]
    private readonly string _payload;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN045", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("_payload", "[ConstructorGuard(ConstructorGuardKind.None)]"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenConstructorIgnoreCoexistsWithGenerateConstructor()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[GenerateConstructor]
public partial class CacheEntry
{
    private readonly IRepository _repository;

    [ConstructorIgnore]
    private readonly string? _serializedPayload;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN046_WhenFieldIsNotPrivate()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class TenantService
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)|}]
    public readonly string TenantName;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN046", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("TenantName", "not private"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN046_WhenFieldIsNotReadonly()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class TenantService
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)|}]
    private string _tenantName;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN046", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_tenantName", "not readonly"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN046_WhenFieldIsStatic()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class TenantService
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)|}]
    private static readonly string _tenantName = ""x"";
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN046", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_tenantName", "static"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN046_WhenFieldIsInitialized()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class TenantService
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)|}]
    private readonly string _tenantName = ""default"";
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN046", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_tenantName", "initialized with a field initializer"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN047_WhenConstructorGuardKindIsUndefined()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class TenantService
{
    [{|#0:ConstructorGuard((ConstructorGuardKind)99)|}]
    private readonly string _tenantName;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN047", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(99, "ConstructorGuardKind"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN047_WhenConstructorNullGuardModeIsUndefined()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public interface IRepository { }

[{|#0:GenerateConstructor((ConstructorNullGuardMode)99)|}]
public partial class UserService
{
    private readonly IRepository _repository;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN047", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(99, "ConstructorNullGuardMode"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN048_WhenNotNullAppliedToNonNullableValueType()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class RetryPolicy
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.NotNull)|}]
    private readonly int _retryCount;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN048", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("NotNull", "_retryCount", "int", "the field's type is a non-nullable value type, so a runtime null value is never possible"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNotNullAppliedToUnconstrainedTypeParameter()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class ValueHolder<T>
{
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly T _value;
}
");

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN048_WhenNotNullAppliedToStructConstrainedTypeParameter()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class ValueHolder<T>
    where T : struct
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.NotNull)|}]
    private readonly T _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN048", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("NotNull", "_value", "T", "the field's type is a non-nullable value type, so a runtime null value is never possible"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN048_WhenNotNullOrWhiteSpaceAppliedToNonStringField()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class RetryPolicy
{
    [{|#0:ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)|}]
    private readonly int _retryCount;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN048", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("NotNullOrWhiteSpace", "_retryCount", "int", "this guard only applies to string-compatible fields"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNotNullAppliedToNullableValueType()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class RetryPolicy
{
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly int? _retryCount;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN049_WhenCustomGuardTypeCannotBeResolved()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(UndefinedGuardType))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN049", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_orderId", "the guard type could not be resolved"));
        test.ExpectedDiagnostics.Add(DiagnosticResult.CompilerError("CS0246").WithSpan(6, 30, 6, 48).WithArguments("UndefinedGuardType"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN050_WhenExplicitMethodNameIsEmpty()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class OrderIdGuard
{
    public static void Validate(string value, string parameterName) { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(OrderIdGuard), """")|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN050", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_orderId"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN051_WhenMethodNameDoesNotExist()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class OrderIdGuard
{
    public static void SomeOtherMethod(string value, string parameterName) { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(OrderIdGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "OrderIdGuard", "_orderId", "string", "no method named 'Validate' was found on 'OrderIdGuard'"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenMethodIsNotStatic()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public class OrderIdGuard
{
    public void Validate(string value, string parameterName) { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(OrderIdGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "OrderIdGuard", "_orderId", "string", "it is not static"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenMethodReturnsNonVoid()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class OrderIdGuard
{
    public static string Validate(string value, string parameterName) => value;
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(OrderIdGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "OrderIdGuard", "_orderId", "string", "it does not return void"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenValueParameterTypeIsIncompatible()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class NumberGuards
{
    public static void Validate(int value, string parameterName) { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(NumberGuards))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "NumberGuards", "_orderId", "string", "its value parameter type is not compatible with the field's type"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenValueParameterIsPassedByRef()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class RefGuard
{
    public static void Validate(ref string value, string parameterName) { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(RefGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "RefGuard", "_orderId", "string", "its 'value' parameter is passed by 'ref', which a direct generated call cannot supply"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenValueParameterIsPassedByOut()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class OutGuard
{
    public static void Validate(out string value, string parameterName) { value = string.Empty; }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(OutGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "OutGuard", "_orderId", "string", "its 'value' parameter is passed by 'out', which a direct generated call cannot supply"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenParameterNameParameterIsPassedByIn()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class InGuard
{
    public static void Validate(string value, in string parameterName) { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(InGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "InGuard", "_orderId", "string", "its 'parameterName' parameter is passed by 'in', which a direct generated call cannot supply"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenGenericMethodHasUninferredExtraTypeParameter()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class ExtraTypeParamGuard
{
    public static void Validate<T, TExtra>(T value, string parameterName) { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(ExtraTypeParamGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "ExtraTypeParamGuard", "_orderId", "string", "its type parameter 'TExtra' cannot be inferred from the field's type"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenGenericConstraintIsIncompatibleWithFieldType()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class DisposableGuard
{
    public static void Validate<T>(T value, string parameterName) where T : System.IDisposable { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(DisposableGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "DisposableGuard", "_orderId", "string", "its type parameter 'T' requires 'System.IDisposable', which 'string' does not satisfy"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN052_WhenMultipleOverloadsMatch()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class AmbiguousGuard
{
    public static void Validate(string value, string parameterName) { }
    public static void Validate<T>(T value, string parameterName) { }
}

public partial class OrderService
{
    [{|#0:ConstructorGuard(typeof(AmbiguousGuard))|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN052", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "AmbiguousGuard", "_orderId", "string"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NoDiagnostic_ForValidDirectExactCustomGuard()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class OrderIdGuard
{
    public static void Validate(string value, string parameterName) { }
}

public partial class OrderService
{
    [ConstructorGuard(typeof(OrderIdGuard))]
    private readonly string _orderId;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForValidGenericCustomGuard()
    {
        var test = CreateTest(@"
using System.Collections.Generic;
using NexusLabs.Needlr.Generators;

public static class CollectionNotEmptyGuard
{
    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName) { }
}

public partial class OrderService
{
    [ConstructorGuard(typeof(CollectionNotEmptyGuard))]
    private readonly IReadOnlyCollection<string> _orders;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForValidExplicitMethodSelector()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class NumberGuards
{
    public static void ValidatePositive(int value, string parameterName) { }
}

public partial class RetryPolicy
{
    [ConstructorGuard(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))]
    private readonly int _retryCount;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN053_WhenTargetDoesNotDeriveFromAttribute()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class SomeGuard
{
    public static void Validate(string value, string parameterName) { }
}

[{|#0:ConstructorGuardDefinition(typeof(SomeGuard))|}]
public sealed class NotAnAttributeAlias
{
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN053", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("NotAnAttributeAlias", "not derived from System.Attribute"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN053_WhenAttributeUsageExcludesField()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class SomeGuard
{
    public static void Validate(string value, string parameterName) { }
}

[{|#0:ConstructorGuardDefinition(typeof(SomeGuard))|}]
[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class MethodOnlyAliasAttribute : System.Attribute
{
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN053", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MethodOnlyAliasAttribute", "not usable on fields ([AttributeUsage] does not include AttributeTargets.Field)"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NDLRGEN054_WhenGuardTypeCannotBeResolved()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

[{|#0:ConstructorGuardDefinition(typeof(UndefinedGuardType))|}]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class BrokenAliasAttribute : System.Attribute
{
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN054", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("BrokenAliasAttribute", "the guard type could not be resolved"));
        test.ExpectedDiagnostics.Add(DiagnosticResult.CompilerError("CS0246").WithSpan(4, 36, 4, 54).WithArguments("UndefinedGuardType"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN054_WhenNoCompatibleMethodExists()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class SomeGuard
{
    public static void SomeOtherMethod(string value, string parameterName) { }
}

[{|#0:ConstructorGuardDefinition(typeof(SomeGuard))|}]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class BrokenAliasAttribute : System.Attribute
{
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN054", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("BrokenAliasAttribute", "no method named 'Validate' was found on 'SomeGuard'"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForValidGuardDefinitionAndAliasUsage()
    {
        var test = CreateTest(@"
using System.Collections.Generic;
using NexusLabs.Needlr.Generators;

public static class CollectionNotEmptyGuard
{
    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class CollectionNotEmptyAttribute : System.Attribute
{
}

public partial class OrderService
{
    [CollectionNotEmpty]
    private readonly IReadOnlyCollection<string> _orders;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN051_WhenInSourceAliasIsIncompatibleWithFieldTypeAtUsage()
    {
        // The alias itself is valid (NDLRGEN053/054 do not fire), but this particular
        // field's type is incompatible with the alias's guard method, which can only be
        // known at the usage site.
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class NumberGuards
{
    public static void ValidatePositive(int value, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class PositiveAttribute : System.Attribute
{
}

public partial class OrderService
{
    [{|#0:Positive|}]
    private readonly string _orderId;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN051", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("ValidatePositive", "NumberGuards", "_orderId", "string", "its value parameter type is not compatible with the field's type"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }


    [Fact]
    public async Task NoDiagnostic_ForParameterizedAliasWithSingleForwardedArgument()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class MinCountGuard
{
    public static void Validate(int value, int min, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(MinCountGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class MinCountAttribute : System.Attribute
{
    public MinCountAttribute(int min) { }
}

public partial class Basket
{
    [MinCount(3)]
    private readonly int _value;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForParameterizedAliasWithOmittedOptionalArgument()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class RetryGuard
{
    public static void Validate(int value, int maxAttempts, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(RetryGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class RetryAttribute : System.Attribute
{
    public RetryAttribute(int maxAttempts = 5) { }
}

public partial class Container
{
    [Retry]
    private readonly int _value;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForParameterizedAliasWithMultipleArgumentsInOrder()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class RangeGuard
{
    public static void Validate(int value, int min, int max, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(RangeGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class WithinRangeAttribute : System.Attribute
{
    public WithinRangeAttribute(int min, int max) { }
}

public partial class RetryPolicy
{
    [WithinRange(3, 10)]
    private readonly int _value;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForParameterizedAliasForwardingNullArgument()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class DefaultableGuard
{
    public static void Validate(string value, string fallback, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(DefaultableGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class DefaultableAttribute : System.Attribute
{
    public DefaultableAttribute(string fallback) { }
}

public partial class OrderService
{
    [Defaultable(null)]
    private readonly string _value;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForParameterizedAliasForwardingEnumArgument()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public enum RiskLevel { Low, Medium, High }

public static class RiskGuard
{
    public static void Validate(string value, RiskLevel level, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(RiskGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class RiskAttribute : System.Attribute
{
    public RiskAttribute(RiskLevel level) { }
}

public partial class OrderService
{
    [Risk(RiskLevel.High)]
    private readonly string _value;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForParameterizedAliasForwardingOpenGenericTypeofArgument()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class TypeGuard
{
    public static void Validate(object value, System.Type expected, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(TypeGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class OfTypeAttribute : System.Attribute
{
    public OfTypeAttribute(System.Type expected) { }
}

public partial class Container
{
    [OfType(typeof(System.Collections.Generic.List<>))]
    private readonly object _value;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForParameterizedAliasSharedGenericTypeParameter()
    {
        // T is bound both by the guarded value and by the forwarded threshold
        // argument; both the field's type and the alias's own argument type must agree
        // on the same T for this to resolve.
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class AtLeastGuard
{
    public static void Validate<T>(T value, T threshold, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(AtLeastGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class AtLeastAttribute : System.Attribute
{
    public AtLeastAttribute(int threshold) { }
}

public partial class Container
{
    [AtLeast(3)]
    private readonly int _value;
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForParameterizedGuardDefinitionDeclaration()
    {
        // The alias's own field-usage arity (one forwarded int argument) is only known
        // at a usage site; declaring [ConstructorGuardDefinition] must not require that
        // arity to be known up front, so no NDLRGEN054 is expected here.
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class MinCountGuard
{
    public static void Validate(int value, int min, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(MinCountGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class MinCountAttribute : System.Attribute
{
    public MinCountAttribute(int min = 1) { }
}
");
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN056_WhenForwardedArgumentCountIsWrong()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class RangeGuard
{
    public static void Validate(int value, int min, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(RangeGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class WithinRangeAttribute : System.Attribute
{
    public WithinRangeAttribute(int min, int max) { }
}

public partial class RetryPolicy
{
    [{|#0:WithinRange(3, 10)|}]
    private readonly int _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN056", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "RangeGuard", "_value", "int", "it has 1 parameter(s) between the value and the parameter name, but this alias usage forwards 2 argument(s)"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN056_WhenForwardedArgumentTypeIsIncompatible()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class MinCountGuard
{
    public static void Validate(int value, int min, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(MinCountGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class MinCountAttribute : System.Attribute
{
    public MinCountAttribute(string min) { }
}

public partial class Basket
{
    [{|#0:MinCount(""three"")|}]
    private readonly int _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN056", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "MinCountGuard", "_value", "int", "its parameter 'min' of type 'int' is not compatible with the forwarded argument of type 'string'"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN056_WhenGenericSharedTypeParameterConflictsWithForwardedArgument()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class AtLeastGuard
{
    public static void Validate<T>(T value, T threshold, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(AtLeastGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class AtLeastAttribute : System.Attribute
{
    public AtLeastAttribute(string threshold) { }
}

public partial class Container
{
    [{|#0:AtLeast(""3"")|}]
    private readonly int _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN056", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "AtLeastGuard", "_value", "int", "its parameter 'threshold' cannot accept the forwarded argument of type 'string'"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN056_WhenGenericConstraintFailsOnlyThroughForwardedArgument()
    {
        // T is inferable from the value parameter alone (int), but the constraint
        // requires System.IDisposable, which int does not satisfy. This constraint
        // violation is reachable only through the forwarded "extra" parameter's own
        // constrained type parameter, not through the value parameter's type.
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class TaggedGuard
{
    public static void Validate<T, TTag>(T value, TTag tag, string parameterName) where TTag : System.IDisposable { }
}

[ConstructorGuardDefinition(typeof(TaggedGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class TaggedAttribute : System.Attribute
{
    public TaggedAttribute(int tag) { }
}

public partial class Container
{
    [{|#0:Tagged(1)|}]
    private readonly int _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN056", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "TaggedGuard", "_value", "int", "its type parameter 'TTag' requires 'System.IDisposable', which 'int' does not satisfy"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN055_WhenAliasForwardsArrayArgument()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class TagsGuard
{
    public static void Validate(string value, string[] allowed, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(TagsGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class TagsAttribute : System.Attribute
{
    public TagsAttribute(string[] allowed) { }
}

public partial class Container
{
    [{|#0:Tags(new[] { ""a"", ""b"" })|}]
    private readonly string _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN055", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_value", "positional argument 1 is an array, which is not forwarded to the guard method in this version"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN055_WhenAliasForwardsFloatingPointArgument()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class ThresholdGuard
{
    public static void Validate(double value, double threshold, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(ThresholdGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class ThresholdAttribute : System.Attribute
{
    public ThresholdAttribute(double threshold) { }
}

public partial class Container
{
    [{|#0:Threshold(1.5)|}]
    private readonly double _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN055", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_value", "positional argument 1 is a floating-point value, which is not forwarded to the guard method in this version"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN055_WhenAliasUsesNamedProperty()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class LabelGuard
{
    public static void Validate(string value, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(LabelGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class LabeledAttribute : System.Attribute
{
    public string? Prefix { get; set; }
}

public partial class OrderService
{
    [{|#0:Labeled(Prefix = ""ORD"")|}]
    private readonly string _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN055", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("_value", "named argument 'Prefix' is not forwarded to the guard method in this version"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForReferencedAssemblyAliasWithForwardedArgument()
    {
        var test = CreateTest(@"
using FrameworkLib;

public partial class Basket
{
    [MinCount(3)]
    private readonly int _value;
}
");
        test.TestState.AdditionalProjects["FrameworkLib"].Sources.Add(Attributes + @"
namespace FrameworkLib
{
    public static class MinCountGuard
    {
        public static void Validate(int value, int min, string parameterName) { }
    }

    [NexusLabs.Needlr.Generators.ConstructorGuardDefinition(typeof(MinCountGuard))]
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class MinCountAttribute : System.Attribute
    {
        public MinCountAttribute(int min) { }
    }
}
");
        test.TestState.AdditionalProjectReferences.Add("FrameworkLib");

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN056_WhenReferencedAssemblyAliasForwardsIncompatibleArgumentType()
    {
        var test = CreateTest(@"
using FrameworkLib;

public partial class Basket
{
    [{|#0:MinCount(""three"")|}]
    private readonly int _value;
}
");
        test.TestState.AdditionalProjects["FrameworkLib"].Sources.Add(Attributes + @"
namespace FrameworkLib
{
    public static class MinCountGuard
    {
        public static void Validate(int value, int min, string parameterName) { }
    }

    [NexusLabs.Needlr.Generators.ConstructorGuardDefinition(typeof(MinCountGuard))]
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class MinCountAttribute : System.Attribute
    {
        public MinCountAttribute(string min) { }
    }
}
");
        test.TestState.AdditionalProjectReferences.Add("FrameworkLib");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN056", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "FrameworkLib.MinCountGuard", "_value", "int", "its parameter 'min' of type 'int' is not compatible with the forwarded argument of type 'string'"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN052_WhenMultipleOverloadsMatchForwardedArguments()
    {
        var test = CreateTest(@"
using NexusLabs.Needlr.Generators;

public static class AmbiguousRangeGuard
{
    public static void Validate(int value, int min, string parameterName) { }
    public static void Validate<T>(T value, int min, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(AmbiguousRangeGuard))]
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class MinCountAttribute : System.Attribute
{
    public MinCountAttribute(int min) { }
}

public partial class Basket
{
    [{|#0:MinCount(3)|}]
    private readonly int _value;
}
");
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN052", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Validate", "AmbiguousRangeGuard", "_value", "int"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
