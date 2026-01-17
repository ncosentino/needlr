using System.Reflection;
using System.Threading;

using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

public sealed class PrePluginRegistrationCallbackTests
{
    [Fact]
    public void BuildWebApplication_UsingPrePluginRegistrationCallback_RunsBeforePluginAndPostCallbacks()
    {
        var events = new List<string>();
        PrePostOrderLog.Current.Value = events;

        var webApplication = new Syringe()
            .UsingAdditionalAssemblies([Assembly.GetExecutingAssembly()])
            .ForWebApplication()
            .UsingOptions(() => CreateWebApplicationOptions.Default
                .UsingPrePluginRegistrationCallback(_ => events.Add("Pre-Options"))
                .UsingPostPluginRegistrationCallback(_ => events.Add("Post-Options")))
            .BuildWebApplication();

        Assert.NotNull(webApplication);

        Assert.Equal(["Pre-Options", "Plugin", "Post-Options"], events);

        PrePostOrderLog.Current.Value = null;
    }

    [Fact]
    public void BuildWebApplication_MultipleUsingPrePluginRegistrationCallback_ChainsAndPreservesOrder()
    {
        var events = new List<string>();
        PrePostOrderLog.Current.Value = events;

        var webApplication = new Syringe()
            .UsingAdditionalAssemblies([Assembly.GetExecutingAssembly()])
            .ForWebApplication()
            .UsingOptions(() => CreateWebApplicationOptions.Default
                .UsingPrePluginRegistrationCallback(_ => events.Add("Pre1"))
                .UsingPrePluginRegistrationCallback(_ => events.Add("Pre2"))
                .UsingPostPluginRegistrationCallback(_ => events.Add("Post1"))
                .UsingPostPluginRegistrationCallback(_ => events.Add("Post2")))
            .BuildWebApplication();

        Assert.NotNull(webApplication);

        Assert.Equal(["Pre1", "Pre2", "Plugin", "Post1", "Post2"], events);

        PrePostOrderLog.Current.Value = null;
    }
}

internal static class PrePostOrderLog
{
    public static readonly AsyncLocal<List<string>?> Current = new();
}

public sealed class TestWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        PrePostOrderLog.Current.Value?.Add("Plugin");
    }
}
