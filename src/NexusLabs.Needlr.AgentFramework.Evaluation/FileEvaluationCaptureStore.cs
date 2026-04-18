using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Disk-backed <see cref="IEvaluationCaptureStore"/> that persists each response
/// as a single JSON file under a caller-supplied directory. File names are the
/// request hash plus a <c>.json</c> extension.
/// </summary>
/// <remarks>
/// Writes are atomic per file via write-then-rename. Reads return <see langword="null"/>
/// when no file exists for the requested key, and propagate any other I/O error.
/// </remarks>
public sealed class FileEvaluationCaptureStore : IEvaluationCaptureStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _directoryPath;

    /// <summary>
    /// Creates a store that reads from and writes to <paramref name="directoryPath"/>.
    /// The directory is created on first write if it does not already exist.
    /// </summary>
    /// <param name="directoryPath">Absolute or relative path to the cache directory.</param>
    public FileEvaluationCaptureStore(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        _directoryPath = directoryPath;
    }

    /// <inheritdoc />
    public async Task<ChatResponse?> TryGetAsync(
        string key,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var path = GetPath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var payload = await JsonSerializer
            .DeserializeAsync<CapturedChatResponsePayload>(stream, _serializerOptions, cancellationToken)
            .ConfigureAwait(false);
        return payload?.ToChatResponse();
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string key,
        ChatResponse response,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(response);

        Directory.CreateDirectory(_directoryPath);
        var finalPath = GetPath(key);
        var tempPath = finalPath + ".tmp";

        var payload = CapturedChatResponsePayload.FromChatResponse(response);
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, payload, _serializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }
        File.Move(tempPath, finalPath);
    }

    private string GetPath(string key) => Path.Combine(_directoryPath, key + ".json");
}
