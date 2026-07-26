using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

/// <summary>
/// Truth tables for every <c>UsingOnlyAsScoped</c> overload, covering the cross product of the
/// underlying filterer results, target assignability, and the additional predicate result.
/// </summary>
public sealed class UsingOnlyAsScopedTruthTableTests
{
    [Theory]
    [InlineData(false, false, false, false, false, false, false)]
    [InlineData(false, false, false, true, true, false, false)]
    [InlineData(false, false, true, false, false, false, true)]
    [InlineData(false, false, true, true, true, false, false)]
    [InlineData(false, true, false, false, false, true, false)]
    [InlineData(false, true, false, true, true, false, false)]
    [InlineData(false, true, true, false, false, true, true)]
    [InlineData(false, true, true, true, true, false, false)]
    [InlineData(true, false, false, false, true, false, false)]
    [InlineData(true, false, false, true, true, false, false)]
    [InlineData(true, false, true, false, true, false, true)]
    [InlineData(true, false, true, true, true, false, false)]
    [InlineData(true, true, false, false, true, true, false)]
    [InlineData(true, true, false, true, true, false, false)]
    [InlineData(true, true, true, false, true, true, true)]
    [InlineData(true, true, true, true, true, false, false)]
    public void UsingOnlyAsScopedOfT_TruthTable(
        bool innerScoped,
        bool innerTransient,
        bool innerSingleton,
        bool assignableToTarget,
        bool expectedScoped,
        bool expectedTransient,
        bool expectedSingleton)
    {
        var filterer = new ConfigurableTypeFilterer(innerScoped, innerTransient, innerSingleton)
            .UsingOnlyAsScoped<ITruthTableTarget>();

        var candidate = assignableToTarget
            ? typeof(TruthTableTargetImpl)
            : typeof(UnrelatedTruthTableType);

        Assert.Equal(expectedScoped, filterer.IsInjectableScopedType(candidate));
        Assert.Equal(expectedTransient, filterer.IsInjectableTransientType(candidate));
        Assert.Equal(expectedSingleton, filterer.IsInjectableSingletonType(candidate));
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, false, false)]
    [InlineData(false, false, false, false, true, false, false, false)]
    [InlineData(false, false, false, true, false, false, false, false)]
    [InlineData(false, false, false, true, true, true, false, false)]
    [InlineData(false, false, true, false, false, false, false, true)]
    [InlineData(false, false, true, false, true, false, false, true)]
    [InlineData(false, false, true, true, false, false, false, true)]
    [InlineData(false, false, true, true, true, true, false, false)]
    [InlineData(false, true, false, false, false, false, true, false)]
    [InlineData(false, true, false, false, true, false, true, false)]
    [InlineData(false, true, false, true, false, false, true, false)]
    [InlineData(false, true, false, true, true, true, false, false)]
    [InlineData(false, true, true, false, false, false, true, true)]
    [InlineData(false, true, true, false, true, false, true, true)]
    [InlineData(false, true, true, true, false, false, true, true)]
    [InlineData(false, true, true, true, true, true, false, false)]
    [InlineData(true, false, false, false, false, true, false, false)]
    [InlineData(true, false, false, false, true, true, false, false)]
    [InlineData(true, false, false, true, false, true, false, false)]
    [InlineData(true, false, false, true, true, true, false, false)]
    [InlineData(true, false, true, false, false, true, false, true)]
    [InlineData(true, false, true, false, true, true, false, true)]
    [InlineData(true, false, true, true, false, true, false, true)]
    [InlineData(true, false, true, true, true, true, false, false)]
    [InlineData(true, true, false, false, false, true, true, false)]
    [InlineData(true, true, false, false, true, true, true, false)]
    [InlineData(true, true, false, true, false, true, true, false)]
    [InlineData(true, true, false, true, true, true, false, false)]
    [InlineData(true, true, true, false, false, true, true, true)]
    [InlineData(true, true, true, false, true, true, true, true)]
    [InlineData(true, true, true, true, false, true, true, true)]
    [InlineData(true, true, true, true, true, true, false, false)]
    public void UsingOnlyAsScopedOfTWithPredicate_TruthTable(
        bool innerScoped,
        bool innerTransient,
        bool innerSingleton,
        bool assignableToTarget,
        bool predicateResult,
        bool expectedScoped,
        bool expectedTransient,
        bool expectedSingleton)
    {
        var filterer = new ConfigurableTypeFilterer(innerScoped, innerTransient, innerSingleton)
            .UsingOnlyAsScoped<ITruthTableTarget>(_ => predicateResult);

        var candidate = assignableToTarget
            ? typeof(TruthTableTargetImpl)
            : typeof(UnrelatedTruthTableType);

        Assert.Equal(expectedScoped, filterer.IsInjectableScopedType(candidate));
        Assert.Equal(expectedTransient, filterer.IsInjectableTransientType(candidate));
        Assert.Equal(expectedSingleton, filterer.IsInjectableSingletonType(candidate));
    }

    [Theory]
    [InlineData(false, false, false, false, false, false, false)]
    [InlineData(false, false, false, true, true, false, false)]
    [InlineData(false, false, true, false, false, false, true)]
    [InlineData(false, false, true, true, true, false, false)]
    [InlineData(false, true, false, false, false, true, false)]
    [InlineData(false, true, false, true, true, false, false)]
    [InlineData(false, true, true, false, false, true, true)]
    [InlineData(false, true, true, true, true, false, false)]
    [InlineData(true, false, false, false, true, false, false)]
    [InlineData(true, false, false, true, true, false, false)]
    [InlineData(true, false, true, false, true, false, true)]
    [InlineData(true, false, true, true, true, false, false)]
    [InlineData(true, true, false, false, true, true, false)]
    [InlineData(true, true, false, true, true, false, false)]
    [InlineData(true, true, true, false, true, true, true)]
    [InlineData(true, true, true, true, true, false, false)]
    public void UsingOnlyAsScopedWithPredicate_TruthTable(
        bool innerScoped,
        bool innerTransient,
        bool innerSingleton,
        bool predicateResult,
        bool expectedScoped,
        bool expectedTransient,
        bool expectedSingleton)
    {
        var filterer = new ConfigurableTypeFilterer(innerScoped, innerTransient, innerSingleton)
            .UsingOnlyAsScoped(_ => predicateResult);

        var candidate = typeof(UnrelatedTruthTableType);

        Assert.Equal(expectedScoped, filterer.IsInjectableScopedType(candidate));
        Assert.Equal(expectedTransient, filterer.IsInjectableTransientType(candidate));
        Assert.Equal(expectedSingleton, filterer.IsInjectableSingletonType(candidate));
    }
}
