using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.Reflection;

public sealed class AddDecoratorScopedServiceTests
{
    private readonly IServiceProvider _serviceProvider;

    public AddDecoratorScopedServiceTests()
    {
        _serviceProvider = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(services =>
            {
                // Register the base service as scoped
                services.AddScoped<ITestServiceForDecoration, TestServiceToBeDecorated>();
            })
            .AddDecorator<ITestServiceForDecoration, TestServiceDecorator>()
            .BuildServiceProvider();
    }

    [Fact]
    public void GetService_ScopedService_PreservesLifetime()
    {
        using var scope1 = _serviceProvider.CreateScope();
        using var scope2 = _serviceProvider.CreateScope();

        var instanceScope1a = scope1.ServiceProvider.GetService<ITestServiceForDecoration>();
        var instanceScope1b = scope1.ServiceProvider.GetService<ITestServiceForDecoration>();
        var instanceScope2 = scope2.ServiceProvider.GetService<ITestServiceForDecoration>();

        // Within the same scope, should be the same instance (scoped)
        Assert.Same(instanceScope1a, instanceScope1b);

        // Across different scopes, should be different instances
        Assert.NotSame(instanceScope1a, instanceScope2);
    }
}
