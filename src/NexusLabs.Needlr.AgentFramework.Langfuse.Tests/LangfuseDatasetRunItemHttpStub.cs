using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// HTTP boundary helper that returns valid Langfuse dataset-run-item responses.
/// </summary>
internal static class LangfuseDatasetRunItemHttpStub
{
    public static HttpClient Create(
        string datasetRunId,
        List<CapturedRequest> captured) =>
        Create(_ => datasetRunId, captured);

    public static HttpClient Create(
        Func<int, string> datasetRunId,
        List<CapturedRequest> captured)
    {
        var calls = 0;
        return new HttpClient(new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var capturedRequest = new CapturedRequest(request.Method, request.RequestUri!, body);
            lock (captured)
            {
                captured.Add(capturedRequest);
            }

            await Task.Yield();
            var call = Interlocked.Increment(ref calls);
            return CreateResponse(
                capturedRequest,
                $"dataset-run-item-{call}",
                datasetRunId(call));
        }));
    }

    public static HttpResponseMessage CreateResponse(
        CapturedRequest request,
        string datasetRunItemId,
        string datasetRunId)
    {
        using var json = JsonDocument.Parse(request.Body!);
        return CreateResponse(
            datasetRunItemId,
            datasetRunId,
            json.RootElement.GetProperty("runName").GetString()!,
            json.RootElement.GetProperty("datasetItemId").GetString()!,
            json.RootElement.GetProperty("traceId").GetString()!);
    }

    public static HttpResponseMessage CreateResponse(
        string datasetRunItemId,
        string datasetRunId,
        string runName,
        string datasetItemId,
        string traceId) =>
        LangfuseHttpStub.Json(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                id = datasetRunItemId,
                datasetRunId,
                datasetRunName = runName,
                datasetItemId,
                traceId,
                observationId = (string?)null,
                createdAt = "2026-07-12T00:00:00Z",
                updatedAt = "2026-07-12T00:00:00Z",
            }));
}
