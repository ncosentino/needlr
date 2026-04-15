using System.Text.Json;

namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Default implementation that discovers the GitHub OAuth token from
/// configured sources (explicit value, apps.json, or environment variables).
/// </summary>
internal sealed class GitHubOAuthTokenProvider : IGitHubOAuthTokenProvider
{
    private readonly CopilotChatClientOptions _options;
    private string? _cachedToken;

    public GitHubOAuthTokenProvider(CopilotChatClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string GetOAuthToken()
    {
        if (_cachedToken is not null)
        {
            return _cachedToken;
        }

        _cachedToken = ResolveToken();
        return _cachedToken;
    }

    private string ResolveToken()
    {
        if (!string.IsNullOrWhiteSpace(_options.GitHubToken))
        {
            return _options.GitHubToken;
        }

        return _options.TokenSource switch
        {
            CopilotTokenSource.AppsJson => ReadFromAppsJson()
                ?? throw new InvalidOperationException(
                    "No OAuth token found in apps.json. Ensure the Copilot CLI is logged in."),

            CopilotTokenSource.EnvironmentVariable => ReadFromEnvironment()
                ?? throw new InvalidOperationException(
                    "No GH_TOKEN or GITHUB_TOKEN environment variable found."),

            CopilotTokenSource.Auto => ReadFromAppsJson()
                ?? ReadFromEnvironment()
                ?? throw new InvalidOperationException(
                    "No GitHub OAuth token found. Log in via the Copilot CLI, " +
                    "set GH_TOKEN/GITHUB_TOKEN, or provide an explicit GitHubToken in options."),

            _ => throw new ArgumentOutOfRangeException(nameof(_options.TokenSource)),
        };
    }

    internal static string? ReadFromAppsJson()
    {
        var appsJsonPath = GetAppsJsonPath();
        if (appsJsonPath is null || !File.Exists(appsJsonPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(appsJsonPath);
            using var doc = JsonDocument.Parse(json);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.TryGetProperty("oauth_token", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        return token;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    internal static string? ReadFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("GH_TOKEN")
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    }

    internal static string? GetAppsJsonPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "github-copilot", "apps.json");
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "github-copilot", "apps.json");
        }

        if (OperatingSystem.IsLinux())
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, "github-copilot", "apps.json");
        }

        return null;
    }
}
