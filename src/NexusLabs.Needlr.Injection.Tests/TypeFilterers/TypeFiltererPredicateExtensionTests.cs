using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

public sealed class TypeFiltererPredicateExtensionTests
{
    [Fact]
    public void UsingOnlyAsTransient_WithPredicate_FiltersCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsTransient(t =>
                t.IsAssignableTo(typeof(IJob)) &&
                t.IsClass &&
                !t.IsAbstract);

        var concreteJob = typeof(ConcreteJob);
        var abstractJob = typeof(AbstractJob);
        var jobInterface = typeof(IJob);
        var unrelatedClass = typeof(UnrelatedClass);

        Assert.False(filterer.IsInjectableScopedType(concreteJob));
        Assert.True(filterer.IsInjectableTransientType(concreteJob));
        Assert.False(filterer.IsInjectableSingletonType(concreteJob));

        Assert.False(filterer.IsInjectableTransientType(abstractJob));
        Assert.False(filterer.IsInjectableTransientType(jobInterface));
        Assert.False(filterer.IsInjectableTransientType(unrelatedClass));
    }

    [Fact]
    public void UsingOnlyAsTransient_WithTypeAndPredicate_FiltersCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsTransient<IJob>(t => t.IsClass && !t.IsAbstract);

        var concreteJob = typeof(ConcreteJob);
        var abstractJob = typeof(AbstractJob);
        var jobInterface = typeof(IJob);
        var unrelatedClass = typeof(UnrelatedClass);

        Assert.False(filterer.IsInjectableScopedType(concreteJob));
        Assert.True(filterer.IsInjectableTransientType(concreteJob));
        Assert.False(filterer.IsInjectableSingletonType(concreteJob));

        Assert.False(filterer.IsInjectableTransientType(abstractJob));
        Assert.False(filterer.IsInjectableTransientType(jobInterface));
        Assert.False(filterer.IsInjectableTransientType(unrelatedClass));
    }

    [Fact]
    public void UsingOnlyAsScoped_WithPredicate_FiltersCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsScoped(t =>
                t.IsAssignableTo(typeof(IRepository)) &&
                t.Name.EndsWith("Repository") &&
                !t.IsAbstract);

        var userRepository = typeof(UserRepository);
        var productRepo = typeof(ProductRepo);
        var baseRepository = typeof(BaseRepository);

        Assert.True(filterer.IsInjectableScopedType(userRepository));
        Assert.False(filterer.IsInjectableTransientType(userRepository));
        Assert.False(filterer.IsInjectableSingletonType(userRepository));

        Assert.False(filterer.IsInjectableScopedType(productRepo));
        Assert.False(filterer.IsInjectableScopedType(baseRepository));
    }

    [Fact]
    public void UsingOnlyAsScoped_WithTypeAndPredicate_FiltersCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsScoped<IRepository>(t => !t.IsAbstract);

        var userRepository = typeof(UserRepository);
        var baseRepository = typeof(BaseRepository);
        var unrelatedClass = typeof(UnrelatedClass);

        Assert.True(filterer.IsInjectableScopedType(userRepository));
        Assert.False(filterer.IsInjectableTransientType(userRepository));
        Assert.False(filterer.IsInjectableSingletonType(userRepository));

        Assert.False(filterer.IsInjectableScopedType(baseRepository));
        Assert.False(filterer.IsInjectableScopedType(unrelatedClass));
    }

    [Fact]
    public void UsingOnlyAsSingleton_WithPredicate_FiltersCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton(t =>
                t.IsAssignableTo(typeof(IService)) &&
                t.IsSealed);

        var sealedService = typeof(SealedService);
        var regularService = typeof(RegularService);
        var unrelatedClass = typeof(UnrelatedClass);

        Assert.False(filterer.IsInjectableScopedType(sealedService));
        Assert.False(filterer.IsInjectableTransientType(sealedService));
        Assert.True(filterer.IsInjectableSingletonType(sealedService));

        Assert.False(filterer.IsInjectableSingletonType(regularService));
        Assert.False(filterer.IsInjectableSingletonType(unrelatedClass));
    }

    [Fact]
    public void UsingOnlyAsSingleton_WithTypeAndPredicate_FiltersCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<IService>(t => t.Name.StartsWith("Cached"));

        var cachedService = typeof(CachedService);
        var regularService = typeof(RegularService);
        var cachedNonService = typeof(CachedNonService);

        Assert.False(filterer.IsInjectableScopedType(cachedService));
        Assert.False(filterer.IsInjectableTransientType(cachedService));
        Assert.True(filterer.IsInjectableSingletonType(cachedService));

        Assert.False(filterer.IsInjectableSingletonType(regularService));
        Assert.False(filterer.IsInjectableSingletonType(cachedNonService));
    }

    [Fact]
    public void Except_WithPredicate_ExcludesMatchingTypes()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<IService>()
            .Except(t => t.Name.Contains("Legacy"));

        var legacyService = typeof(LegacyService);
        var regularService = typeof(RegularService);
        var legacyNonService = typeof(LegacyNonService);

        Assert.False(filterer.IsInjectableSingletonType(legacyService));
        Assert.True(filterer.IsInjectableSingletonType(regularService));
        Assert.False(filterer.IsInjectableSingletonType(legacyNonService));
    }

    [Fact]
    public void ComplexChaining_WithPredicates_WorksCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsTransient(t =>
                t.IsAssignableTo(typeof(IJob)) &&
                t.IsClass &&
                !t.IsAbstract)
            .UsingOnlyAsScoped<IRepository>(t =>
                !t.IsAbstract &&
                t.Name.EndsWith("Repository"))
            .UsingOnlyAsSingleton(t =>
                t.IsAssignableTo(typeof(IService)) &&
                t.IsSealed)
            .Except<ILegacyMarker>()
            .Except(t => t.Name.StartsWith("Deprecated"));

        var concreteJob = typeof(ConcreteJob);
        var userRepository = typeof(UserRepository);
        var sealedService = typeof(SealedService);
        var legacyService = typeof(LegacyService);
        var deprecatedJob = typeof(DeprecatedJob);

        Assert.True(filterer.IsInjectableTransientType(concreteJob));
        Assert.False(filterer.IsInjectableScopedType(concreteJob));

        Assert.True(filterer.IsInjectableScopedType(userRepository));
        Assert.False(filterer.IsInjectableTransientType(userRepository));

        Assert.True(filterer.IsInjectableSingletonType(sealedService));
        Assert.False(filterer.IsInjectableScopedType(sealedService));

        Assert.False(filterer.IsInjectableSingletonType(legacyService));
        Assert.False(filterer.IsInjectableTransientType(deprecatedJob));
    }

    [Fact]
    public void EmptyFilterer_WithPredicates_StartsFromEmptyState()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsTransient(t =>
                t.IsAssignableTo(typeof(IJob)) &&
                t.IsClass &&
                !t.IsAbstract);

        var concreteJob = typeof(ConcreteJob);
        var abstractJob = typeof(AbstractJob);

        Assert.True(filterer.IsInjectableTransientType(concreteJob));
        Assert.False(filterer.IsInjectableTransientType(abstractJob));
        Assert.False(filterer.IsInjectableScopedType(concreteJob));
        Assert.False(filterer.IsInjectableSingletonType(concreteJob));
    }

    [Fact]
    public void ComplexPredicates_WithMultipleConditions_WorkCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsTransient(t =>
                t.IsAssignableTo(typeof(IHandler)) &&
                t.IsClass &&
                !t.IsAbstract &&
                t.GetConstructors().Any(c => c.IsPublic));

        var validHandler = typeof(ValidHandler);
        var nestedHandler = typeof(ContainerClass.NestedHandler);
        var privateConstructorHandler = typeof(PrivateConstructorHandler);

        Assert.True(filterer.IsInjectableTransientType(validHandler));
        Assert.True(filterer.IsInjectableTransientType(nestedHandler));
        Assert.False(filterer.IsInjectableTransientType(privateConstructorHandler));
    }

    [Fact]
    public void PredicateWithGenericTypes_WorksCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton(t =>
                t.IsGenericType &&
                t.GetGenericTypeDefinition() == typeof(IGenericService<>));

        var stringGenericService = typeof(GenericServiceImpl<string>);
        var intGenericService = typeof(GenericServiceImpl<int>);
        var nonGenericService = typeof(RegularService);

        Assert.False(filterer.IsInjectableSingletonType(stringGenericService));
        Assert.False(filterer.IsInjectableSingletonType(intGenericService));
        Assert.False(filterer.IsInjectableSingletonType(nonGenericService));

        var stringInterface = typeof(IGenericService<string>);
        var intInterface = typeof(IGenericService<int>);
        Assert.True(filterer.IsInjectableSingletonType(stringInterface));
        Assert.True(filterer.IsInjectableSingletonType(intInterface));
    }

    private interface IJob { }
    private class ConcreteJob : IJob { }
    private abstract class AbstractJob : IJob { }
    private class DeprecatedJob : IJob { }

    private interface IRepository { }
    private class UserRepository : IRepository { }
    private class ProductRepo : IRepository { }
    private abstract class BaseRepository : IRepository { }

    private interface IService { }
    private sealed class SealedService : IService { }
    private class RegularService : IService { }
    private sealed class CachedService : IService { }
    private class LegacyService : IService, ILegacyMarker { }

    private interface ILegacyMarker { }
    private class LegacyNonService : ILegacyMarker { }

    private class UnrelatedClass { }
    private class CachedNonService { }

    private interface IHandler { }
    private class ValidHandler : IHandler
    {
        public ValidHandler() { }
    }

    private class PrivateConstructorHandler : IHandler
    {
        private PrivateConstructorHandler() { }
    }

    private static class ContainerClass
    {
        public class NestedHandler : IHandler { }
    }

    private interface IGenericService<T> { }
    private class GenericServiceImpl<T> : IGenericService<T> { }
}