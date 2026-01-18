using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

public sealed class PostPluginRegistrationCallbackTests
{
    [Fact]
    public void BuildWebApplication_UsingPostPluginRegistrationCallback_RunsAfterOptionsCallbacks()
    {
        List<string> callbackCalls = [];

        var webApplication = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(_ =>
            {
                callbackCalls.Add(nameof(SyringeExtensions.UsingPostPluginRegistrationCallback));
            })
            .ForWebApplication()
            .UsingOptions(() => CreateWebApplicationOptions.Default with
            {
                PostPluginRegistrationCallbacks =
                [
                    _ => callbackCalls.Add(nameof(WebApplicationSyringeExtensions.UsingOptions))
                ]
            })
            .BuildWebApplication();

        Assert.NotNull(webApplication);

        Assert.Equal(2, callbackCalls.Count);
        Assert.Equal(nameof(WebApplicationSyringeExtensions.UsingOptions), callbackCalls[0]);
        Assert.Equal(nameof(SyringeExtensions.UsingPostPluginRegistrationCallback), callbackCalls[1]);
    }

    [Fact]
    public void BuildWebApplication_MultipleUsingPostPluginRegistrationCallback_Chains()
    {
        List<string> callbackCalls = [];

        var webApplication = new Syringe()
            .UsingReflection()
            .UsingPostPluginRegistrationCallback(_ =>
            {
                callbackCalls.Add(nameof(SyringeExtensions.UsingPostPluginRegistrationCallback) + "1");
            })
            .UsingPostPluginRegistrationCallback(_ =>
            {
                callbackCalls.Add(nameof(SyringeExtensions.UsingPostPluginRegistrationCallback) + "2");
            })
            .ForWebApplication()
            .UsingOptions(() => CreateWebApplicationOptions.Default with
            {
                PostPluginRegistrationCallbacks =
                [
                    _ => callbackCalls.Add(nameof(WebApplicationSyringeExtensions.UsingOptions))
                ]
            })
            .BuildWebApplication();

        Assert.NotNull(webApplication);

        Assert.Equal(3, callbackCalls.Count);
        Assert.Equal(nameof(WebApplicationSyringeExtensions.UsingOptions), callbackCalls[0]);
        Assert.Equal(nameof(SyringeExtensions.UsingPostPluginRegistrationCallback) + "1", callbackCalls[1]);
        Assert.Equal(nameof(SyringeExtensions.UsingPostPluginRegistrationCallback) + "2", callbackCalls[2]);
    }
}