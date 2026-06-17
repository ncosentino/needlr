namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Sets process environment variables for the duration of a test and restores their previous
/// values on disposal. Used to exercise <see cref="LangfuseOptions.FromEnvironment"/> without
/// leaking state across tests.
/// </summary>
internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> _previousValues = [];

    public EnvironmentVariableScope(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (var (key, value) in values)
        {
            _previousValues[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public void Dispose()
    {
        foreach (var (key, value) in _previousValues)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
