namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a discovered named HttpClient options type (from [HttpClientOptions]).
/// </summary>
internal readonly struct DiscoveredHttpClient
{
    public DiscoveredHttpClient(
        string typeName,
        string clientName,
        string sectionName,
        string assemblyName,
        HttpClientCapabilities capabilities,
        string? sourceFilePath = null)
    {
        TypeName = typeName;
        ClientName = clientName;
        SectionName = sectionName;
        AssemblyName = assemblyName;
        Capabilities = capabilities;
        SourceFilePath = sourceFilePath;
    }

    /// <summary>Fully qualified type name of the options class (e.g., "global::MyApp.WebFetchHttpClientOptions").</summary>
    public string TypeName { get; }

    /// <summary>The resolved HttpClient name used for <c>services.AddHttpClient("Name", ...)</c>.</summary>
    public string ClientName { get; }

    /// <summary>The resolved configuration section name (e.g., "HttpClients:WebFetch").</summary>
    public string SectionName { get; }

    public string AssemblyName { get; }

    /// <summary>Which capability interfaces the type implements — drives conditional emission.</summary>
    public HttpClientCapabilities Capabilities { get; }

    public string? SourceFilePath { get; }
}
