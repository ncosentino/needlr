using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests that verify the source generator correctly handles [RegisterClosedOverImplementationsOf]
/// attributes. For each discovered concrete closed implementation of the designated open generic
/// interface, the generator closes the annotated composition type over the same type argument(s)
/// and registers it as the designated facade service type, resolving constructor dependencies from DI.
/// </summary>
public sealed class RegisterClosedOverImplementationsOfGeneratorTests
{
    private const string Shapes = """
        namespace TestNamespace
        {
            public interface IFooDefinition<TData> where TData : class
            {
                string Discriminator { get; }
            }

            public interface IFooStore<TData> where TData : class { }

            public interface IFoo { string Discriminator { get; } }

            public sealed class AlphaData { }
            public sealed class BetaData { }

            public sealed class AlphaFoo : IFooDefinition<AlphaData> { public string Discriminator => "alpha"; }
            public sealed class BetaFoo : IFooDefinition<BetaData> { public string Discriminator => "beta"; }
        }
        """;

    [Fact]
    public void Composition_SingleImplementation_GeneratesClosedRegistrationAsFacade()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IBarDefinition<TData> where TData : class { }
                public interface IBar { }
                public sealed class OnlyData { }
                public sealed class OnlyBar : IBarDefinition<OnlyData> { }

                [RegisterClosedOverImplementationsOf(typeof(IBarDefinition<>), As = typeof(IBar))]
                public sealed class BarCore<TData> : IBar where TData : class
                {
                    public BarCore(IBarDefinition<TData> definition) { }
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.Contains("AddSingleton<global::TestNamespace.IBar>", generatedCode);
        Assert.Contains("new global::TestNamespace.BarCore<global::TestNamespace.OnlyData>", generatedCode);
        Assert.Contains("GetRequiredService<global::TestNamespace.IBarDefinition<global::TestNamespace.OnlyData>>", generatedCode);
    }

    [Fact]
    public void Composition_MultipleImplementations_GeneratesOnePerDiscoveredTypeArgument()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                [RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
                public sealed class FooCore<TData> : IFoo where TData : class
                {
                    public FooCore(IFooDefinition<TData> definition, IFooStore<TData> store) { }
                    public string Discriminator => "";
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .WithSource(Shapes)
            .RunTypeRegistryGenerator();

        Assert.Contains("new global::TestNamespace.FooCore<global::TestNamespace.AlphaData>", generatedCode);
        Assert.Contains("new global::TestNamespace.FooCore<global::TestNamespace.BetaData>", generatedCode);
    }

    [Fact]
    public void Composition_ResolvesEveryConstructorDependencyClosedOverTypeArgument()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                [RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
                public sealed class FooCore<TData> : IFoo where TData : class
                {
                    public FooCore(IFooDefinition<TData> definition, IFooStore<TData> store) { }
                    public string Discriminator => "";
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .WithSource(Shapes)
            .RunTypeRegistryGenerator();

        Assert.Contains("GetRequiredService<global::TestNamespace.IFooDefinition<global::TestNamespace.AlphaData>>", generatedCode);
        Assert.Contains("GetRequiredService<global::TestNamespace.IFooStore<global::TestNamespace.AlphaData>>", generatedCode);
    }

    [Fact]
    public void Composition_NoImplementations_GeneratesNoRegistrations()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IBazDefinition<TData> where TData : class { }
                public interface IBaz { }

                [RegisterClosedOverImplementationsOf(typeof(IBazDefinition<>), As = typeof(IBaz))]
                public sealed class BazCore<TData> : IBaz where TData : class
                {
                    public BazCore(IBazDefinition<TData> definition) { }
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.DoesNotContain("new global::TestNamespace.BazCore<", generatedCode);
    }

    [Fact]
    public void Composition_LifetimeScoped_GeneratesScopedRegistration()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                [RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo), Lifetime = InjectableLifetime.Scoped)]
                public sealed class FooCore<TData> : IFoo where TData : class
                {
                    public FooCore(IFooDefinition<TData> definition) { }
                    public string Discriminator => "";
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .WithSource(Shapes)
            .RunTypeRegistryGenerator();

        Assert.Contains("AddScoped<global::TestNamespace.IFoo>", generatedCode);
        Assert.DoesNotContain("AddSingleton<global::TestNamespace.IFoo>", generatedCode);
    }

    [Fact]
    public void Composition_TwoTypeParameters_ClosesCompositionOverBothArguments()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IPairDefinition<TKey, TValue> where TKey : class where TValue : class { }
                public interface IPair { }
                public sealed class KeyData { }
                public sealed class ValueData { }
                public sealed class KvPair : IPairDefinition<KeyData, ValueData> { }

                [RegisterClosedOverImplementationsOf(typeof(IPairDefinition<,>), As = typeof(IPair))]
                public sealed class PairCore<TKey, TValue> : IPair
                    where TKey : class where TValue : class
                {
                    public PairCore(IPairDefinition<TKey, TValue> definition) { }
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.Contains("new global::TestNamespace.PairCore<global::TestNamespace.KeyData, global::TestNamespace.ValueData>", generatedCode);
    }

    [Fact]
    public void Composition_TypeArgumentViolatesConstraint_ReportsDiagnosticAndSkips()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IQuxDefinition<TData> { }
                public struct StructData { }
                public sealed class StructQux : IQuxDefinition<StructData> { }
                public interface IQux { }

                [RegisterClosedOverImplementationsOf(typeof(IQuxDefinition<>), As = typeof(IQux))]
                public sealed class QuxCore<TData> : IQux where TData : class
                {
                    public QuxCore(IQuxDefinition<TData> definition) { }
                }
            }
            """;

        var runner = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source);

        var generatedCode = runner.RunTypeRegistryGenerator();
        var diagnostics = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        Assert.DoesNotContain("new global::TestNamespace.QuxCore<global::TestNamespace.StructData>", generatedCode);
        Assert.Contains(diagnostics, d => d.Id == "NDLRGEN038");
    }

    [Fact]
    public void Composition_KeyedConstructorDependency_ResolvesAsKeyedService()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace Microsoft.Extensions.DependencyInjection
            {
                [System.AttributeUsage(System.AttributeTargets.Parameter)]
                public sealed class FromKeyedServicesAttribute : System.Attribute
                {
                    public FromKeyedServicesAttribute(object key) { Key = key; }
                    public object Key { get; }
                }
            }

            namespace TestNamespace
            {
                [RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo))]
                public sealed class FooCore<TData> : IFoo where TData : class
                {
                    public FooCore(
                        IFooDefinition<TData> definition,
                        [Microsoft.Extensions.DependencyInjection.FromKeyedServices("primary")] IFooStore<TData> store) { }
                    public string Discriminator => "";
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .WithSource(Shapes)
            .RunTypeRegistryGenerator();

        Assert.Contains("GetRequiredKeyedService<global::TestNamespace.IFooStore<global::TestNamespace.AlphaData>>(\"primary\")", generatedCode);
        Assert.Contains("GetRequiredService<global::TestNamespace.IFooDefinition<global::TestNamespace.AlphaData>>", generatedCode);
    }

    [Fact]
    public void Composition_DefinitionInReferencedAssembly_IsComposedOver()
    {
        // A definition living in a referenced library (no [GenerateTypeRegistry]) is auto-registered by the
        // current assembly, so the composition must also close over it — same set decorators expand over.
        var externalSource = """
            namespace ExternalLib
            {
                public interface IExtDefinition<TData> where TData : class { }
                public interface IExt { }
                public sealed class ExtData { }
                public sealed class ExtDefinition : IExtDefinition<ExtData> { }
            }
            """;

        var mainSource = """
            using NexusLabs.Needlr.Generators;
            using ExternalLib;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace", "ExternalLib" })]

            namespace TestNamespace
            {
                [RegisterClosedOverImplementationsOf(typeof(IExtDefinition<>), As = typeof(IExt))]
                public sealed class ExtCore<TData> : IExt where TData : class
                {
                    public ExtCore(IExtDefinition<TData> definition) { }
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithCrossAssemblySource("ExternalLib", externalSource)
            .WithSource(mainSource)
            .RunTypeRegistryGenerator();

        Assert.Contains("new global::TestNamespace.ExtCore<global::ExternalLib.ExtData>", generatedCode);
        Assert.Contains("GetRequiredService<global::ExternalLib.IExtDefinition<global::ExternalLib.ExtData>>", generatedCode);
    }

    [Fact]
    public void Composition_LifetimeTransient_GeneratesTransientRegistration()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                [RegisterClosedOverImplementationsOf(typeof(IFooDefinition<>), As = typeof(IFoo), Lifetime = InjectableLifetime.Transient)]
                public sealed class FooCore<TData> : IFoo where TData : class
                {
                    public FooCore(IFooDefinition<TData> definition) { }
                    public string Discriminator => "";
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .WithSource(Shapes)
            .RunTypeRegistryGenerator();

        Assert.Contains("AddTransient<global::TestNamespace.IFoo>", generatedCode);
        Assert.DoesNotContain("AddSingleton<global::TestNamespace.IFoo>", generatedCode);
    }

    [Fact]
    public void Composition_MultipleMarkers_RegistersUnderEachFacade()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IMultiDefinition<TData> where TData : class { }
                public interface IMultiA { }
                public interface IMultiB { }
                public sealed class MultiData { }
                public sealed class MultiImpl : IMultiDefinition<MultiData> { }

                [RegisterClosedOverImplementationsOf(typeof(IMultiDefinition<>), As = typeof(IMultiA))]
                [RegisterClosedOverImplementationsOf(typeof(IMultiDefinition<>), As = typeof(IMultiB))]
                public sealed class MultiCore<TData> : IMultiA, IMultiB where TData : class
                {
                    public MultiCore(IMultiDefinition<TData> definition) { }
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.Contains("AddSingleton<global::TestNamespace.IMultiA>", generatedCode);
        Assert.Contains("AddSingleton<global::TestNamespace.IMultiB>", generatedCode);
    }

    [Fact]
    public void Composition_NullableValueTypeArgumentViolatesNotNullConstraint_ReportsDiagnosticAndSkips()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface INnDefinition<TData> { }
                public sealed class RefData { }
                public sealed class RefHolder : INnDefinition<RefData> { }
                public sealed class NullableHolder : INnDefinition<int?> { }
                public interface INn { }

                [RegisterClosedOverImplementationsOf(typeof(INnDefinition<>), As = typeof(INn))]
                public sealed class NnCore<TData> : INn where TData : notnull
                {
                    public NnCore(INnDefinition<TData> definition) { }
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();
        var diagnostics = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        // The non-nullable reference type registers; the nullable value type (int?) is skipped + diagnosed.
        Assert.Contains("new global::TestNamespace.NnCore<global::TestNamespace.RefData>", generatedCode);
        var registrationCount = System.Text.RegularExpressions.Regex
            .Matches(generatedCode, "new global::TestNamespace.NnCore<").Count;
        Assert.Equal(1, registrationCount);
        Assert.Contains(diagnostics, d => d.Id == "NDLRGEN038");
    }

    [Fact]
    public void Composition_CovariantConstraintSatisfiedByVariance_IsNotFalseSkipped()
    {
        // Regression guard: a fixed covariant generic constraint must NOT skip a type that satisfies it via
        // variance. DogProducer : IProducer<Dog> satisfies `where TData : IProducer<Animal>` because
        // IProducer is covariant, so the registration must be emitted with no NDLRGEN038.
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestNamespace" })]

            namespace TestNamespace
            {
                public interface IProducer<out T> { }
                public class Animal { }
                public sealed class Dog : Animal { }
                public sealed class DogProducer : IProducer<Dog> { }

                public interface IVarDefinition<TData> { }
                public sealed class VarHolder : IVarDefinition<DogProducer> { }
                public interface IVar { }

                [RegisterClosedOverImplementationsOf(typeof(IVarDefinition<>), As = typeof(IVar))]
                public sealed class VarCore<TData> : IVar where TData : IProducer<Animal>
                {
                    public VarCore(IVarDefinition<TData> definition) { }
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGenerator();
        var diagnostics = GeneratorTestRunner.ForComposedWithInlineTypes()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        Assert.Contains("new global::TestNamespace.VarCore<global::TestNamespace.DogProducer>", generatedCode);
        Assert.DoesNotContain(diagnostics, d => d.Id == "NDLRGEN038");
    }
}
