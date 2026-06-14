using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests that the source generator excludes .NET MAUI per-platform application entry points
/// from the type registry when scanning a MAUI head project.
///
/// Context: a MAUI head compiles per-platform entry types under <c>Platforms/</c> — the Windows
/// <c>App : Microsoft.Maui.MauiWinUIApplication</c>, the Android
/// <c>MainApplication : Microsoft.Maui.MauiApplication</c>, and the iOS/Mac
/// <c>AppDelegate : Microsoft.Maui.MauiUIApplicationDelegate</c>. These are framework-owned and
/// are decorated by the platform's own source generators (WinRT/CsWinRT, Android, etc.) with
/// interop members that are inaccessible from generated code. Registering them makes the head
/// build fail (CS0122 / CS0101). They must be skipped while the cross-platform App, pages,
/// views, and view models remain scannable.
///
/// These tests use stub base types (so they run without the MAUI workload) and verify the
/// platform entry types are excluded while ordinary types are still registered.
/// </summary>
public sealed class MauiPlatformEntryExclusionTests
{
    private const string MauiFrameworkSource = @"
namespace Microsoft.Maui
{
    public class MauiWinUIApplication { }
    public class MauiApplication { }
    public class MauiUIApplicationDelegate { }
}";

    [Fact]
    public void Generator_WindowsPlatformApp_IsExcludedFromRegistry()
    {
        var consumerSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyApp"" })]

namespace MyApp.WinUI
{
    public partial class App : global::Microsoft.Maui.MauiWinUIApplication { }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithAssemblyName("MyApp")
            .WithCrossAssemblySource("MauiFramework", MauiFrameworkSource)
            .WithSource(consumerSource)
            .RunTypeRegistryGenerator();

        Assert.DoesNotContain("MyApp.WinUI.App", output);
    }

    [Fact]
    public void Generator_AndroidPlatformApp_IsExcludedFromRegistry()
    {
        var consumerSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyApp"" })]

namespace MyApp
{
    public class MainApplication : global::Microsoft.Maui.MauiApplication { }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithAssemblyName("MyApp")
            .WithCrossAssemblySource("MauiFramework", MauiFrameworkSource)
            .WithSource(consumerSource)
            .RunTypeRegistryGenerator();

        Assert.DoesNotContain("MyApp.MainApplication", output);
    }

    [Fact]
    public void Generator_ApplePlatformDelegate_IsExcludedFromRegistry()
    {
        var consumerSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyApp"" })]

namespace MyApp
{
    public class AppDelegate : global::Microsoft.Maui.MauiUIApplicationDelegate { }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithAssemblyName("MyApp")
            .WithCrossAssemblySource("MauiFramework", MauiFrameworkSource)
            .WithSource(consumerSource)
            .RunTypeRegistryGenerator();

        Assert.DoesNotContain("MyApp.AppDelegate", output);
    }

    [Fact]
    public void Generator_PlatformEntryTypes_ExcludedButOrdinaryTypes_StillRegistered()
    {
        // The whole point: only the framework platform entry points are skipped. Ordinary
        // services and view models that happen to live in the head project must still register.
        var consumerSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyApp"" })]

namespace MyApp.WinUI
{
    public partial class App : global::Microsoft.Maui.MauiWinUIApplication { }
}

namespace MyApp
{
    public class MainViewModel { }
    public sealed class GreetingService { }
}";

        var output = GeneratorTestRunner.ForTypeRegistry()
            .WithAssemblyName("MyApp")
            .WithCrossAssemblySource("MauiFramework", MauiFrameworkSource)
            .WithSource(consumerSource)
            .RunTypeRegistryGenerator();

        Assert.DoesNotContain("MyApp.WinUI.App", output);
        Assert.Contains("typeof(global::MyApp.MainViewModel)", output);
        Assert.Contains("typeof(global::MyApp.GreetingService)", output);
    }
}
