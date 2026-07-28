using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for the NDLRGEN063 generated-constructor suggestion.
/// </summary>
public sealed class GenerateConstructorSuggestionAnalyzerTests
{
    private static string Attributes =>
        NeedlrTestAttributes.AllWithGeneratedConstructor;

    private static CSharpAnalyzerTest<
        GenerateConstructorSuggestionAnalyzer,
        DefaultVerifier> CreateTest(string code) => new()
        {
            TestCode = code + Attributes,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };

    private static CSharpAnalyzerTest<
        GenerateConstructorSuggestionAnalyzer,
        DefaultVerifier> CreateUnsafeTest(string code)
    {
        var test = CreateTest(code);
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var options = (CSharpCompilationOptions)solution
                .GetProject(projectId)!
                .CompilationOptions!;
            return solution.WithProjectCompilationOptions(
                projectId,
                options.WithAllowUnsafe(true));
        });

        return test;
    }

    /// <summary>
    /// Reports an assignment-only constructor that exactly matches generated output.
    /// </summary>
    [Fact]
    public async Task NDLRGEN063_ForAssignmentOnlyConstructor()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class UserService
            {
                private readonly IRepository _repository;

                public {|#0:UserService|}(IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN063", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("UserService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reports supported guard calls when they use the generator's emitted order.
    /// </summary>
    [Fact]
    public async Task NDLRGEN063_ForSupportedGuardsBeforeAssignments()
    {
        var test = CreateTest("""
            #nullable enable

            public interface IRepository { }

            public sealed class TenantService
            {
                private readonly IRepository _repository;
                private readonly string _tenantName;

                public {|#0:TenantService|}(
                    IRepository repository,
                    string tenantName)
                {
                    System.ArgumentNullException.ThrowIfNull(repository);
                    System.ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);

                    _repository = repository;
                    _tenantName = tenantName;
                }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN063", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("TenantService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reports the supported null-or-empty guard as an explicit field guard candidate.
    /// </summary>
    [Fact]
    public async Task NDLRGEN063_ForThrowIfNullOrEmptyGuard()
    {
        var test = CreateTest("""
            #nullable enable

            public sealed class TenantService
            {
                private readonly string _tenantName;

                public {|#0:TenantService|}(string tenantName)
                {
                    System.ArgumentException.ThrowIfNullOrEmpty(tenantName);
                    _tenantName = tenantName;
                }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN063", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("TenantService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Allows a consumer to promote the advisory diagnostic to a warning.
    /// </summary>
    [Fact]
    public async Task NDLRGEN063_CanBePromotedToWarning()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class UserService
            {
                private readonly IRepository _repository;

                public {|#0:UserService|}(IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);
        test.TestState.AnalyzerConfigFiles.Add((
            "/.globalconfig",
            """
            is_global = true
            dotnet_diagnostic.NDLRGEN063.severity = warning
            """));
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN063", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("UserService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not report when guard and assignment ordering differs from generated output.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenGuardsAndAssignmentsAreInterleaved()
    {
        var test = CreateTest("""
            #nullable enable

            public interface IRepository { }
            public interface ILogger { }

            public sealed class UserService
            {
                private readonly IRepository _repository;
                private readonly ILogger _logger;

                public UserService(IRepository repository, ILogger logger)
                {
                    System.ArgumentNullException.ThrowIfNull(repository);
                    _repository = repository;
                    System.ArgumentNullException.ThrowIfNull(logger);
                    _logger = logger;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not infer an architectural refactor for normalized constructor values.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenConstructorTransformsValue()
    {
        var test = CreateTest("""
            public sealed class Cache
            {
                private readonly string _name;

                public Cache(string name)
                {
                    _name = name.Trim();
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not report when generation would reorder the public constructor parameters.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenParameterOrderDiffersFromFieldOrder()
    {
        var test = CreateTest("""
            public interface IRepository { }
            public interface ILogger { }

            public sealed class UserService
            {
                private readonly IRepository _repository;
                private readonly ILogger _logger;

                public UserService(ILogger logger, IRepository repository)
                {
                    _logger = logger;
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not choose among multiple authored constructors.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenMultipleConstructorsExist()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class UserService
            {
                private readonly IRepository? _repository;

                public UserService()
                {
                }

                public UserService(IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not suggest generation when it would change constructor accessibility.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenConstructorIsNotPublic()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class UserService
            {
                private readonly IRepository _repository;

                internal UserService(IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not remove a default value that changes valid constructor call sites.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenParameterHasDefaultValue()
    {
        var test = CreateTest("""
            #nullable enable

            public interface IRepository { }

            public sealed class UserService
            {
                private readonly IRepository? _repository;

                public UserService(IRepository? repository = null)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not remove the params calling convention from an authored constructor.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenParameterUsesParams()
    {
        var test = CreateTest("""
            public sealed class Batch
            {
                private readonly int[] _values;

                public Batch(params int[] values)
                {
                    _values = values;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not remove a parameter passing modifier from the public signature.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenParameterUsesInModifier()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class UserService
            {
                private readonly IRepository _repository;

                public UserService(in IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not discard parameter metadata that the generator cannot reproduce.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenParameterHasAttribute()
    {
        var test = CreateTest("""
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class RuntimeValueAttribute : System.Attribute
            {
            }

            public interface IRepository { }

            public sealed class UserService
            {
                private readonly IRepository _repository;

                public UserService(
                    [RuntimeValue] IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not discard constructor metadata that the generator cannot reproduce.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenConstructorHasAttribute()
    {
        var test = CreateTest("""
            [System.AttributeUsage(System.AttributeTargets.Constructor)]
            public sealed class ActivationAttribute : System.Attribute
            {
            }

            public interface IRepository { }

            public sealed class UserService
            {
                private readonly IRepository _repository;

                [Activation]
                public UserService(IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not suggest source that would require an unsafe generated declaration.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenParameterContainsPointerType()
    {
        var test = CreateUnsafeTest("""
            public unsafe sealed class PointerHolder
            {
                private readonly int* _pointer;

                public PointerHolder(int* pointer)
                {
                    _pointer = pointer;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not report a constructor that passes arguments to its base type.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenBaseArgumentsAreRequired()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public class ServiceBase
            {
                public ServiceBase(int capacity)
                {
                }
            }

            public sealed class UserService : ServiceBase
            {
                private readonly IRepository _repository;

                public UserService(IRepository repository)
                    : base(4)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not duplicate diagnostics for an already configured generated constructor.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenGenerateConstructorIsAlreadyPresent()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public interface IRepository { }

            [GenerateConstructor]
            public partial class UserService
            {
                private readonly IRepository _repository;
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not duplicate a positive field guard that already triggers generation.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenFieldGuardAlreadyTriggersGeneration()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public interface IRepository { }

            public partial class UserService
            {
                [ConstructorGuard(ConstructorGuardKind.NotNull)]
                private readonly IRepository _repository;
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not suggest a field guard that Needlr rejects for a non-nullable value type.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenGuardIsIncompatibleWithFieldType()
    {
        var test = CreateTest("""
            public sealed class RetryPolicy
            {
                private readonly int _retryCount;

                public RetryPolicy(int retryCount)
                {
                    System.ArgumentNullException.ThrowIfNull(retryCount);
                    _retryCount = retryCount;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not recommend generated dependencies for plugins activated before DI exists.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_ForParameterlessActivationPlugin()
    {
        var test = CreateTest("""
            namespace NexusLabs.Needlr
            {
                public interface IServiceCollectionPlugin { }
            }

            public interface IRepository { }

            public sealed class UserPlugin :
                NexusLabs.Needlr.IServiceCollectionPlugin
            {
                private readonly IRepository _repository;

                public UserPlugin(IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not compete with a constructor supplied by another source generator.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenDeferToContainerIsApplied()
    {
        var test = CreateTest("""
            namespace NexusLabs.Needlr
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class DeferToContainerAttribute :
                    System.Attribute
                {
                }
            }

            public interface IRepository { }

            [NexusLabs.Needlr.DeferToContainer]
            public sealed class UserService
            {
                private readonly IRepository _repository;

                public UserService(IRepository repository)
                {
                    _repository = repository;
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reports primary-constructor parameters captured only by matching fields.
    /// </summary>
    [Fact]
    public async Task NDLRGEN063_ForPrimaryConstructorIdentityCaptures()
    {
        var test = CreateTest("""
            public interface IRepository { }
            public interface ILogger { }

            public sealed class {|#0:UserService|}(
                IRepository repository,
                ILogger logger)
            {
                private readonly IRepository _repository = repository;
                private readonly ILogger _logger = logger;
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN063", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("UserService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reports the exact guarded-primary-constructor migration documented by Needlr.
    /// </summary>
    [Fact]
    public async Task NDLRGEN063_ForSingleGuardedPrimaryConstructor()
    {
        var test = CreateTest("""
            #nullable enable

            public interface IRepository { }

            public sealed class {|#0:UserService|}(IRepository repository)
            {
                private readonly IRepository _repository =
                    repository ??
                    throw new System.ArgumentNullException(
                        nameof(repository));
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN063", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("UserService"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not claim identical exception timing for multiple guarded field captures.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenMultiplePrimaryParametersAreGuarded()
    {
        var test = CreateTest("""
            #nullable enable

            public interface IRepository { }
            public interface ILogger { }

            public sealed class UserService(
                IRepository repository,
                ILogger logger)
            {
                private readonly IRepository _repository =
                    repository ??
                    throw new System.ArgumentNullException(
                        nameof(repository));
                private readonly ILogger _logger =
                    logger ??
                    throw new System.ArgumentNullException(
                        nameof(logger));
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not report when a primary parameter remains part of authored behavior.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenPrimaryParameterIsUsedOutsideCapture()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class UserService(IRepository repository)
            {
                private readonly IRepository _repository = repository;

                public IRepository Repository => repository;
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not report when another instance initializer could change evaluation order.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenPrimaryConstructorHasAdditionalInitializer()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class UserService(IRepository repository)
            {
                private readonly IRepository _repository = repository;
                private readonly object _state = new();
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not infer moving value normalization into a separate factory or service.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_ForPrimaryConstructorWithNormalizedValue()
    {
        var test = CreateTest("""
            #nullable enable

            public interface IRepository { }

            public sealed class UserService(
                IRepository repository,
                string name)
            {
                private readonly IRepository _repository =
                    repository ??
                    throw new System.ArgumentNullException(
                        nameof(repository));
                private readonly string _name = name.Trim();
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not move a primary-constructor capture past another property initializer.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_WhenPrimaryConstructorHasPropertyInitializer()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class UserService(IRepository repository)
            {
                private readonly IRepository _repository = repository;

                public object State { get; } = new();
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not suggest an unsupported nested generated-constructor type.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_ForNestedClass()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed class Outer
            {
                public sealed class UserService
                {
                    private readonly IRepository _repository;

                    public UserService(IRepository repository)
                    {
                        _repository = repository;
                    }
                }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Does not suggest field-based constructor generation for records.
    /// </summary>
    [Fact]
    public async Task NoDiagnostic_ForRecord()
    {
        var test = CreateTest("""
            public interface IRepository { }

            public sealed record UserService(IRepository Repository);
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
