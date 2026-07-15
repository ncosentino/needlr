using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Creates valid Langfuse dataset and dataset-item API responses for focused boundary tests.
/// </summary>
internal static class LangfuseDatasetApiStub
{
    public static HttpResponseMessage Dataset(
        string id,
        string name,
        object? metadata = null) =>
        LangfuseHttpStub.Json(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                id,
                projectId = "project-1",
                name,
                description = "evaluation dataset",
                metadata,
                inputSchema = (object?)null,
                expectedOutputSchema = (object?)null,
                createdAt = "2026-07-01T00:00:00Z",
                updatedAt = "2026-07-02T00:00:00Z",
            }));

    public static object Item(
        string id,
        string datasetId,
        string datasetName,
        string status = "ACTIVE",
        object? input = null,
        object? expectedOutput = null,
        object? metadata = null,
        string? sourceTraceId = null,
        string? sourceObservationId = null) =>
        new
        {
            id,
            datasetId,
            datasetName,
            status,
            input,
            expectedOutput,
            metadata,
            sourceTraceId,
            sourceObservationId,
            createdAt = "2026-07-03T00:00:00Z",
            updatedAt = "2026-07-04T00:00:00Z",
            mediaReferences = Array.Empty<object>(),
        };

    public static HttpResponseMessage Page(
        int page,
        int pageSize,
        int totalItems,
        int totalPages,
        params object[] items) =>
        LangfuseHttpStub.Json(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                data = items,
                meta = new
                {
                    page,
                    limit = pageSize,
                    totalItems,
                    totalPages,
                },
            }));
}
