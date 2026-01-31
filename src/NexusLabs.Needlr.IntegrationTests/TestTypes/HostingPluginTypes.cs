namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Test implementation of IHostApplicationBuilderPlugin for parity testing.
/// </summary>
public sealed class TestHostApplicationBuilderPlugin : Hosting.IHostApplicationBuilderPlugin
{
    public bool WasConfigured { get; private set; }

    public void Configure(Hosting.HostApplicationBuilderPluginOptions options)
    {
        WasConfigured = true;
    }
}

/// <summary>
/// Second test implementation for testing multiple plugin discovery.
/// </summary>
public sealed class SecondTestHostApplicationBuilderPlugin : Hosting.IHostApplicationBuilderPlugin
{
    public void Configure(Hosting.HostApplicationBuilderPluginOptions options)
    {
    }
}

/// <summary>
/// Test implementation of IHostPlugin for parity testing.
/// </summary>
public sealed class TestHostPlugin : Hosting.IHostPlugin
{
    public bool WasConfigured { get; private set; }

    public void Configure(Hosting.HostPluginOptions options)
    {
        WasConfigured = true;
    }
}

/// <summary>
/// Second test implementation for testing multiple plugin discovery.
/// </summary>
public sealed class SecondTestHostPlugin : Hosting.IHostPlugin
{
    public void Configure(Hosting.HostPluginOptions options)
    {
    }
}
