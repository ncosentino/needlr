using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

/// <summary>
/// Covers type shapes, chained lifetime overrides, exclusion precedence, predicate short-circuiting,
/// and argument guards for the <see cref="ITypeFilterer"/> lifetime extension methods.
/// </summary>
public sealed class TypeFiltererCombinationTests
{
    [Theory]
    [InlineData(typeof(IShapeTarget), true)]
    [InlineData(typeof(ShapeBase), true)]
    [InlineData(typeof(ShapeDerived), true)]
    [InlineData(typeof(ShapeContainer.NestedShape), true)]
    [InlineData(typeof(OpenShape<int>), true)]
    [InlineData(typeof(OpenShape<>), true)]
    [InlineData(typeof(UnrelatedShape), false)]
    public void UsingOnlyAsSingletonOfT_AppliesToAssignableTypeShapesOnly(
        Type candidate,
        bool expectedSingleton)
    {
        var filterer = new ConfigurableTypeFilterer(false, false, false)
            .UsingOnlyAsSingleton<IShapeTarget>();

        Assert.Equal(expectedSingleton, filterer.IsInjectableSingletonType(candidate));
        Assert.False(filterer.IsInjectableScopedType(candidate));
        Assert.False(filterer.IsInjectableTransientType(candidate));
    }

    [Fact]
    public void UsingOnlyAsSingletonOfT_ClosedGenericTarget_MatchesOnlyThatClosing()
    {
        var filterer = new ConfigurableTypeFilterer(false, false, false)
            .UsingOnlyAsSingleton<IGenericShape<int>>();

        Assert.True(filterer.IsInjectableSingletonType(typeof(GenericShape<int>)), "Expected the matching closed generic to be overridden");
        Assert.False(filterer.IsInjectableSingletonType(typeof(GenericShape<string>)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(GenericShape<>)));
    }

    [Fact]
    public void ChainedLifetimeOverrides_LastOverrideWins()
    {
        var filterer = new ConfigurableTypeFilterer(false, false, false)
            .UsingOnlyAsScoped<IShapeTarget>()
            .UsingOnlyAsTransient<IShapeTarget>();

        var candidate = typeof(ShapeBase);

        Assert.False(filterer.IsInjectableScopedType(candidate));
        Assert.True(filterer.IsInjectableTransientType(candidate), "Expected the last chained override to win");
        Assert.False(filterer.IsInjectableSingletonType(candidate));
        Assert.Equal(
            TypeFiltererLifetime.Transient,
            filterer.GetEffectiveLifetime(candidate, TypeFiltererLifetime.Singleton));
    }

    [Fact]
    public void ChainedLifetimeOverrides_ReversedOrder_LastOverrideWins()
    {
        var filterer = new ConfigurableTypeFilterer(false, false, false)
            .UsingOnlyAsTransient<IShapeTarget>()
            .UsingOnlyAsScoped<IShapeTarget>();

        var candidate = typeof(ShapeBase);

        Assert.True(filterer.IsInjectableScopedType(candidate), "Expected the last chained override to win");
        Assert.False(filterer.IsInjectableTransientType(candidate));
        Assert.False(filterer.IsInjectableSingletonType(candidate));
        Assert.Equal(
            TypeFiltererLifetime.Scoped,
            filterer.GetEffectiveLifetime(candidate, TypeFiltererLifetime.Singleton));
    }

    [Fact]
    public void ChainedLifetimeOverrides_NarrowerPredicateOverrideOnlyAffectsMatchingTypes()
    {
        var filterer = new ConfigurableTypeFilterer(false, false, false)
            .UsingOnlyAsSingleton<IShapeTarget>()
            .UsingOnlyAsTransient<IShapeTarget>(t => t == typeof(ShapeDerived));

        Assert.True(filterer.IsInjectableTransientType(typeof(ShapeDerived)), "Expected the narrower override to apply");
        Assert.False(filterer.IsInjectableSingletonType(typeof(ShapeDerived)));

        Assert.True(filterer.IsInjectableSingletonType(typeof(ShapeBase)), "Expected the wider override to remain for non-matching types");
        Assert.False(filterer.IsInjectableTransientType(typeof(ShapeBase)));
    }

    [Theory]
    [InlineData(TypeFiltererLifetime.Scoped)]
    [InlineData(TypeFiltererLifetime.Transient)]
    [InlineData(TypeFiltererLifetime.Singleton)]
    public void ExceptOfT_AfterLifetimeOverride_ExcludesFromAllLifetimes(TypeFiltererLifetime lifetime)
    {
        var filterer = OverrideWith(new ConfigurableTypeFilterer(false, false, false), lifetime)
            .Except<ExcludedShape>();

        AssertFullyExcluded(filterer, typeof(ExcludedShape));
        AssertOverriddenTo(filterer, typeof(IncludedShape), lifetime);
    }

    [Theory]
    [InlineData(TypeFiltererLifetime.Scoped)]
    [InlineData(TypeFiltererLifetime.Transient)]
    [InlineData(TypeFiltererLifetime.Singleton)]
    public void ExceptOfT_BeforeLifetimeOverride_ExcludesFromAllLifetimes(TypeFiltererLifetime lifetime)
    {
        var filterer = OverrideWith(
            new ConfigurableTypeFilterer(false, false, false).Except<ExcludedShape>(),
            lifetime);

        AssertFullyExcluded(filterer, typeof(ExcludedShape));
        AssertOverriddenTo(filterer, typeof(IncludedShape), lifetime);
    }

    [Theory]
    [InlineData(TypeFiltererLifetime.Scoped)]
    [InlineData(TypeFiltererLifetime.Transient)]
    [InlineData(TypeFiltererLifetime.Singleton)]
    public void ExceptPredicate_BeforeLifetimeOverride_ExcludesFromAllLifetimes(TypeFiltererLifetime lifetime)
    {
        var filterer = OverrideWith(
            new ConfigurableTypeFilterer(false, false, false).Except(t => t == typeof(ExcludedShape)),
            lifetime);

        AssertFullyExcluded(filterer, typeof(ExcludedShape));
        AssertOverriddenTo(filterer, typeof(IncludedShape), lifetime);
    }

    [Theory]
    [InlineData(TypeFiltererLifetime.Scoped)]
    [InlineData(TypeFiltererLifetime.Transient)]
    [InlineData(TypeFiltererLifetime.Singleton)]
    public void InnerFiltererExclusion_IsHonoredByLifetimeOverrides(TypeFiltererLifetime lifetime)
    {
        var inner = new ConfigurableTypeFilterer(
            isScoped: false,
            isTransient: false,
            isSingleton: false,
            exclusionPredicate: t => t == typeof(ExcludedShape));

        var filterer = OverrideWith(inner, lifetime);

        AssertFullyExcluded(filterer, typeof(ExcludedShape));
        AssertOverriddenTo(filterer, typeof(IncludedShape), lifetime);
    }

    [Fact]
    public void ExceptOfT_RemovesTypeAlreadyAllowedByInnerFilterer()
    {
        var filterer = new ConfigurableTypeFilterer(true, true, true)
            .Except<ExcludedShape>();

        AssertFullyExcluded(filterer, typeof(ExcludedShape));

        Assert.True(filterer.IsInjectableScopedType(typeof(IncludedShape)), "Expected non-excluded types to keep the inner scoped result");
        Assert.True(filterer.IsInjectableTransientType(typeof(IncludedShape)), "Expected non-excluded types to keep the inner transient result");
        Assert.True(filterer.IsInjectableSingletonType(typeof(IncludedShape)), "Expected non-excluded types to keep the inner singleton result");
        Assert.False(filterer.IsTypeExcluded(typeof(IncludedShape)));
    }

    [Fact]
    public void UsingOnlyAsScopedOfTWithPredicate_DoesNotEvaluatePredicateForUnassignableTypes()
    {
        var predicateInvocations = 0;
        var filterer = new ConfigurableTypeFilterer(false, false, false)
            .UsingOnlyAsScoped<IShapeTarget>(_ =>
            {
                predicateInvocations++;
                return true;
            });

        Assert.False(filterer.IsInjectableScopedType(typeof(UnrelatedShape)));
        Assert.Equal(0, predicateInvocations);

        Assert.True(filterer.IsInjectableScopedType(typeof(ShapeBase)), "Expected assignable types to match the scoped override");
        Assert.Equal(1, predicateInvocations);
    }

    [Fact]
    public void UsingOnlyAsScopedOfTWithPredicate_DoesNotEvaluatePredicateWhenInnerFiltererAlreadyMatches()
    {
        var predicateInvocations = 0;
        var filterer = new ConfigurableTypeFilterer(true, false, false)
            .UsingOnlyAsScoped<IShapeTarget>(_ =>
            {
                predicateInvocations++;
                return true;
            });

        Assert.True(filterer.IsInjectableScopedType(typeof(ShapeBase)), "Expected the inner filterer result to short-circuit the override");
        Assert.Equal(0, predicateInvocations);
    }

    [Fact]
    public void ExceptPredicate_IsNotEvaluatedWhenInnerFiltererRejectsType()
    {
        var predicateInvocations = 0;
        var filterer = new ConfigurableTypeFilterer(false, false, false)
            .Except(_ =>
            {
                predicateInvocations++;
                return true;
            });

        Assert.False(filterer.IsInjectableScopedType(typeof(ShapeBase)));
        Assert.Equal(0, predicateInvocations);
    }

    [Fact]
    public void NullFilterer_ThrowsArgumentNullExceptionForEveryOverload()
    {
        ITypeFilterer? nullFilterer = null;
        Predicate<Type> predicate = static _ => true;

        Assert.Throws<ArgumentNullException>(() => nullFilterer!.Except<ShapeBase>());
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.Except(predicate));
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsScoped<ShapeBase>());
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsScoped<ShapeBase>(predicate));
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsScoped(predicate));
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsTransient<ShapeBase>());
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsTransient<ShapeBase>(predicate));
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsTransient(predicate));
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsSingleton<ShapeBase>());
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsSingleton<ShapeBase>(predicate));
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsSingleton(predicate));
    }

    [Fact]
    public void NullPredicate_ThrowsArgumentNullExceptionForEveryOverload()
    {
        var filterer = EmptyTypeFilterer.Instance;
        Predicate<Type> nullPredicate = null!;

        Assert.Throws<ArgumentNullException>(() => filterer.Except(nullPredicate));
        Assert.Throws<ArgumentNullException>(() => filterer.UsingOnlyAsScoped<ShapeBase>(nullPredicate));
        Assert.Throws<ArgumentNullException>(() => filterer.UsingOnlyAsScoped(nullPredicate));
        Assert.Throws<ArgumentNullException>(() => filterer.UsingOnlyAsTransient<ShapeBase>(nullPredicate));
        Assert.Throws<ArgumentNullException>(() => filterer.UsingOnlyAsTransient(nullPredicate));
        Assert.Throws<ArgumentNullException>(() => filterer.UsingOnlyAsSingleton<ShapeBase>(nullPredicate));
        Assert.Throws<ArgumentNullException>(() => filterer.UsingOnlyAsSingleton(nullPredicate));
    }

    private static ITypeFilterer OverrideWith(
        ITypeFilterer typeFilterer,
        TypeFiltererLifetime lifetime) => lifetime switch
        {
            TypeFiltererLifetime.Scoped => typeFilterer.UsingOnlyAsScoped<IShapeTarget>(),
            TypeFiltererLifetime.Transient => typeFilterer.UsingOnlyAsTransient<IShapeTarget>(),
            TypeFiltererLifetime.Singleton => typeFilterer.UsingOnlyAsSingleton<IShapeTarget>(),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unexpected lifetime."),
        };

    private static void AssertFullyExcluded(
        ITypeFilterer filterer,
        Type candidate)
    {
        Assert.False(filterer.IsInjectableScopedType(candidate));
        Assert.False(filterer.IsInjectableTransientType(candidate));
        Assert.False(filterer.IsInjectableSingletonType(candidate));
        Assert.True(filterer.IsTypeExcluded(candidate), "Expected the excluded type to remain excluded");
        Assert.Equal(
            TypeFiltererLifetime.Singleton,
            filterer.GetEffectiveLifetime(candidate, TypeFiltererLifetime.Singleton));
    }

    private static void AssertOverriddenTo(
        ITypeFilterer filterer,
        Type candidate,
        TypeFiltererLifetime lifetime)
    {
        Assert.Equal(lifetime == TypeFiltererLifetime.Scoped, filterer.IsInjectableScopedType(candidate));
        Assert.Equal(lifetime == TypeFiltererLifetime.Transient, filterer.IsInjectableTransientType(candidate));
        Assert.Equal(lifetime == TypeFiltererLifetime.Singleton, filterer.IsInjectableSingletonType(candidate));
        Assert.False(filterer.IsTypeExcluded(candidate));
        Assert.Equal(
            lifetime,
            filterer.GetEffectiveLifetime(candidate, TypeFiltererLifetime.Singleton));
    }

    private interface IShapeTarget { }

    private class ShapeBase : IShapeTarget { }

    private sealed class ShapeDerived : ShapeBase { }

    private sealed class UnrelatedShape { }

    private sealed class OpenShape<T> : IShapeTarget { }

    private interface IGenericShape<T> { }

    private sealed class GenericShape<T> : IGenericShape<T> { }

    private sealed class ExcludedShape : IShapeTarget { }

    private sealed class IncludedShape : IShapeTarget { }

    private static class ShapeContainer
    {
        public sealed class NestedShape : IShapeTarget { }
    }
}
