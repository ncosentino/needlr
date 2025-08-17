using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

public sealed class AddDecoratorTransientServiceTests
{
    private readonly IServiceProvider _serviceProvider;

    public AddDecoratorTransientServiceTests()
    {
        _serviceProvider = new Syringe()
            .UsingPostPluginRegistrationCallback(services =>
            {
                // Register the base service as transient
                services.AddTransient<ITestServiceForDecoration, TestServiceToBeDecorated>();
            })
            .AddDecorator<ITestServiceForDecoration, TestServiceDecorator>()
            .BuildServiceProvider();
    }

    [Fact]
    public void GetService_TransientService_PreservesLifetime()
    {
        var instance1 = _serviceProvider.GetService<ITestServiceForDecoration>();
        var instance2 = _serviceProvider.GetService<ITestServiceForDecoration>();

        // Transient services should always be different instances
        Assert.NotSame(instance1, instance2);

        // But both should still be decorated
        Assert.IsType<TestServiceDecorator>(instance1);
        Assert.IsType<TestServiceDecorator>(instance2);
    }
}
