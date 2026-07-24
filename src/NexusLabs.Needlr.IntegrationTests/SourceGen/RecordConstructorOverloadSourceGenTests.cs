using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Executable behavior tests for generated positional-record constructor overloads.
/// </summary>
public sealed class RecordConstructorOverloadSourceGenTests
{
    [Fact]
    public void PrimaryConstructor_RemainsAvailableAndLeavesAddedPropertyUnset()
    {
        var request = CreatePrimary();

        Assert.Null(request.PreparedScope);
    }

    [Fact]
    public void GeneratedOverload_AssignsMarkedProperty()
    {
        var scope = new RcPreparedScope("prepared");

        var request = CreateWithScope(scope);

        Assert.Same(scope, request.PreparedScope);
    }

    [Fact]
    public void GeneratedOverload_NullMarkedPropertyThrowsExactArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CreateWithScope(null!));

        Assert.Equal("PreparedScope", exception.ParamName);
    }

    [Fact]
    public void WithExpression_RemainsLegalAndCanBypassConstructorGuard()
    {
        var request = CreateWithScope(new RcPreparedScope("prepared"));

        var changed = request with { PreparedScope = null };

        Assert.Null(changed.PreparedScope);
    }

    [Fact]
    public void ObjectInitializer_RemainsLegalAndCanBypassConstructorGuard()
    {
        var request = new RcVerificationRequest(
            "query",
            "tenant",
            25,
            false,
            Guid.Parse("01f69ec4-e5c1-4b10-b94d-b0dc82f4b66d"),
            DateTimeOffset.Parse("2026-07-23T12:00:00-07:00"),
            new[] { "one", "two" })
        {
            PreparedScope = null,
        };

        Assert.Null(request.PreparedScope);
    }

    [Fact]
    public void CopyConstructor_RemainsAvailableToRecordImplementation()
    {
        var scope = new RcPreparedScope("prepared");
        var request = CreateWithScope(scope);

        var copy = request.CopyViaConstructor();

        Assert.NotSame(request, copy);
        Assert.Equal(request, copy);
        Assert.Same(scope, copy.PreparedScope);
    }

    [Fact]
    public void ParameterizedCustomAlias_GuardsAndAssignsMarkedProperty()
    {
        var valid = new RcAliasRequest("request-1", "abc");

        Assert.Equal("abc", valid.Code);

        var exception = Assert.Throws<ArgumentException>(() => new RcAliasRequest("request-1", "ab"));
        Assert.Equal("Code", exception.ParamName);
    }

    [Fact]
    public void MarkedRecord_RemainsExcludedFromAutomaticInjectionDiscovery()
    {
        var serviceProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        Assert.Null(serviceProvider.GetService<RcVerificationRequest>());
    }

    private static RcVerificationRequest CreatePrimary()
    {
        return new RcVerificationRequest(
            "query",
            "tenant",
            25,
            false,
            Guid.Parse("01f69ec4-e5c1-4b10-b94d-b0dc82f4b66d"),
            DateTimeOffset.Parse("2026-07-23T12:00:00-07:00"),
            new[] { "one", "two" });
    }

    private static RcVerificationRequest CreateWithScope(RcPreparedScope scope)
    {
        return new RcVerificationRequest(
            "query",
            "tenant",
            25,
            false,
            Guid.Parse("01f69ec4-e5c1-4b10-b94d-b0dc82f4b66d"),
            DateTimeOffset.Parse("2026-07-23T12:00:00-07:00"),
            new[] { "one", "two" },
            scope);
    }
}

public sealed record RcPreparedScope(string Name);

public partial record RcVerificationRequest(
    string Query,
    string Tenant,
    int Limit,
    bool IncludeDrafts,
    Guid CorrelationId,
    DateTimeOffset RequestedAt,
    IReadOnlyList<string> Tags)
{
    [RecordConstructorOverloadParameter]
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    public RcPreparedScope? PreparedScope { get; init; }

    public RcVerificationRequest CopyViaConstructor() => new(this);
}

[ConstructorGuardDefinition(typeof(RcMinLengthGuard))]
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class RcMinLengthAttribute : Attribute
{
    public RcMinLengthAttribute(int minimum)
    {
    }
}

public static class RcMinLengthGuard
{
    public static void Validate(string? value, int minimum, string parameterName)
    {
        if (value is null || value.Length < minimum)
        {
            throw new ArgumentException($"Value must contain at least {minimum} characters.", parameterName);
        }
    }
}

public partial record RcAliasRequest(string Id)
{
    [RecordConstructorOverloadParameter]
    [RcMinLength(3)]
    public string? Code { get; init; }
}
