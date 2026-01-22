using System.Reflection;

using Carter;

using Microsoft.AspNetCore.SignalR;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Carter;
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Tests that verify plugins from Needlr extension packages (Carter, SignalR) are 
/// discoverable via source generation. This is critical because:
/// 1. Each extension package contains plugin classes (e.g., CarterWebApplicationBuilderPlugin)
/// 2. These plugins must be registered with the DI container for the features to work
/// 3. Source generation must discover these plugins from referenced assemblies
/// 
/// The key insight is that extension packages MUST have their own [GenerateTypeRegistry]
/// attribute with their own module initializer. Consumer projects should NOT need to
/// include the extension package's namespace in their own [GenerateTypeRegistry].
/// 
/// Without proper [GenerateTypeRegistry] in these packages, the plugins won't be 
/// discovered and features like Carter routing or SignalR hub registration will fail silently.
/// </summary>
public sealed class ExtensionPackagePluginDiscoveryTests
{
    private static Func<IReadOnlyList<PluginTypeInfo>> GetPluginTypesProvider()
    {
        if (!NeedlrSourceGenBootstrap.TryGetProviders(out _, out var pluginProvider))
        {
            throw new InvalidOperationException("NeedlrSourceGenBootstrap has no registered providers");
        }
        return pluginProvider;
    }

    /// <summary>
    /// Verifies that the Carter package has its own [GenerateTypeRegistry] and module initializer.
    /// This is the critical requirement - extension packages must self-register.
    /// 
    /// When a consumer references NexusLabs.Needlr.Carter, the Carter assembly's module
    /// initializer should register CarterWebApplicationBuilderPlugin with NeedlrSourceGenBootstrap.
    /// The consumer should NOT need to add "NexusLabs.Needlr.Carter" to their own namespace prefixes.
    /// </summary>
    [Fact]
    public void Carter_PackageHasOwnTypeRegistry()
    {
        // Arrange
        var carterAssembly = typeof(CarterWebApplicationBuilderPlugin).Assembly;

        // Act - Check if the Carter assembly has a TypeRegistry class in Generated namespace
        var typeRegistryType = carterAssembly.GetType("NexusLabs.Needlr.Carter.Generated.TypeRegistry");

        // Assert - Carter package should have its own generated TypeRegistry
        Assert.NotNull(typeRegistryType);

        // The TypeRegistry should have GetPluginTypes method
        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);
    }

    /// <summary>
    /// Verifies that the Carter package has a module initializer that self-registers.
    /// </summary>
    [Fact]
    public void Carter_PackageHasModuleInitializer()
    {
        // Arrange
        var carterAssembly = typeof(CarterWebApplicationBuilderPlugin).Assembly;

        // Act - Check if the Carter assembly has a module initializer
        var moduleInitializerType = carterAssembly.GetType("NexusLabs.Needlr.Carter.Generated.NeedlrSourceGenModuleInitializer");

        // Assert - Carter package should have its own module initializer
        Assert.NotNull(moduleInitializerType);
    }

    /// <summary>
    /// Verifies that the SignalR package has its own [GenerateTypeRegistry] and module initializer.
    /// </summary>
    [Fact]
    public void SignalR_PackageHasOwnTypeRegistry()
    {
        // Arrange
        var signalRAssembly = typeof(SignalR.SignalRWebApplicationBuilderPlugin).Assembly;

        // Act - Check if the SignalR assembly has a TypeRegistry class
        var typeRegistryType = signalRAssembly.GetType("NexusLabs.Needlr.SignalR.Generated.TypeRegistry");

        // Assert - SignalR package should have its own generated TypeRegistry
        Assert.NotNull(typeRegistryType);

        // The TypeRegistry should have GetPluginTypes method
        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);
    }

    /// <summary>
    /// Verifies that the SignalR package has a module initializer that self-registers.
    /// </summary>
    [Fact]
    public void SignalR_PackageHasModuleInitializer()
    {
        // Arrange
        var signalRAssembly = typeof(SignalR.SignalRWebApplicationBuilderPlugin).Assembly;

        // Act - Check if the SignalR assembly has a module initializer
        var moduleInitializerType = signalRAssembly.GetType("NexusLabs.Needlr.SignalR.Generated.NeedlrSourceGenModuleInitializer");

        // Assert - SignalR package should have its own module initializer
        Assert.NotNull(moduleInitializerType);
    }

    /// <summary>
    /// Verifies that CarterWebApplicationBuilderPlugin is registered via the Carter package's
    /// own TypeRegistry, not via the consumer's TypeRegistry.
    /// </summary>
    [Fact]
    public void Carter_PluginsRegisteredViaOwnTypeRegistry()
    {
        // Arrange
        var carterAssembly = typeof(CarterWebApplicationBuilderPlugin).Assembly;
        var typeRegistryType = carterAssembly.GetType("NexusLabs.Needlr.Carter.Generated.TypeRegistry");
        
        // Skip if TypeRegistry doesn't exist (expected failure case)
        if (typeRegistryType == null)
        {
            Assert.Fail("Carter package does not have its own TypeRegistry - this is the bug!");
            return;
        }

        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);

        // Act - Get plugins from Carter's own TypeRegistry
        var pluginTypes = (IReadOnlyList<PluginTypeInfo>)getPluginTypesMethod.Invoke(null, null)!;

        // Assert - Carter's TypeRegistry should contain its own plugins
        var pluginTypeNames = pluginTypes.Select(p => p.PluginType.Name).ToList();
        Assert.Contains("CarterWebApplicationBuilderPlugin", pluginTypeNames);
        Assert.Contains("CarterWebApplicationPlugin", pluginTypeNames);
    }

    /// <summary>
    /// Verifies that SignalR plugins are registered via the SignalR package's own TypeRegistry.
    /// </summary>
    [Fact]
    public void SignalR_PluginsRegisteredViaOwnTypeRegistry()
    {
        // Arrange
        var signalRAssembly = typeof(SignalR.SignalRWebApplicationBuilderPlugin).Assembly;
        var typeRegistryType = signalRAssembly.GetType("NexusLabs.Needlr.SignalR.Generated.TypeRegistry");

        // Skip if TypeRegistry doesn't exist (expected failure case)
        if (typeRegistryType == null)
        {
            Assert.Fail("SignalR package does not have its own TypeRegistry - this is the bug!");
            return;
        }

        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);

        // Act - Get plugins from SignalR's own TypeRegistry
        var pluginTypes = (IReadOnlyList<PluginTypeInfo>)getPluginTypesMethod.Invoke(null, null)!;

        // Assert - SignalR's TypeRegistry should contain its own plugin
        var pluginTypeNames = pluginTypes.Select(p => p.PluginType.Name).ToList();
        Assert.Contains("SignalRWebApplicationBuilderPlugin", pluginTypeNames);
    }

    /// <summary>
    /// Verifies that all plugin types from Carter package are registered in NeedlrSourceGenBootstrap
    /// via the Carter package's own module initializer (not via consumer's TypeRegistry).
    /// </summary>
    [Fact]
    public void Carter_ModuleInitializerRegistersPlugins()
    {
        // Act - Get all registered plugin types from bootstrap
        var allPluginTypes = GetPluginTypesProvider()().ToList();

        // Assert - Carter plugins should be in the global registry
        var carterPluginTypes = allPluginTypes
            .Where(p => p.PluginType.Assembly == typeof(CarterWebApplicationBuilderPlugin).Assembly)
            .Select(p => p.PluginType.Name)
            .ToList();

        Assert.Contains("CarterWebApplicationBuilderPlugin", carterPluginTypes);
        Assert.Contains("CarterWebApplicationPlugin", carterPluginTypes);
    }

    /// <summary>
    /// Verifies that SignalR plugins are registered in NeedlrSourceGenBootstrap.
    /// </summary>
    [Fact]
    public void SignalR_ModuleInitializerRegistersPlugins()
    {
        // Act - Get all registered plugin types from bootstrap
        var allPluginTypes = GetPluginTypesProvider()().ToList();

        // Assert - SignalR plugins should be in the global registry
        var signalRPluginTypes = allPluginTypes
            .Where(p => p.PluginType.Assembly == typeof(SignalR.SignalRWebApplicationBuilderPlugin).Assembly)
            .Select(p => p.PluginType.Name)
            .ToList();

        Assert.Contains("SignalRWebApplicationBuilderPlugin", signalRPluginTypes);
    }
}
