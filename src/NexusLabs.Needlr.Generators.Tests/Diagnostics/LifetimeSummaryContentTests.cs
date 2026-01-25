using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class LifetimeSummaryContentTests
{
    [Fact]
    public void LifetimeSummary_ContainsHeader()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "LifetimeSummary");

        Assert.Contains("# Needlr Lifetime Summary", content);
    }

    [Fact]
    public void LifetimeSummary_ShowsLifetimeCategories()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" }, IncludeSelf = true)]

namespace TestApp
{
    public interface ISingleton1 { }
    public class Singleton1 : ISingleton1 { }

    public interface ISingleton2 { }
    public class Singleton2 : ISingleton2 { }
}";

        var content = GetDiagnosticContent(source, "LifetimeSummary");

        Assert.Contains("Singleton", content);
        Assert.Contains("Count", content);
    }

    [Fact]
    public void LifetimeSummary_ShowsPercentages()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "LifetimeSummary");

        Assert.Contains("%", content);
    }

    [Fact]
    public void LifetimeSummary_ShowsTotalCount()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ISingleton1 { }
    public class Singleton1 : ISingleton1 { }
    
    public interface IScoped1 { }
    public class Scoped1 : IScoped1 { }
}";

        var content = GetDiagnosticContent(source, "LifetimeSummary");

        Assert.Contains("Total", content);
    }

    [Fact]
    public void LifetimeSummary_Counts_MatchActualRegistrations()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" }, IncludeSelf = true)]

namespace TestApp
{
    public interface ISingleton1 { }
    public class Singleton1 : ISingleton1 { }

    public interface ISingleton2 { }
    public class Singleton2 : ISingleton2 { }
    
    public interface IScoped1 { }
    public class Scoped1 : IScoped1 { }
    
    public interface ITransient1 { }
    public class Transient1 : ITransient1 { }
    
    public interface ITransient2 { }
    public class Transient2 : ITransient2 { }
    
    public interface ITransient3 { }
    public class Transient3 : ITransient3 { }
}";

        var content = GetDiagnosticContent(source, "LifetimeSummary");

        Assert.Contains("| Singleton |", content);
        Assert.Contains("| **Total** | **6** | 100% |", content);
        Assert.Matches(@"\| Singleton \| 6 \|", content);
    }

    [Fact]
    public void LifetimeSummary_Percentages_AreAccurate()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" }, IncludeSelf = true)]

namespace TestApp
{
    public interface ISingleton1 { }
    public class Singleton1 : ISingleton1 { }

    public interface ISingleton2 { }
    public class Singleton2 : ISingleton2 { }
    
    public interface IScoped1 { }
    public class Scoped1 : IScoped1 { }

    public interface ITransient1 { }
    public class Transient1 : ITransient1 { }
}";

        var content = GetDiagnosticContent(source, "LifetimeSummary");

        Assert.Contains("100%", content);
    }
}
