using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DiagnosticFilterTests
{
    [Fact]
    public void Filter_SingleType_ExcludesOtherTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
    
    public interface IShippingService { }
    public class ShippingService : IShippingService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", "TestApp.OrderService");

        Assert.Contains("OrderService", content);
        Assert.Contains("## Services (1)", content);
        Assert.DoesNotContain("PaymentService", content);
        Assert.DoesNotContain("ShippingService", content);
    }

    [Fact]
    public void Filter_MultipleTypes_ExcludesUnmatched()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
    
    public interface IShippingService { }
    public class ShippingService : IShippingService { }
    
    public interface INotificationService { }
    public class NotificationService : INotificationService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", 
            "TestApp.OrderService,TestApp.PaymentService");

        Assert.Contains("OrderService", content);
        Assert.Contains("PaymentService", content);
        Assert.DoesNotContain("ShippingService", content);
        Assert.DoesNotContain("NotificationService", content);
    }

    [Fact]
    public void Filter_Empty_IncludesAllTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
    
    public interface IShippingService { }
    public class ShippingService : IShippingService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", "");

        Assert.Contains("OrderService", content);
        Assert.Contains("PaymentService", content);
        Assert.Contains("ShippingService", content);
    }

    [Fact]
    public void Filter_Null_IncludesAllTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", null);

        Assert.Contains("OrderService", content);
        Assert.Contains("PaymentService", content);
    }

    [Fact]
    public void Filter_AppliesToAllDiagnosticOutputs()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
}";

        var registrationContent = GetDiagnosticContentWithFilter(source, "RegistrationIndex", "TestApp.OrderService");
        var graphContent = GetDiagnosticContentWithFilter(source, "DependencyGraph", "TestApp.OrderService");
        var summaryContent = GetDiagnosticContentWithFilter(source, "LifetimeSummary", "TestApp.OrderService");

        Assert.Contains("OrderService", registrationContent);
        Assert.DoesNotContain("PaymentService", registrationContent);
        
        Assert.Contains("OrderService", graphContent);
        Assert.DoesNotContain("PaymentService", graphContent);
        
        Assert.Contains("1", summaryContent);
    }

    [Fact]
    public void Filter_MatchesShortTypeName()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", "OrderService");

        Assert.Contains("OrderService", content);
        Assert.DoesNotContain("PaymentService", content);
    }

    [Fact]
    public void Filter_MatchesNamespaceQualifiedName()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", "TestApp.OrderService");

        Assert.Contains("OrderService", content);
        Assert.DoesNotContain("PaymentService", content);
    }

    [Fact]
    public void Filter_TrimsSeparatedValues()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
    
    public interface IShippingService { }
    public class ShippingService : IShippingService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", 
            "  TestApp.OrderService  ,  TestApp.PaymentService  ");

        Assert.Contains("OrderService", content);
        Assert.Contains("PaymentService", content);
        Assert.DoesNotContain("ShippingService", content);
    }

    [Fact]
    public void Filter_SemicolonSeparator()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
    
    public interface IShippingService { }
    public class ShippingService : IShippingService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", 
            "TestApp.OrderService;TestApp.PaymentService");

        Assert.Contains("OrderService", content);
        Assert.Contains("PaymentService", content);
        Assert.DoesNotContain("ShippingService", content);
    }

    [Fact]
    public void Filter_IgnoresEmptyParts()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
    
    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
}";

        var content = GetDiagnosticContentWithFilter(source, "RegistrationIndex", 
            "TestApp.OrderService,,TestApp.PaymentService,");

        Assert.Contains("OrderService", content);
        Assert.Contains("PaymentService", content);
    }
}
