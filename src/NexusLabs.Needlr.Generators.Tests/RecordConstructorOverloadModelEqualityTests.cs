using System;

using NexusLabs.Needlr.Generators.Models;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Equality-contract tests for record-constructor-overload incremental pipeline models.
/// </summary>
public sealed class RecordConstructorOverloadModelEqualityTests
{
    [Fact]
    public void RecordConstructorPrimaryParameter_EqualValuesSatisfyEqualityContract()
    {
        var first = CreatePrimaryParameter();
        var second = CreatePrimaryParameter();
        var third = CreatePrimaryParameter();

        AssertEqualityContract(first, second, third, (left, right) => left == right, (left, right) => left != right);
    }

    [Fact]
    public void RecordConstructorPrimaryParameter_EachSemanticFieldAffectsEquality()
    {
        var baseline = CreatePrimaryParameter();

        Assert.NotEqual(baseline, CreatePrimaryParameter(name: "Other"));
        Assert.NotEqual(baseline, CreatePrimaryParameter(escapedName: "@other"));
        Assert.NotEqual(baseline, CreatePrimaryParameter(typeName: "global::System.Object"));
        Assert.NotEqual(baseline, CreatePrimaryParameter(documentation: "Other documentation."));
    }

    [Fact]
    public void RecordConstructorPropertyParameter_EqualValuesSatisfyEqualityContract()
    {
        var first = CreatePropertyParameter();
        var second = CreatePropertyParameter(effectiveGuards: [CreateGuard()]);
        var third = CreatePropertyParameter(effectiveGuards: [CreateGuard()]);

        AssertEqualityContract(first, second, third, (left, right) => left == right, (left, right) => left != right);
    }

    [Fact]
    public void RecordConstructorPropertyParameter_EachSemanticFieldAffectsEquality()
    {
        var baseline = CreatePropertyParameter();

        Assert.NotEqual(baseline, CreatePropertyParameter(propertyName: "Other"));
        Assert.NotEqual(baseline, CreatePropertyParameter(escapedPropertyName: "@other"));
        Assert.NotEqual(baseline, CreatePropertyParameter(typeName: "global::System.Int64"));
        Assert.NotEqual(baseline, CreatePropertyParameter(documentation: "Other documentation."));
        Assert.NotEqual(baseline, CreatePropertyParameter(effectiveGuards: []));
        Assert.NotEqual(
            baseline,
            CreatePropertyParameter(effectiveGuards: [CreateGuard(), CreateGuard("Check")]));
        Assert.NotEqual(
            baseline,
            CreatePropertyParameter(effectiveGuards: [CreateGuard("Check"), CreateGuard()]));
        Assert.NotEqual(
            baseline,
            CreatePropertyParameter(effectiveGuards: [CreateGuard("Check")]));
    }

    [Fact]
    public void RecordConstructorOverloadModel_EqualValuesSatisfyEqualityContract()
    {
        var first = CreateModel();
        var second = CreateModel(
            primaryParameters: [CreatePrimaryParameter()],
            propertyParameters: [CreatePropertyParameter()]);
        var third = CreateModel(
            primaryParameters: [CreatePrimaryParameter()],
            propertyParameters: [CreatePropertyParameter()]);

        AssertEqualityContract(first, second, third, (left, right) => left == right, (left, right) => left != right);
    }

    [Fact]
    public void RecordConstructorOverloadModel_EachSemanticFieldAffectsEquality()
    {
        var baseline = CreateModel();

        Assert.NotEqual(baseline, CreateModel(containingNamespace: "Other"));
        Assert.NotEqual(baseline, CreateModel(containingTypeName: "OtherRequest"));
        Assert.NotEqual(baseline, CreateModel(escapedContainingTypeName: "@OtherRequest"));
        Assert.NotEqual(baseline, CreateModel(typeParameterList: "<TValue>"));
        Assert.NotEqual(baseline, CreateModel(arity: 2));
        Assert.NotEqual(baseline, CreateModel(primaryParameters: []));
        Assert.NotEqual(
            baseline,
            CreateModel(primaryParameters: [CreatePrimaryParameter(), CreatePrimaryParameter("Other", "other")]));
        Assert.NotEqual(
            baseline,
            CreateModel(primaryParameters: [CreatePrimaryParameter("Other", "other"), CreatePrimaryParameter()]));
        Assert.NotEqual(
            baseline,
            CreateModel(primaryParameters: [CreatePrimaryParameter(typeName: "global::System.Object")]));
        Assert.NotEqual(baseline, CreateModel(propertyParameters: []));
        Assert.NotEqual(
            baseline,
            CreateModel(propertyParameters: [CreatePropertyParameter(), CreatePropertyParameter("Other", "other")]));
        Assert.NotEqual(
            baseline,
            CreateModel(propertyParameters: [CreatePropertyParameter("Other", "other"), CreatePropertyParameter()]));
        Assert.NotEqual(
            baseline,
            CreateModel(propertyParameters: [CreatePropertyParameter(typeName: "global::System.Int64")]));
        Assert.NotEqual(baseline, CreateModel(sourceFilePath: null));
    }

    [Fact]
    public void RecordConstructorOverloadEmitContext_EqualValuesSatisfyEqualityContract()
    {
        var first = new RecordConstructorOverloadEmitContext("TestAssembly", BreadcrumbLevel.Minimal);
        var second = new RecordConstructorOverloadEmitContext("TestAssembly", BreadcrumbLevel.Minimal);
        var third = new RecordConstructorOverloadEmitContext("TestAssembly", BreadcrumbLevel.Minimal);

        AssertEqualityContract(first, second, third, (left, right) => left == right, (left, right) => left != right);
    }

    [Fact]
    public void RecordConstructorOverloadEmitContext_EachSemanticFieldAffectsEquality()
    {
        var baseline = new RecordConstructorOverloadEmitContext("TestAssembly", BreadcrumbLevel.Minimal);

        Assert.NotEqual(
            baseline,
            new RecordConstructorOverloadEmitContext("OtherAssembly", BreadcrumbLevel.Minimal));
        Assert.NotEqual(
            baseline,
            new RecordConstructorOverloadEmitContext("TestAssembly", BreadcrumbLevel.Verbose));
    }

    private static ConstructorGuardModel CreateGuard(string methodName = "Validate")
    {
        return new ConstructorGuardModel(
            GeneratedConstructorGuardKind.Custom,
            "global::TestApp.Guard",
            methodName,
            ["3"]);
    }

    private static RecordConstructorPrimaryParameter CreatePrimaryParameter(
        string name = "Name",
        string escapedName = "name",
        string typeName = "global::System.String",
        string documentation = "The name.")
    {
        return new RecordConstructorPrimaryParameter(name, escapedName, typeName, documentation);
    }

    private static RecordConstructorPropertyParameter CreatePropertyParameter(
        string propertyName = "Count",
        string escapedPropertyName = "count",
        string typeName = "global::System.Int32",
        string documentation = "The count.",
        ConstructorGuardModel[]? effectiveGuards = null)
    {
        return new RecordConstructorPropertyParameter(
            propertyName,
            escapedPropertyName,
            typeName,
            documentation,
            effectiveGuards ?? [CreateGuard()]);
    }

    private static RecordConstructorOverloadModel CreateModel(
        string containingNamespace = "TestApp",
        string containingTypeName = "Request",
        string escapedContainingTypeName = "Request",
        string typeParameterList = "<T>",
        int arity = 1,
        RecordConstructorPrimaryParameter[]? primaryParameters = null,
        RecordConstructorPropertyParameter[]? propertyParameters = null,
        string? sourceFilePath = "Request.cs")
    {
        return new RecordConstructorOverloadModel(
            containingNamespace,
            containingTypeName,
            escapedContainingTypeName,
            typeParameterList,
            arity,
            primaryParameters ?? [CreatePrimaryParameter()],
            propertyParameters ?? [CreatePropertyParameter()],
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
