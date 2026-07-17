using System.Collections;
using System.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseResourceLockProviderTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public void PublicApi_ExposesOnlyTokenlessAndCanonicalNonOptionalOverloads()
    {
        AssertSurface(typeof(ILangfuseResourceLockProvider), expectAbstract: true);
        AssertSurface(typeof(LangfuseInProcessResourceLockProvider), expectAbstract: false);
    }

    [Fact]
    public async Task TokenlessAcquireAsync_DelegatesWithoutCancellationAndPreservesExclusivity()
    {
        var provider = new LangfuseInProcessResourceLockProvider();
        var key = LangfuseResourceLockKey.Create("project", "model", "gpt");
        await using var firstLease = await provider.AcquireAsync(key, _cancellationToken);

#pragma warning disable xUnit1051 // Intentionally exercises the tokenless resource-lock overload.
        var secondLeaseTask = provider.AcquireAsync(key).AsTask();
#pragma warning restore xUnit1051

        Assert.False(secondLeaseTask.IsCompleted);

        await firstLease.DisposeAsync();
        await using var secondLease = await secondLeaseTask;
    }

    [Fact]
    public async Task CanonicalAcquireAsync_CancellationReleasesWaitingReference()
    {
        var provider = new LangfuseInProcessResourceLockProvider();
        var key = LangfuseResourceLockKey.Create("project", "score-config", "quality");
        await using var firstLease = await provider.AcquireAsync(key, _cancellationToken);
        using var cancellation = new CancellationTokenSource();

        var waitingLeaseTask = provider.AcquireAsync(key, cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitingLeaseTask);
        await firstLease.DisposeAsync();

        var replacementLease = await provider.AcquireAsync(key, _cancellationToken);
        await replacementLease.DisposeAsync();

        Assert.Equal(0, GetEntryCount(provider));
    }

    [Fact]
    public async Task EquivalentKeys_ShareOneLockAndLeaseDisposalIsIdempotent()
    {
        var provider = new LangfuseInProcessResourceLockProvider();
        var firstKey = LangfuseResourceLockKey.Create("project", "model", "gpt");
        var equivalentKey = LangfuseResourceLockKey.Create("project", "model", "gpt");
        var firstLease = await provider.AcquireAsync(firstKey, _cancellationToken);
        var secondLeaseTask = provider.AcquireAsync(equivalentKey, _cancellationToken).AsTask();

        Assert.Equal(firstKey, equivalentKey);
        Assert.False(secondLeaseTask.IsCompleted);

        await firstLease.DisposeAsync();
        await firstLease.DisposeAsync();
        await using var secondLease = await secondLeaseTask;
    }

    [Fact]
    public async Task AcquireAsync_RejectsNullKeys()
    {
        var provider = new LangfuseInProcessResourceLockProvider();

#pragma warning disable xUnit1051 // Intentionally exercises the tokenless resource-lock overload.
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.AcquireAsync(null!).AsTask());
#pragma warning restore xUnit1051
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.AcquireAsync(null!, _cancellationToken).AsTask());
    }

    [Theory]
    [InlineData(null, "model", "gpt")]
    [InlineData("", "model", "gpt")]
    [InlineData(" ", "model", "gpt")]
    [InlineData("project", null, "gpt")]
    [InlineData("project", "", "gpt")]
    [InlineData("project", " ", "gpt")]
    [InlineData("project", "model", null)]
    [InlineData("project", "model", "")]
    [InlineData("project", "model", " ")]
    public void Create_RejectsMissingKeyComponents(
        string? scope,
        string? resourceType,
        string? resourceName)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => LangfuseResourceLockKey.Create(scope!, resourceType!, resourceName!));
    }

    private static void AssertSurface(Type type, bool expectAbstract)
    {
        var methods = type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == nameof(ILangfuseResourceLockProvider.AcquireAsync))
            .OrderBy(method => method.GetParameters().Length)
            .ToArray();

        Assert.Equal(2, methods.Length);
        AssertMethod(
            methods[0],
            expectAbstract,
            typeof(LangfuseResourceLockKey));
        AssertMethod(
            methods[1],
            expectAbstract,
            typeof(LangfuseResourceLockKey),
            typeof(CancellationToken));
        Assert.All(
            methods.SelectMany(method => method.GetParameters()),
            parameter => Assert.False(parameter.IsOptional));
    }

    private static void AssertMethod(
        MethodInfo method,
        bool expectAbstract,
        params Type[] parameterTypes)
    {
        Assert.Equal(typeof(ValueTask<IAsyncDisposable>), method.ReturnType);
        Assert.Equal(expectAbstract, method.IsAbstract);
        Assert.Equal(
            parameterTypes,
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    private static int GetEntryCount(LangfuseInProcessResourceLockProvider provider)
    {
        var entriesField = typeof(LangfuseInProcessResourceLockProvider).GetField(
            "_entries",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var entries = Assert.IsAssignableFrom<IDictionary>(entriesField!.GetValue(provider));
        return entries.Count;
    }
}
