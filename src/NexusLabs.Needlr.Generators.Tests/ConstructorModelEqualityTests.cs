using System;

using NexusLabs.Needlr.Generators.Models;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Equality-contract tests for generated-constructor incremental pipeline models.
/// </summary>
public sealed class ConstructorModelEqualityTests
{
    [Fact]
    public void ConstructorGuardModel_EqualValuesSatisfyEqualityContract()
    {
        var first = CreateGuard();
        var second = CreateGuard(forwardedArgumentLiterals: ["3", "\"name\""]);
        var third = CreateGuard(forwardedArgumentLiterals: ["3", "\"name\""]);

        AssertEqualityContract(first, second, third, (left, right) => left == right, (left, right) => left != right);
        Assert.Equal(
            new ConstructorGuardModel(
                GeneratedConstructorGuardKind.NotNull,
                customGuardTypeName: null,
                customGuardMethodName: null,
                forwardedArgumentLiterals: null),
            new ConstructorGuardModel(
                GeneratedConstructorGuardKind.NotNull,
                customGuardTypeName: null,
                customGuardMethodName: null,
                forwardedArgumentLiterals: []));
    }

    [Fact]
    public void ConstructorGuardModel_EachSemanticFieldAffectsEquality()
    {
        var baseline = CreateGuard();

        Assert.NotEqual(baseline, CreateGuard(kind: GeneratedConstructorGuardKind.NotNull));
        Assert.NotEqual(baseline, CreateGuard(customGuardTypeName: "global::OtherGuard"));
        Assert.NotEqual(baseline, CreateGuard(customGuardMethodName: "Check"));
        Assert.NotEqual(baseline, CreateGuard(forwardedArgumentLiterals: []));
        Assert.NotEqual(baseline, CreateGuard(forwardedArgumentLiterals: ["4", "\"name\""]));
        Assert.NotEqual(baseline, CreateGuard(forwardedArgumentLiterals: ["\"name\"", "3"]));
    }

    [Fact]
    public void EligibleConstructorField_EqualValuesSatisfyEqualityContract()
    {
        var first = CreateField();
        var second = CreateField(explicitGuards: [CreateGuard()]);
        var third = CreateField(explicitGuards: [CreateGuard()]);

        AssertEqualityContract(first, second, third, (left, right) => left == right, (left, right) => left != right);
    }

    [Fact]
    public void EligibleConstructorField_EachSemanticFieldAffectsEquality()
    {
        var baseline = CreateField();

        Assert.NotEqual(baseline, CreateField(fieldName: "_other"));
        Assert.NotEqual(baseline, CreateField(parameterName: "other"));
        Assert.NotEqual(baseline, CreateField(parameterTypeName: "global::System.String"));
        Assert.NotEqual(baseline, CreateField(isNonNullableReferenceType: false));
        Assert.NotEqual(baseline, CreateField(explicitGuards: []));
        Assert.NotEqual(
            baseline,
            CreateField(explicitGuards: [CreateGuard(), CreateGuard(kind: GeneratedConstructorGuardKind.NotNull)]));
        Assert.NotEqual(
            baseline,
            CreateField(explicitGuards: [CreateGuard(kind: GeneratedConstructorGuardKind.NotNull), CreateGuard()]));
        Assert.NotEqual(
            baseline,
            CreateField(explicitGuards: [CreateGuard(customGuardMethodName: "Check")]));
    }

    [Fact]
    public void GeneratedConstructorModel_EqualValuesSatisfyEqualityContract()
    {
        var first = CreateModel();
        var second = CreateModel(fields: [CreateField()]);
        var third = CreateModel(fields: [CreateField()]);

        AssertEqualityContract(first, second, third, (left, right) => left == right, (left, right) => left != right);
    }

    [Fact]
    public void GeneratedConstructorModel_EachSemanticFieldAffectsEquality()
    {
        var baseline = CreateModel();

        Assert.NotEqual(baseline, CreateModel(containingNamespace: "Other"));
        Assert.NotEqual(baseline, CreateModel(containingTypeName: "OtherService"));
        Assert.NotEqual(baseline, CreateModel(typeParameterList: "<TValue>"));
        Assert.NotEqual(baseline, CreateModel(arity: 2));
        Assert.NotEqual(
            baseline,
            CreateModel(nullGuardMode: GeneratedConstructorNullGuardMode.NonNullableReferences));
        Assert.NotEqual(baseline, CreateModel(fields: []));
        Assert.NotEqual(
            baseline,
            CreateModel(fields: [CreateField(), CreateField(fieldName: "_other", parameterName: "other")]));
        Assert.NotEqual(
            baseline,
            CreateModel(fields: [CreateField(fieldName: "_other", parameterName: "other"), CreateField()]));
        Assert.NotEqual(
            baseline,
            CreateModel(fields: [CreateField(parameterTypeName: "global::System.String")]));
        Assert.NotEqual(baseline, CreateModel(sourceFilePath: null));
    }

    [Fact]
    public void GeneratedConstructorEmitContext_EqualValuesSatisfyEqualityContract()
    {
        var first = new GeneratedConstructorEmitContext("TestAssembly", BreadcrumbLevel.Minimal);
        var second = new GeneratedConstructorEmitContext("TestAssembly", BreadcrumbLevel.Minimal);
        var third = new GeneratedConstructorEmitContext("TestAssembly", BreadcrumbLevel.Minimal);

        AssertEqualityContract(first, second, third, (left, right) => left == right, (left, right) => left != right);
    }

    [Fact]
    public void GeneratedConstructorEmitContext_EachSemanticFieldAffectsEquality()
    {
        var baseline = new GeneratedConstructorEmitContext("TestAssembly", BreadcrumbLevel.Minimal);

        Assert.NotEqual(
            baseline,
            new GeneratedConstructorEmitContext("OtherAssembly", BreadcrumbLevel.Minimal));
        Assert.NotEqual(
            baseline,
            new GeneratedConstructorEmitContext("TestAssembly", BreadcrumbLevel.Verbose));
    }

    private static ConstructorGuardModel CreateGuard(
        GeneratedConstructorGuardKind kind = GeneratedConstructorGuardKind.Custom,
        string? customGuardTypeName = "global::TestApp.Guard",
        string? customGuardMethodName = "Validate",
        string[]? forwardedArgumentLiterals = null)
    {
        return new ConstructorGuardModel(
            kind,
            customGuardTypeName,
            customGuardMethodName,
            forwardedArgumentLiterals ?? ["3", "\"name\""]);
    }

    private static EligibleConstructorField CreateField(
        string fieldName = "_repository",
        string parameterName = "repository",
        string parameterTypeName = "global::TestApp.IRepository",
        bool isNonNullableReferenceType = true,
        ConstructorGuardModel[]? explicitGuards = null)
    {
        return new EligibleConstructorField(
            fieldName,
            parameterName,
            parameterTypeName,
            isNonNullableReferenceType,
            explicitGuards ?? [CreateGuard()]);
    }

    private static GeneratedConstructorModel CreateModel(
        string containingNamespace = "TestApp",
        string containingTypeName = "Service",
        string typeParameterList = "<T>",
        int arity = 1,
        GeneratedConstructorNullGuardMode nullGuardMode = GeneratedConstructorNullGuardMode.None,
        EligibleConstructorField[]? fields = null,
        string? sourceFilePath = "Service.cs")
    {
        return new GeneratedConstructorModel(
            containingNamespace,
            containingTypeName,
            typeParameterList,
            arity,
            nullGuardMode,
            fields ?? [CreateField()],
            sourceFilePath);
    }

    private static void AssertEqualityContract<T>(
        T first,
        T second,
        T third,
        Func<T, T, bool> equalsOperator,
        Func<T, T, bool> notEqualsOperator)
        where T : struct, IEquatable<T>
    {
        Assert.True(first.Equals(first), "Expected equality to be reflexive.");
        Assert.True(first.Equals(second), "Expected equal values to compare equal.");
        Assert.True(second.Equals(first), "Expected equality to be symmetric.");
        Assert.True(second.Equals(third), "Expected the second and third values to compare equal.");
        Assert.True(first.Equals(third), "Expected equality to be transitive.");
        Assert.True(equalsOperator(first, second), "Expected the equality operator to return true.");
        Assert.False(notEqualsOperator(first, second), "Expected the inequality operator to return false.");
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(first.GetHashCode(), first.GetHashCode());
        Assert.False(first.Equals(null), "Expected Equals(object) to reject null.");
        Assert.False(first.Equals("unrelated"), "Expected Equals(object) to reject unrelated types.");
    }
}
