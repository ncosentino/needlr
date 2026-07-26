using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// The constraint matrix for <c>[RegisterClosedOverImplementationsOf]</c>. For every generic
/// constraint Needlr validates itself, a discovered type argument that satisfies it must produce a
/// closed registration, and one that violates it must be skipped with NDLRGEN038 so invalid closed
/// generics never reach emitted C#. Constraints that are intentionally deferred to the C# compiler
/// must never cause a valid closure to be skipped.
/// </summary>
public sealed class RegisterClosedOverImplementationsOfConstraintTests
{
    [Fact]
    public void ClassConstraint_SatisfiedByReferenceType_RegistersClosedComposition()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public sealed class RefData { }
                public sealed class RefHolder : ICaseDefinition<RefData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : class
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.RefData>");
        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 1);
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void ClassConstraint_ViolatedByValueType_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public struct ValueData { public int Value; }
                public sealed class ValueHolder : ICaseDefinition<ValueData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : class
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.ValueData");
    }

    [Fact]
    public void StructConstraint_SatisfiedByValueType_RegistersClosedComposition()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public struct ValueData { public int Value; }
                public sealed class ValueHolder : ICaseDefinition<ValueData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : struct
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.ValueData>");
        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 1);
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void StructConstraint_ViolatedByReferenceType_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public sealed class RefData { }
                public sealed class RefHolder : ICaseDefinition<RefData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : struct
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.RefData");
    }

    [Fact]
    public void StructConstraint_ViolatedByNullableValueType_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public struct ValueData { public int Value; }
                public sealed class NullableHolder : ICaseDefinition<ValueData?> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : struct
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.ValueData?");
    }

    [Fact]
    public void UnmanagedConstraint_SatisfiedByUnmanagedStruct_RegistersClosedComposition()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public struct UnmanagedData { public int Value; }
                public sealed class UnmanagedHolder : ICaseDefinition<UnmanagedData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : unmanaged
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.UnmanagedData>");
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void UnmanagedConstraint_ViolatedByManagedStruct_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public struct ManagedData { public string Value; }
                public sealed class ManagedHolder : ICaseDefinition<ManagedData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : unmanaged
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.ManagedData");
    }

    [Fact]
    public void NotNullConstraint_SatisfiedByValueTypeAndReferenceType_RegistersBothClosedCompositions()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public struct ValueData { public int Value; }
                public sealed class RefData { }
                public sealed class ValueHolder : ICaseDefinition<ValueData> { }
                public sealed class RefHolder : ICaseDefinition<RefData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : notnull
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.ValueData>");
        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.RefData>");
        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 2);
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void NotNullConstraint_ViolatedByNullableValueType_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public struct ValueData { public int Value; }
                public sealed class NullableHolder : ICaseDefinition<ValueData?> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : notnull
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.ValueData?");
    }

    [Fact]
    public void NewConstraint_SatisfiedByValueType_RegistersClosedComposition()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public struct ValueData { public int Value; }
                public sealed class ValueHolder : ICaseDefinition<ValueData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : new()
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.ValueData>");
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void NewConstraint_SatisfiedByPublicParameterlessClass_RegistersClosedComposition()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public sealed class RefData { }
                public sealed class RefHolder : ICaseDefinition<RefData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : new()
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.RefData>");
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void NewConstraint_ViolatedByAbstractClass_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public abstract class AbstractData { }
                public sealed class AbstractHolder : ICaseDefinition<AbstractData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : new()
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.AbstractData");
    }

    [Fact]
    public void NewConstraint_ViolatedByClassWithoutParameterlessConstructor_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                [NexusLabs.Needlr.DoNotAutoRegister]
                public sealed class NoDefaultCtorData { public NoDefaultCtorData(int value) { } }

                public sealed class NoDefaultCtorHolder : ICaseDefinition<NoDefaultCtorData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : new()
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.NoDefaultCtorData");
    }

    [Fact]
    public void NewConstraint_ViolatedByNonPublicParameterlessConstructor_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                [NexusLabs.Needlr.DoNotAutoRegister]
                public sealed class PrivateCtorData { private PrivateCtorData() { } }

                public sealed class PrivateCtorHolder : ICaseDefinition<PrivateCtorData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : new()
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.PrivateCtorData");
    }

    [Fact]
    public void NewConstraint_ViolatedByArrayTypeArgument_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public sealed class RefData { }
                public sealed class ArrayHolder : ICaseDefinition<RefData[]> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : new()
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.RefData[]");
    }

    [Fact]
    public void BaseClassConstraint_SatisfiedByExactTypeAndDerivedType_RegistersBothClosedCompositions()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public class BaseData { }
                public sealed class DerivedData : BaseData { }
                public sealed class BaseHolder : ICaseDefinition<BaseData> { }
                public sealed class DerivedHolder : ICaseDefinition<DerivedData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : BaseData
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.BaseData>");
        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.DerivedData>");
        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 2);
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void BaseClassConstraint_ViolatedByUnrelatedType_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public class BaseData { }
                public sealed class UnrelatedData { }
                public sealed class UnrelatedHolder : ICaseDefinition<UnrelatedData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : BaseData
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.UnrelatedData");
    }

    [Fact]
    public void InterfaceConstraint_SatisfiedDirectlyAndThroughBaseClass_RegistersBothClosedCompositions()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public interface ICaseMarker { }
                public sealed class DirectMarkerData : ICaseMarker { }
                public class MarkerBase : ICaseMarker { }
                public sealed class InheritedMarkerData : MarkerBase { }
                public sealed class DirectHolder : ICaseDefinition<DirectMarkerData> { }
                public sealed class InheritedHolder : ICaseDefinition<InheritedMarkerData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : ICaseMarker
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.DirectMarkerData>");
        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.InheritedMarkerData>");
        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 2);
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void InterfaceConstraint_ViolatedByUnrelatedType_ReportsDiagnosticAndSkips()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public interface ICaseMarker { }
                public sealed class UnmarkedData { }
                public sealed class UnmarkedHolder : ICaseDefinition<UnmarkedData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : ICaseMarker
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 0);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(result, "global::TestNamespace.UnmarkedData");
    }

    [Fact]
    public void MultipleConstraints_EachOneIndividuallyViolated_SkipsOnlyTheViolatingClosures()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public interface ICaseMarker { }

                public sealed class ValidData : ICaseMarker { }
                public struct MarkedValueData : ICaseMarker { }
                public sealed class UnmarkedData { }

                [NexusLabs.Needlr.DoNotAutoRegister]
                public sealed class MarkedNoCtorData : ICaseMarker { public MarkedNoCtorData(int value) { } }

                public sealed class ValidHolder : ICaseDefinition<ValidData> { }
                public sealed class ValueHolder : ICaseDefinition<MarkedValueData> { }
                public sealed class UnmarkedHolder : ICaseDefinition<UnmarkedData> { }
                public sealed class NoCtorHolder : ICaseDefinition<MarkedNoCtorData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : class, ICaseMarker, new()
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.ValidData>");
        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "CaseCore", 1);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(
            result,
            "global::TestNamespace.MarkedValueData",
            "global::TestNamespace.UnmarkedData",
            "global::TestNamespace.MarkedNoCtorData");
    }

    [Fact]
    public void TwoTypeParameters_OnlySecondArgumentViolates_SkipsOnlyThatPair()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public sealed class KeyData { }
                public struct ValueData { public int Value; }
                public sealed class ValidPair : IPairCaseDefinition<KeyData, ValueData> { }
                public sealed class InvalidPair : IPairCaseDefinition<KeyData, KeyData> { }

                [RegisterClosedOverImplementationsOf(typeof(IPairCaseDefinition<,>), As = typeof(ICase))]
                public sealed class PairCore<TKey, TValue> : ICase
                    where TKey : class
                    where TValue : struct
                {
                    public PairCore(IPairCaseDefinition<TKey, TValue> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(
            result,
            "PairCore<global::TestNamespace.KeyData, global::TestNamespace.ValueData>");
        ClosedOverConstraintCaseRunner.AssertClosedRegistrationCount(result, "PairCore", 1);
        ClosedOverConstraintCaseRunner.AssertConstraintViolations(
            result,
            "global::TestNamespace.KeyData, global::TestNamespace.KeyData");
    }

    [Fact]
    public void InvariantGenericConstraint_SatisfiedExactly_IsNotFalseSkipped()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public interface IBox<T> { }
                public sealed class BoxContent { }
                public sealed class BoxData : IBox<BoxContent> { }
                public sealed class BoxHolder : ICaseDefinition<BoxData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : IBox<BoxContent>
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.BoxData>");
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void SelfReferentialGenericConstraint_Satisfied_IsNotFalseSkipped()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public sealed class ComparableData : System.IComparable<ComparableData>
                {
                    public int CompareTo(ComparableData other) => 0;
                }

                public sealed class ComparableHolder : ICaseDefinition<ComparableData> { }

                [RegisterClosedOverImplementationsOf(typeof(ICaseDefinition<>), As = typeof(ICase))]
                public sealed class CaseCore<TData> : ICase where TData : System.IComparable<TData>
                {
                    public CaseCore(ICaseDefinition<TData> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(result, "CaseCore<global::TestNamespace.ComparableData>");
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }

    [Fact]
    public void BareTypeParameterConstraint_Satisfied_IsNotFalseSkipped()
    {
        var result = ClosedOverConstraintCaseRunner.Run("""
                public class AnimalData { }
                public sealed class DogData : AnimalData { }
                public sealed class AnimalPair : IPairCaseDefinition<AnimalData, DogData> { }

                [RegisterClosedOverImplementationsOf(typeof(IPairCaseDefinition<,>), As = typeof(ICase))]
                public sealed class PairCore<TKey, TValue> : ICase
                    where TKey : class
                    where TValue : TKey
                {
                    public PairCore(IPairCaseDefinition<TKey, TValue> definition) { }
                }
            """);

        ClosedOverConstraintCaseRunner.AssertClosedOver(
            result,
            "PairCore<global::TestNamespace.AnimalData, global::TestNamespace.DogData>");
        ClosedOverConstraintCaseRunner.AssertNoConstraintViolations(result);
    }
}
