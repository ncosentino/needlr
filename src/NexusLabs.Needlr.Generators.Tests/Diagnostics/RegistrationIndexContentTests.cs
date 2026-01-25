using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class RegistrationIndexContentTests
{
    [Fact]
    public void RegistrationIndex_ContainsHeader()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("# Needlr Registration Index", content);
    }

    [Fact]
    public void RegistrationIndex_ShowsServicesTable()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("Interface", content);
        Assert.Contains("Implementation", content);
        Assert.Contains("Lifetime", content);
        Assert.Contains("IOrderService", content);
        Assert.Contains("OrderService", content);
        Assert.Contains("Singleton", content);
    }

    [Fact]
    public void RegistrationIndex_ShowsSourceColumn()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("Source", content);
    }

    [Fact]
    public void RegistrationIndex_ShowsDecorators()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IService { }
    
    public class BaseService : IService { }
    
    [DecoratorFor<IService>(Order = 1)]
    public class LoggingDecorator : IService
    {
        private readonly IService _inner;
        public LoggingDecorator(IService inner) => _inner = inner;
    }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("Decorator", content);
        Assert.Contains("LoggingDecorator", content);
    }

    [Fact]
    public void RegistrationIndex_ShowsDecoratorChainOrder()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IService { }
    
    public class BaseService : IService { }
    
    [DecoratorFor<IService>(Order = 1)]
    public class FirstDecorator : IService
    {
        private readonly IService _inner;
        public FirstDecorator(IService inner) => _inner = inner;
    }
    
    [DecoratorFor<IService>(Order = 2)]
    public class SecondDecorator : IService
    {
        private readonly IService _inner;
        public SecondDecorator(IService inner) => _inner = inner;
    }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("FirstDecorator", content);
        Assert.Contains("SecondDecorator", content);
        Assert.Contains("Decorator", content);
    }

    [Fact]
    public void RegistrationIndex_ShowsAssemblyName()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("Assembly:", content);
    }

    [Fact]
    public void RegistrationIndex_ShowsGeneratedTimestamp()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("Generated:", content);
    }

    [Fact]
    public void RegistrationIndex_ServiceCount_IsAccurate()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" }, IncludeSelf = true)]

namespace TestApp
{
    public interface IService1 { }
    public class Service1 : IService1 { }
    
    public interface IService2 { }
    public class Service2 : IService2 { }
    
    public interface IService3 { }
    public class Service3 : IService3 { }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("## Services (3)", content);
    }

    [Fact]
    public void RegistrationIndex_DecoratorSection_OnlyAppearsWhenDecoratorsExist()
    {
        var sourceWithoutDecorators = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IService { }
    public class Service : IService { }
}";

        var content = GetDiagnosticContent(sourceWithoutDecorators, "RegistrationIndex");

        Assert.DoesNotContain("## Decorators", content);
    }

    [Fact]
    public void RegistrationIndex_DecoratorChain_ShowsCorrectOrder()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" }, IncludeSelf = true)]

namespace TestApp
{
    public interface IService { }
    
    public class BaseService : IService { }
    
    [DecoratorFor<IService>(Order = 1)]
    public class FirstDecorator : IService
    {
        private readonly IService _inner;
        public FirstDecorator(IService inner) => _inner = inner;
    }
    
    [DecoratorFor<IService>(Order = 2)]
    public class SecondDecorator : IService
    {
        private readonly IService _inner;
        public SecondDecorator(IService inner) => _inner = inner;
    }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("## Decorators", content);
        Assert.Contains("FirstDecorator", content);
        Assert.Contains("SecondDecorator", content);
        
        var firstIndex = content.IndexOf("FirstDecorator");
        var secondIndex = content.IndexOf("SecondDecorator");
        
        Assert.True(firstIndex > 0);
        Assert.True(secondIndex > 0);
    }
}
