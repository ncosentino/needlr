using System.Linq;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Behavioral tests for <see cref="GeneratedConstructorGenerator"/>: constructor
/// generation, class-level null-guard modes, field-triggered generation, built-in
/// guards, guard composition, and custom guard resolution.
/// </summary>
public sealed class GeneratedConstructorGeneratorTests
{
    [Fact]
    public void BareGenerateConstructor_EmitsAssignmentsWithoutGuards()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public UserService(global::TestApp.IRepository repository)", generatedCode);
        Assert.Contains("_repository = repository;", generatedCode);
        Assert.DoesNotContain("ArgumentNullException", generatedCode);
    }

    [Fact]
    public void SealedPartialClass_GeneratesConstructorAndCompiles()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public sealed partial class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);
        var errors = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGeneratorCompilationErrors(new GeneratedConstructorGenerator());

        Assert.Contains("public UserService(global::TestApp.IRepository repository)", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void ExplicitNoneMode_MatchesBareAttribute()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor(ConstructorNullGuardMode.None)]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public UserService(global::TestApp.IRepository repository)", generatedCode);
        Assert.DoesNotContain("ArgumentNullException", generatedCode);
    }

    [Fact]
    public void NonNullableReferencesMode_GuardsNonNullableReferenceField()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("global::System.ArgumentNullException.ThrowIfNull(repository);", generatedCode);
        Assert.Contains("_repository = repository;", generatedCode);
    }

    [Fact]
    public void NonNullableReferencesMode_DoesNotGuardNullableReferenceField()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
                public partial class UserService
                {
                    private readonly IRepository? _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public UserService(global::TestApp.IRepository? repository)", generatedCode);
        Assert.DoesNotContain("ArgumentNullException", generatedCode);
    }

    [Fact]
    public void NonNullableReferencesMode_DoesNotGuardValueTypeField()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                [GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
                public partial class RetryPolicy
                {
                    private readonly int _retryCount;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public RetryPolicy(int retryCount)", generatedCode);
        Assert.DoesNotContain("ArgumentNullException", generatedCode);
    }

    [Fact]
    public void PositiveFieldGuard_EnablesGenerationWithoutClassAttribute()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                public partial class TenantService
                {
                    private readonly IRepository _repository;

                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    private readonly string _tenantName;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public TenantService(global::TestApp.IRepository repository, string tenantName)", generatedCode);
        Assert.Contains("global::System.ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);", generatedCode);
        Assert.DoesNotContain("ThrowIfNull(repository)", generatedCode);
    }

    [Fact]
    public void ConstructorIgnoreAlone_DoesNotEnableGeneration()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public partial class CacheService
                {
                    [ConstructorIgnore]
                    private readonly object _lock;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void ConstructorIgnore_ExcludesFieldFromGeneratedConstructor()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class CacheService
                {
                    private readonly IRepository _repository;

                    [ConstructorIgnore]
                    private readonly object _lock;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public CacheService(global::TestApp.IRepository repository)", generatedCode);
        Assert.DoesNotContain("_lock", generatedCode);
    }

    [Fact]
    public void FieldWithInitializer_IsExcludedFromGeneration()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class CacheService
                {
                    private readonly IRepository _repository;

                    private readonly int _count = 0;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public CacheService(global::TestApp.IRepository repository)", generatedCode);
        Assert.DoesNotContain("_count", generatedCode);
    }

    [Fact]
    public void MultipleFields_PreserveDeclarationOrderAndUnderscoreNaming()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }
                public interface ILogger { }

                [GenerateConstructor]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                    private readonly ILogger _logger;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public UserService(global::TestApp.IRepository repository, global::TestApp.ILogger logger)", generatedCode);

        var repositoryAssignIndex = generatedCode.IndexOf("_repository = repository;", System.StringComparison.Ordinal);
        var loggerAssignIndex = generatedCode.IndexOf("_logger = logger;", System.StringComparison.Ordinal);
        Assert.True(repositoryAssignIndex >= 0, "Expected the repository field assignment to be present");
        Assert.True(loggerAssignIndex > repositoryAssignIndex, "Expected assignments to preserve field declaration order");
    }

    [Fact]
    public void KeywordFieldName_IsEscapedWithAtPrefix()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                [GenerateConstructor]
                public partial class Wrapper
                {
                    private readonly string _class;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public Wrapper(string @class)", generatedCode);
        Assert.Contains("_class = @class;", generatedCode);
    }

    [Fact]
    public void BuiltInNotNullOrEmpty_EmitsExactGuardCall()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                [GenerateConstructor]
                public partial class OrderService
                {
                    [ConstructorGuard(ConstructorGuardKind.NotNullOrEmpty)]
                    private readonly string _orderId;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("global::System.ArgumentException.ThrowIfNullOrEmpty(orderId);", generatedCode);
    }

    [Fact]
    public void ExplicitNoneOnField_SuppressesClassDefaultGuard()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
                public partial class UserService
                {
                    [ConstructorGuard(ConstructorGuardKind.None)]
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public UserService(global::TestApp.IRepository repository)", generatedCode);
        Assert.DoesNotContain("ArgumentNullException", generatedCode);
    }

    [Fact]
    public void ClassDefaultAndExplicitGuard_ComposeInDeterministicOrder()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                [GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
                public partial class TenantService
                {
                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    private readonly string _tenantName;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        var notNullIndex = generatedCode.IndexOf("ArgumentNullException.ThrowIfNull(tenantName);", System.StringComparison.Ordinal);
        var notWhiteSpaceIndex = generatedCode.IndexOf("ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);", System.StringComparison.Ordinal);

        Assert.True(notNullIndex >= 0, "Expected the class-level default null guard to be emitted first");
        Assert.True(notWhiteSpaceIndex > notNullIndex, "Expected the explicit guard to follow the class-level default");
    }

    [Fact]
    public void DirectCustomGuardType_EmitsDirectMethodCall()
    {
        var source = """
            using System.Collections.Generic;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class CollectionNotEmptyGuard
                {
                    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName) { }
                }

                public class Order { }

                [GenerateConstructor]
                public partial class OrderService
                {
                    [ConstructorGuard(typeof(CollectionNotEmptyGuard))]
                    private readonly IReadOnlyCollection<Order> _orders;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("global::TestApp.CollectionNotEmptyGuard.Validate(orders, nameof(orders));", generatedCode);
    }

    [Fact]
    public void NamedCustomGuardMethod_EmitsDirectCallToSelectedMethod()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class NumberGuards
                {
                    public static void ValidatePositive(int value, string parameterName) { }
                }

                [GenerateConstructor]
                public partial class RetryPolicy
                {
                    [ConstructorGuard(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))]
                    private readonly int _retryCount;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("global::TestApp.NumberGuards.ValidatePositive(retryCount, nameof(retryCount));", generatedCode);
    }

    [Fact]
    public void AliasGuardAttribute_ResolvesToUnderlyingCustomGuard()
    {
        var source = """
            using System;
            using System.Collections.Generic;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class CollectionNotEmptyGuard
                {
                    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class CollectionNotEmptyAttribute : Attribute { }

                public class Order { }

                public partial class OrderService
                {
                    [CollectionNotEmpty]
                    private readonly IReadOnlyCollection<Order> _orders;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public OrderService(global::System.Collections.Generic.IReadOnlyCollection<global::TestApp.Order> orders)", generatedCode);
        Assert.Contains("global::TestApp.CollectionNotEmptyGuard.Validate(orders, nameof(orders));", generatedCode);
    }

    [Fact]
    public void ReferencedAssemblyAliasGuardAttribute_TriggersGenerationAndEmitsDirectGuardCall()
    {
        // The custom guard-alias attribute, its ConstructorGuardDefinition meta-attribute,
        // and the underlying guard type all live in a referenced assembly compiled
        // separately from the consuming source. Roslyn's cross-assembly symbol resolution
        // must still let ConstructorGenerationDiscoveryHelper inspect the meta-attribute
        // on FrameworkLib.CollectionNotEmptyAttribute and normalize it into the same guard
        // model as a same-project alias.
        var librarySource = """
            using System;
            using System.Collections.Generic;
            using NexusLabs.Needlr.Generators;

            namespace FrameworkLib
            {
                public static class CollectionNotEmptyGuard
                {
                    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class CollectionNotEmptyAttribute : Attribute { }
            }
            """;

        var consumerSource = """
            using System.Collections.Generic;
            using FrameworkLib;

            namespace TestApp
            {
                public class Order { }

                public partial class OrderService
                {
                    [CollectionNotEmpty]
                    private readonly IReadOnlyCollection<Order> _orders;
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForConstructorGeneration()
            .WithCrossAssemblySource("FrameworkLib", librarySource)
            .WithSource(consumerSource)
            .RunGenerator(new GeneratedConstructorGenerator());

        var content = generatedCode.Length == 0 ? string.Empty : string.Join("\n\n", generatedCode.Select(f => f.Content));

        Assert.Contains("public OrderService(global::System.Collections.Generic.IReadOnlyCollection<global::TestApp.Order> orders)", content);
        Assert.Contains("global::FrameworkLib.CollectionNotEmptyGuard.Validate(orders, nameof(orders));", content);
    }

    [Fact]
    public void GenericTypeParameter_IsPreservedOnGeneratedConstructor()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                [GenerateConstructor]
                public partial class Repository<T>
                {
                    private readonly T _value;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("partial class Repository<T>", generatedCode);
        Assert.Contains("public Repository(T value)", generatedCode);
    }

    [Fact]
    public void ConstrainedGenericTypeParameter_OmittingConstraintsOnGeneratedPartial_CompilesSuccessfully()
    {
        // The generator emits `partial class Repository<T>` with no `where` clause.
        // C# only requires generic constraints to be declared on (at most) one partial
        // declaration of a type, so the user's constrained declaration is sufficient and
        // the generated partial must compile cleanly without repeating them.
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class Repository<T> where T : class
                {
                    private readonly T _value;
                    private readonly IRepository _repository;
                }
            }
            """;

        var errors = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGeneratorCompilationErrors(new GeneratedConstructorGenerator());

        Assert.Empty(errors);
    }

    [Fact]
    public void MultipleConstrainedGenericTypeParameters_OmittingConstraintsOnGeneratedPartial_CompilesSuccessfully()
    {
        // Covers several distinct constraint kinds (reference type, value type,
        // parameterless-constructor, and interface constraint) across multiple type
        // parameters on the same generated partial declaration.
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IEntity { }

                [GenerateConstructor]
                public partial class Cache<TKey, TValue, TEntity>
                    where TKey : struct
                    where TValue : class, new()
                    where TEntity : IEntity
                {
                    private readonly TKey _key;
                    private readonly TValue _value;
                    private readonly TEntity _entity;
                }
            }
            """;

        var errors = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGeneratorCompilationErrors(new GeneratedConstructorGenerator());

        Assert.Empty(errors);
    }

    [Fact]
    public void NestedClass_IsExcludedFromGeneration()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                public partial class Outer
                {
                    [GenerateConstructor]
                    public partial class Inner
                    {
                        private readonly IRepository _repository;
                    }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void NonPartialClass_IsExcludedFromGeneration()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void ClassWithExistingExplicitConstructor_IsExcludedFromGeneration()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class UserService
                {
                    private readonly IRepository _repository;

                    public UserService(IRepository repository)
                    {
                        _repository = repository;
                    }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void GeneratedConstructor_HasXmlDocumentationForConstructorAndEveryParameter()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }
                public interface ILogger { }

                [GenerateConstructor]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                    private readonly ILogger _logger;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("/// <summary>", generatedCode);
        Assert.Contains("/// <param name=\"repository\">", generatedCode);
        Assert.Contains("/// <param name=\"logger\">", generatedCode);
    }

    [Fact]
    public void UnrelatedNamespace_GenerateConstructorAttribute_DoesNotTriggerGeneration()
    {
        // A same-named attribute declared in a different namespace must never be
        // confused with NexusLabs.Needlr.Generators.GenerateConstructorAttribute.
        var source = """
            namespace OtherLib
            {
                public class GenerateConstructorAttribute : System.Attribute { }
            }

            namespace TestApp
            {
                public interface IRepository { }

                [OtherLib.GenerateConstructor]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void UnrelatedNamespace_ConstructorGuardAttribute_DoesNotTriggerGeneration()
    {
        // A same-named field-level attribute from an unrelated namespace -- including one
        // whose constructor argument is itself an enum, so it parses the same shape as
        // the real ConstructorGuardAttribute -- must not be treated as a positive
        // field-level guard trigger.
        var source = """
            namespace OtherLib
            {
                public enum ConstructorGuardKind
                {
                    NotNullOrWhiteSpace = 2,
                }

                public class ConstructorGuardAttribute : System.Attribute
                {
                    public ConstructorGuardAttribute(ConstructorGuardKind kind) { }
                }
            }

            namespace TestApp
            {
                public partial class TenantService
                {
                    [OtherLib.ConstructorGuard(OtherLib.ConstructorGuardKind.NotNullOrWhiteSpace)]
                    private readonly string _tenantName;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void UnrelatedNamespace_ConstructorIgnoreAttribute_DoesNotExcludeField()
    {
        // A same-named exclusion attribute from an unrelated namespace must have no
        // effect: the decorated field must still become a constructor parameter.
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace OtherLib
            {
                public class ConstructorIgnoreAttribute : System.Attribute { }
            }

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class CacheService
                {
                    private readonly IRepository _repository;

                    [OtherLib.ConstructorIgnore]
                    private readonly string _tag;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public CacheService(global::TestApp.IRepository repository, string tag)", generatedCode);
    }

    [Fact]
    public void UnrelatedNamespace_ConstructorGuardDefinitionAttribute_DoesNotResolveAsAlias()
    {
        // A meta-attribute from an unrelated namespace, even with the same simple name
        // as NexusLabs.Needlr.Generators.ConstructorGuardDefinitionAttribute, must not be
        // recognized as a guard-alias definition, and therefore must not trigger
        // generation on its own.
        var source = """
            namespace OtherLib
            {
                public class ConstructorGuardDefinitionAttribute : System.Attribute
                {
                    public ConstructorGuardDefinitionAttribute(System.Type guardType) { }
                }
            }

            namespace TestApp
            {
                public static class SomeGuard
                {
                    public static void Validate(string value, string parameterName) { }
                }

                [OtherLib.ConstructorGuardDefinition(typeof(SomeGuard))]
                [System.AttributeUsage(System.AttributeTargets.Field)]
                public sealed class FakeAliasAttribute : System.Attribute { }

                public partial class OrderService
                {
                    [FakeAlias]
                    private readonly string _orderId;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void MultipleDirectGuardAttributes_OnSameField_ComposeAdditivelyInDeclarationOrder()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class OrderIdFormatGuard
                {
                    public static void Validate(string value, string parameterName) { }
                }

                [GenerateConstructor]
                public partial class OrderService
                {
                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    [ConstructorGuard(typeof(OrderIdFormatGuard))]
                    private readonly string _orderId;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        var whiteSpaceIndex = generatedCode.IndexOf("ArgumentException.ThrowIfNullOrWhiteSpace(orderId);", System.StringComparison.Ordinal);
        var customCallIndex = generatedCode.IndexOf("global::TestApp.OrderIdFormatGuard.Validate(orderId, nameof(orderId));", System.StringComparison.Ordinal);

        Assert.True(whiteSpaceIndex >= 0, "Expected the first explicit guard to be emitted");
        Assert.True(customCallIndex > whiteSpaceIndex, "Expected explicit guards to be emitted in declaration order");
    }

    [Fact]
    public void MultipleDirectGuardAttributes_OnSameField_CompileWithoutDuplicateAttributeError()
    {
        // AllowMultiple must be true on ConstructorGuardAttribute so this source shape
        // compiles cleanly; otherwise the C# compiler itself rejects it with CS0579
        // ("Duplicate ... attribute") before the generator's own logic is even relevant.
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class OrderIdFormatGuard
                {
                    public static void Validate(string value, string parameterName) { }
                }

                [GenerateConstructor]
                public partial class OrderService
                {
                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    [ConstructorGuard(typeof(OrderIdFormatGuard))]
                    private readonly string _orderId;
                }
            }
            """;

        var errors = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGeneratorCompilationErrors(new GeneratedConstructorGenerator());

        Assert.DoesNotContain(errors, d => d.Id == "CS0579");
    }

    [Fact]
    public void DuplicateIdenticalGuardAttributes_OnSameField_DeduplicateToSingleCall()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                [GenerateConstructor]
                public partial class OrderService
                {
                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    private readonly string _orderId;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        var occurrences = CountOccurrences(generatedCode, "ArgumentException.ThrowIfNullOrWhiteSpace(orderId);");
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void MultiPartialDeclaration_WithFieldsInBothParts_EmitsExactlyOneConstructor()
    {
        // Regression test for the per-type incremental pipeline: both partial parts
        // declare eligible fields, so both syntax nodes match the generator's
        // candidate predicate. Only the canonical (earliest, by file path then
        // position) field-bearing declaration must produce a model/output -- the
        // generator must never emit two constructors (or two "AddSource" hint-name
        // collisions) for the same merged type.
        var sourceA = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class SplitService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var sourceB = """
            namespace TestApp
            {
                public partial class SplitService
                {
                    private readonly string _label = "default";
                }
            }
            """;

        var files = GeneratorTestRunner.ForConstructorGeneration()
            .WithSources(sourceA, sourceB)
            .RunGenerator(new GeneratedConstructorGenerator());

        Assert.Single(files);

        var occurrences = CountOccurrences(files[0].Content, "public SplitService(");
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void SameNameDifferentArity_InSameNamespace_DoNotCollideOnHintNameOrOutput()
    {
        // Regression test for the deterministic, counter-free hint-name discriminator:
        // "Foo" and "Foo<T>" share a namespace and simple name, so without an arity
        // discriminator they would collide on the same AddSource hint name.
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class Foo
                {
                    private readonly IRepository _repository;
                }

                [GenerateConstructor]
                public partial class Foo<T>
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var files = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGenerator(new GeneratedConstructorGenerator());

        Assert.Equal(2, files.Length);

        var hintNames = files.Select(f => f.FilePath).ToArray();
        Assert.Equal(hintNames.Length, hintNames.Distinct().Count());

        var lines = files.SelectMany(f => f.Content.Split('\n')).Select(l => l.Trim()).ToArray();
        Assert.Contains("partial class Foo", lines);
        Assert.Contains("partial class Foo<T>", lines);
        Assert.All(files, f => Assert.Contains("public Foo(global::TestApp.IRepository repository)", f.Content));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string RunGenerator(string source)
    {
        var files = GeneratorTestRunner.ForConstructorGeneration()
            .WithDocumentationMode()
            .WithSource(source)
            .RunGenerator(new GeneratedConstructorGenerator());

        return files.Length == 0
            ? string.Empty
            : string.Join("\n\n", files.Select(f => f.Content));
    }
}
