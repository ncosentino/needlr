using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.TypeFilterers;

public sealed class TypeFiltererExtensionMethodsTests
{
    [Fact]
    public void EmptyTypeFilterer_Instance_ReturnsSingleton()
    {
        var instance1 = EmptyTypeFilterer.Instance;
        var instance2 = EmptyTypeFilterer.Instance;
        
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void EmptyTypeFilterer_ReturnsfalseForAllTypes()
    {
        var filterer = EmptyTypeFilterer.Instance;
        
        Assert.False(filterer.IsInjectableScopedType(typeof(TestService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(TestService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(TestService)));
        
        Assert.False(filterer.IsInjectableScopedType(typeof(ITestService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(ITestService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(ITestService)));
    }

    [Fact]
    public void UsingOnlyAsScoped_WithEmptyFilterer_AllowsOnlySpecifiedTypeAsScoped()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsScoped<TestService>();
        
        Assert.True(filterer.IsInjectableScopedType(typeof(TestService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(TestService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(TestService)));
        
        Assert.False(filterer.IsInjectableScopedType(typeof(OtherService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(OtherService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(OtherService)));
    }

    [Fact]
    public void UsingOnlyAsTransient_WithEmptyFilterer_AllowsOnlySpecifiedTypeAsTransient()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsTransient<TestService>();
        
        Assert.False(filterer.IsInjectableScopedType(typeof(TestService)));
        Assert.True(filterer.IsInjectableTransientType(typeof(TestService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(TestService)));
        
        Assert.False(filterer.IsInjectableScopedType(typeof(OtherService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(OtherService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(OtherService)));
    }

    [Fact]
    public void UsingOnlyAsSingleton_WithEmptyFilterer_AllowsOnlySpecifiedTypeAsSingleton()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<TestService>();
        
        Assert.False(filterer.IsInjectableScopedType(typeof(TestService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(TestService)));
        Assert.True(filterer.IsInjectableSingletonType(typeof(TestService)));
        
        Assert.False(filterer.IsInjectableScopedType(typeof(OtherService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(OtherService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(OtherService)));
    }

    [Fact]
    public void Except_WithConfiguredFilterer_ExcludesSpecifiedType()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<ITestService>()
            .Except<ExcludedService>();
        
        Assert.True(filterer.IsInjectableSingletonType(typeof(TestService)));
        Assert.True(filterer.IsInjectableSingletonType(typeof(OtherService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(ExcludedService)));
    }

    [Fact]
    public void CompoundConditions_MultipleTypes_DifferentLifetimes()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsScoped<IScopedService>()
            .UsingOnlyAsTransient<ITransientService>()
            .UsingOnlyAsSingleton<ISingletonService>();
        
        var scopedService = typeof(ScopedServiceImpl);
        Assert.True(filterer.IsInjectableScopedType(scopedService));
        Assert.False(filterer.IsInjectableTransientType(scopedService));
        Assert.False(filterer.IsInjectableSingletonType(scopedService));
        
        var transientService = typeof(TransientServiceImpl);
        Assert.False(filterer.IsInjectableScopedType(transientService));
        Assert.True(filterer.IsInjectableTransientType(transientService));
        Assert.False(filterer.IsInjectableSingletonType(transientService));
        
        var singletonService = typeof(SingletonServiceImpl);
        Assert.False(filterer.IsInjectableScopedType(singletonService));
        Assert.False(filterer.IsInjectableTransientType(singletonService));
        Assert.True(filterer.IsInjectableSingletonType(singletonService));
        
        var unregisteredService = typeof(UnregisteredService);
        Assert.False(filterer.IsInjectableScopedType(unregisteredService));
        Assert.False(filterer.IsInjectableTransientType(unregisteredService));
        Assert.False(filterer.IsInjectableSingletonType(unregisteredService));
    }

    [Fact]
    public void CompoundConditions_WithExcept_ExcludesFromAllLifetimes()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<IService>()
            .Except<ExcludedService>();
        
        var includedService = typeof(IncludedService);
        Assert.False(filterer.IsInjectableScopedType(includedService));
        Assert.False(filterer.IsInjectableTransientType(includedService));
        Assert.True(filterer.IsInjectableSingletonType(includedService));
        
        var excludedService = typeof(ExcludedService);
        Assert.False(filterer.IsInjectableScopedType(excludedService));
        Assert.False(filterer.IsInjectableTransientType(excludedService));
        Assert.False(filterer.IsInjectableSingletonType(excludedService));
    }

    [Fact]
    public void ChainedConfiguration_InterfaceHierarchy_WorksCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<IBaseService>()
            .UsingOnlyAsScoped<IDerivedService>()
            .Except<IExcludedDerivedService>();
        
        var baseService = typeof(BaseServiceImpl);
        Assert.False(filterer.IsInjectableScopedType(baseService));
        Assert.False(filterer.IsInjectableTransientType(baseService));
        Assert.True(filterer.IsInjectableSingletonType(baseService));
        
        var derivedService = typeof(DerivedServiceImpl);
        Assert.True(filterer.IsInjectableScopedType(derivedService));
        Assert.False(filterer.IsInjectableTransientType(derivedService));
        Assert.False(filterer.IsInjectableSingletonType(derivedService));
        
        var excludedDerivedService = typeof(ExcludedDerivedServiceImpl);
        Assert.False(filterer.IsInjectableScopedType(excludedDerivedService));
        Assert.False(filterer.IsInjectableTransientType(excludedDerivedService));
        Assert.False(filterer.IsInjectableSingletonType(excludedDerivedService));
    }

    [Fact]
    public void ComplexScenario_MixedConfiguration_ProducesExpectedResults()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<IRepository>()
            .UsingOnlyAsScoped<IService>()
            .UsingOnlyAsTransient<IHandler>()
            .Except<ILegacyService>()
            .Except<IDeprecatedRepository>();
        
        Assert.True(filterer.IsInjectableSingletonType(typeof(UserRepository)));
        Assert.False(filterer.IsInjectableScopedType(typeof(UserRepository)));
        Assert.False(filterer.IsInjectableTransientType(typeof(UserRepository)));
        
        Assert.True(filterer.IsInjectableScopedType(typeof(UserService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(UserService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(UserService)));
        
        Assert.True(filterer.IsInjectableTransientType(typeof(CommandHandler)));
        Assert.False(filterer.IsInjectableScopedType(typeof(CommandHandler)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(CommandHandler)));
        
        Assert.False(filterer.IsInjectableScopedType(typeof(LegacyService)));
        Assert.False(filterer.IsInjectableTransientType(typeof(LegacyService)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(LegacyService)));
        
        Assert.False(filterer.IsInjectableScopedType(typeof(DeprecatedRepository)));
        Assert.False(filterer.IsInjectableTransientType(typeof(DeprecatedRepository)));
        Assert.False(filterer.IsInjectableSingletonType(typeof(DeprecatedRepository)));
    }

    [Fact]
    public void NullFilterer_ThrowsArgumentNullException()
    {
        ITypeFilterer? nullFilterer = null;
        
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.Except<TestService>());
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsScoped<TestService>());
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsTransient<TestService>());
        Assert.Throws<ArgumentNullException>(() => nullFilterer!.UsingOnlyAsSingleton<TestService>());
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(Guid))]
    public void PrimitiveTypes_CanBeConfigured(Type primitiveType)
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<object>();
        
        Assert.True(filterer.IsInjectableSingletonType(primitiveType));
        Assert.False(filterer.IsInjectableScopedType(primitiveType));
        Assert.False(filterer.IsInjectableTransientType(primitiveType));
    }

    [Fact]
    public void GenericTypes_WorkCorrectly()
    {
        var filterer = EmptyTypeFilterer.Instance
            .UsingOnlyAsSingleton<IGenericService<string>>()
            .UsingOnlyAsScoped<IGenericService<int>>();
        
        var stringService = typeof(GenericServiceImpl<string>);
        Assert.True(filterer.IsInjectableSingletonType(stringService));
        Assert.False(filterer.IsInjectableScopedType(stringService));
        
        var intService = typeof(GenericServiceImpl<int>);
        Assert.True(filterer.IsInjectableScopedType(intService));
        Assert.False(filterer.IsInjectableSingletonType(intService));
        
        var doubleService = typeof(GenericServiceImpl<double>);
        Assert.False(filterer.IsInjectableScopedType(doubleService));
        Assert.False(filterer.IsInjectableSingletonType(doubleService));
        Assert.False(filterer.IsInjectableTransientType(doubleService));
    }

    private interface ITestService { }
    private class TestService : ITestService { }
    private class OtherService : ITestService { }
    private class ExcludedService : ITestService { }

    private interface IScopedService { }
    private class ScopedServiceImpl : IScopedService { }

    private interface ITransientService { }
    private class TransientServiceImpl : ITransientService { }

    private interface ISingletonService { }
    private class SingletonServiceImpl : ISingletonService { }

    private class UnregisteredService { }

    private interface IService { }
    private class IncludedService : IService { }

    private interface IBaseService { }
    private interface IDerivedService : IBaseService { }
    private interface IExcludedDerivedService : IDerivedService { }
    
    private class BaseServiceImpl : IBaseService { }
    private class DerivedServiceImpl : IDerivedService { }
    private class ExcludedDerivedServiceImpl : IExcludedDerivedService { }

    private interface IRepository { }
    private interface IDeprecatedRepository : IRepository { }
    private class UserRepository : IRepository { }
    private class DeprecatedRepository : IDeprecatedRepository { }

    private interface ILegacyService : IService { }
    private class UserService : IService { }
    private class LegacyService : ILegacyService { }

    private interface IHandler { }
    private class CommandHandler : IHandler { }

    private interface IGenericService<T> { }
    private class GenericServiceImpl<T> : IGenericService<T> { }
}