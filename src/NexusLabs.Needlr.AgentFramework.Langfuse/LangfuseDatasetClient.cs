using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Default <see cref="ILangfuseDatasetClient"/> backed by the shared <see cref="LangfuseApiClient"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseDatasetClient : ILangfuseDatasetClient
{
    private const int MaxPageSize = 100;

    private readonly LangfuseApiClient _apiClient;
    private readonly int _pageSize;

    public LangfuseDatasetClient(
        LangfuseApiClient apiClient,
        int pageSize = MaxPageSize)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ValidatePageSize(pageSize);

        _apiClient = apiClient;
        _pageSize = pageSize;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public async Task<LangfusePage<LangfuseDataset>> ListDatasetsAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page);
        ValidatePageSize(pageSize);

        var response = await ReadPageAsync<LangfuseDatasetRef>(
            $"api/public/v2/datasets?page={page}&limit={pageSize}",
            "dataset response",
            page,
            pageSize,
            cancellationToken).ConfigureAwait(false);
        var datasets = response.Data
            .Select(dataset => MapDataset(dataset, expectedName: null))
            .ToArray();
        return CreatePage(response.Meta, datasets);
    }

    /// <inheritdoc />
    public async Task<LangfusePage<LangfuseDatasetItemSnapshot>> ListDatasetItemsAsync(
        LangfuseDatasetSelection selection,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        selection.Validate();
        ValidatePage(page);
        ValidatePageSize(pageSize);

        var response = await ReadDatasetItemPageAsync(
            selection,
            page,
            pageSize,
            cancellationToken).ConfigureAwait(false);
        var items = response.Data
            .Select(item => MapDatasetItem(
                item,
                selection.Name,
                expectedDatasetId: null))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
        return CreatePage(response.Meta, items);
    }

    /// <inheritdoc />
    public async Task<LangfuseDatasetSnapshot> GetDatasetAsync(
        LangfuseDatasetSelection selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        selection.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var datasetResponse = await GetResponseAsync<LangfuseDatasetRef>(
                $"api/public/v2/datasets/{Uri.EscapeDataString(selection.Name)}",
                cancellationToken)
            .ConfigureAwait(false);
        if (datasetResponse is null)
        {
            throw new LangfuseException(
                $"Langfuse dataset '{selection.Name}' returned an empty response.");
        }

        var dataset = MapDataset(datasetResponse, selection.Name);
        var items = new List<LangfuseDatasetItemSnapshot>();
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        int? expectedTotalItems = null;
        int? expectedTotalPages = null;
        var rawItemCount = 0;

        for (var page = 1; ; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await ReadDatasetItemPageAsync(
                selection,
                page,
                _pageSize,
                cancellationToken).ConfigureAwait(false);

            if (expectedTotalItems is null)
            {
                expectedTotalItems = response.Meta.TotalItems;
                expectedTotalPages = response.Meta.TotalPages;
            }
            else if (response.Meta.TotalItems != expectedTotalItems
                || response.Meta.TotalPages != expectedTotalPages)
            {
                throw new LangfuseException(
                    $"Langfuse dataset '{selection.Name}' changed while its item pages were being materialized.");
            }

            foreach (var itemResponse in response.Data)
            {
                rawItemCount++;
                var item = MapDatasetItem(
                    itemResponse,
                    dataset.Name,
                    dataset.Id);
                if (!itemIds.Add(itemResponse.Id))
                {
                    throw new LangfuseException(
                        $"Langfuse dataset '{dataset.Name}' returned duplicate dataset item id '{itemResponse.Id}'.");
                }

                if (item is not null)
                {
                    items.Add(item);
                }
            }

            if (response.Meta.TotalPages == 0
                || page >= response.Meta.TotalPages)
            {
                break;
            }
        }

        if (rawItemCount != expectedTotalItems)
        {
            throw new LangfuseException(
                $"Langfuse dataset '{selection.Name}' returned {rawItemCount} items while pagination reported {expectedTotalItems}.");
        }

        return new LangfuseDatasetSnapshot
        {
            Dataset = dataset,
            Selection = selection,
            Items = Array.AsReadOnly(items.ToArray()),
        };
    }

    /// <inheritdoc />
    public async Task EnsureDatasetAsync(string name, string? description = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var request = new LangfuseCreateDatasetRequest
        {
            Name = name,
            Description = description,
        };

        try
        {
            await _apiClient
                .PostIdempotentAsync(
                    "api/public/v2/datasets",
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LangfuseHttpException ex)
            when (ex.StatusCode is System.Net.HttpStatusCode.Conflict)
        {
            var existing = await _apiClient
                .GetOrDefaultAsync<LangfuseDatasetRef>(
                    $"api/public/v2/datasets/{Uri.EscapeDataString(name)}",
                    cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null
                && string.Equals(existing.Name, name, StringComparison.Ordinal)
                && string.Equals(existing.Description, description, StringComparison.Ordinal))
            {
                return;
            }

            throw;
        }
    }

    /// <inheritdoc />
    public Task UpsertItemAsync(LangfuseDatasetItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.DatasetName);

        return _apiClient.PostAsync(
            "api/public/dataset-items",
            LangfuseCreateDatasetItemRequest.From(item),
            cancellationToken);
    }

    private async Task<(IReadOnlyList<LangfuseDatasetItemDto> Data, LangfusePaginationMeta Meta)> ReadDatasetItemPageAsync(
        LangfuseDatasetSelection selection,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        await ReadPageAsync<LangfuseDatasetItemDto>(
            BuildDatasetItemPath(selection, page, pageSize),
            $"dataset item response for '{selection.Name}'",
            page,
            pageSize,
            cancellationToken).ConfigureAwait(false);

    private async Task<(IReadOnlyList<T> Data, LangfusePaginationMeta Meta)> ReadPageAsync<T>(
        string path,
        string responseDescription,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var response = await GetResponseAsync<LangfusePageDto<T>>(
            path,
            cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            throw new LangfuseException(
                $"Langfuse {responseDescription} was empty.");
        }

        if (response.Data is not { } data)
        {
            throw new LangfuseException(
                $"Langfuse {responseDescription} did not include a data collection.");
        }

        if (response.Meta is not { } meta)
        {
            throw new LangfuseException(
                $"Langfuse {responseDescription} did not include pagination metadata.");
        }

        ValidatePageMetadata(
            responseDescription,
            data.Count,
            meta,
            page,
            pageSize);
        return (data, meta);
    }

    private async Task<T?> GetResponseAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClient
                .GetAsync<T>(path, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new LangfuseException(
                $"Langfuse returned malformed JSON for response type '{typeof(T).Name}'.",
                ex);
        }
    }

    private static string BuildDatasetItemPath(
        LangfuseDatasetSelection selection,
        int page,
        int pageSize)
    {
        var path =
            $"api/public/dataset-items?datasetName={Uri.EscapeDataString(selection.Name)}" +
            $"&page={page}&limit={pageSize}";
        return selection.GetVersionText() is { } version
            ? $"{path}&version={Uri.EscapeDataString(version)}"
            : path;
    }

    private static LangfusePage<T> CreatePage<T>(
        LangfusePaginationMeta meta,
        T[] items) =>
        new()
        {
            Items = Array.AsReadOnly(items),
            Page = meta.Page,
            PageSize = meta.Limit,
            TotalItems = meta.TotalItems,
            TotalPages = meta.TotalPages,
        };

    private static LangfuseDataset MapDataset(
        LangfuseDatasetRef response,
        string? expectedName)
    {
        if (string.IsNullOrWhiteSpace(response.Id))
        {
            throw new LangfuseException(
                "Langfuse dataset response did not include a dataset id.");
        }

        if (string.IsNullOrWhiteSpace(response.Name))
        {
            throw new LangfuseException(
                "Langfuse dataset response did not include a dataset name.");
        }

        if (expectedName is not null
            && !string.Equals(response.Name, expectedName, StringComparison.Ordinal))
        {
            throw new LangfuseException(
                $"Langfuse returned dataset '{response.Name}' when '{expectedName}' was requested.");
        }

        if (response.CreatedAt == default || response.UpdatedAt == default)
        {
            throw new LangfuseException(
                $"Langfuse dataset '{response.Name}' did not include valid timestamps.");
        }

        return new LangfuseDataset
        {
            Id = response.Id,
            Name = response.Name,
            Description = response.Description,
            Metadata = Clone(response.Metadata),
            InputSchema = Clone(response.InputSchema),
            ExpectedOutputSchema = Clone(response.ExpectedOutputSchema),
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt,
        };
    }

    private static LangfuseDatasetItemSnapshot? MapDatasetItem(
        LangfuseDatasetItemDto response,
        string expectedDatasetName,
        string? expectedDatasetId)
    {
        if (string.IsNullOrWhiteSpace(response.Id))
        {
            throw new LangfuseException(
                $"Langfuse dataset '{expectedDatasetName}' returned an item without an id.");
        }

        if (string.IsNullOrWhiteSpace(response.DatasetId))
        {
            throw new LangfuseException(
                $"Langfuse dataset item '{response.Id}' did not include a dataset id.");
        }

        if (string.IsNullOrWhiteSpace(response.DatasetName))
        {
            throw new LangfuseException(
                $"Langfuse dataset item '{response.Id}' did not include a dataset name.");
        }

        if (!string.Equals(response.DatasetName, expectedDatasetName, StringComparison.Ordinal))
        {
            throw new LangfuseException(
                $"Langfuse dataset item '{response.Id}' belongs to dataset '{response.DatasetName}' instead of '{expectedDatasetName}'.");
        }

        if (expectedDatasetId is not null
            && !string.Equals(response.DatasetId, expectedDatasetId, StringComparison.Ordinal))
        {
            throw new LangfuseException(
                $"Langfuse dataset item '{response.Id}' belongs to dataset id '{response.DatasetId}' instead of '{expectedDatasetId}'.");
        }

        if (response.CreatedAt == default || response.UpdatedAt == default)
        {
            throw new LangfuseException(
                $"Langfuse dataset item '{response.Id}' did not include valid timestamps.");
        }

        if (string.Equals(response.Status, "ARCHIVED", StringComparison.Ordinal))
        {
            return null;
        }

        if (!string.Equals(response.Status, "ACTIVE", StringComparison.Ordinal))
        {
            throw new LangfuseException(
                $"Langfuse dataset item '{response.Id}' returned unsupported status '{response.Status}'.");
        }

        return new LangfuseDatasetItemSnapshot
        {
            Id = response.Id,
            DatasetId = response.DatasetId,
            DatasetName = response.DatasetName,
            Input = Clone(response.Input),
            ExpectedOutput = Clone(response.ExpectedOutput),
            Metadata = Clone(response.Metadata),
            SourceTraceId = response.SourceTraceId,
            SourceObservationId = response.SourceObservationId,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt,
        };
    }

    private static JsonElement? Clone(JsonElement? value) =>
        value is { } element ? element.Clone() : null;

    private static void ValidatePageMetadata(
        string responseDescription,
        int itemCount,
        LangfusePaginationMeta meta,
        int expectedPage,
        int expectedPageSize)
    {
        if (meta.Page != expectedPage)
        {
            throw new LangfuseException(
                $"Langfuse {responseDescription} reported page {meta.Page} while page {expectedPage} was requested.");
        }

        if (meta.Limit != expectedPageSize)
        {
            throw new LangfuseException(
                $"Langfuse {responseDescription} reported page size {meta.Limit} while {expectedPageSize} was requested.");
        }

        if (meta.TotalItems < 0 || meta.TotalPages < 0)
        {
            throw new LangfuseException(
                $"Langfuse {responseDescription} returned negative pagination totals.");
        }

        var expectedTotalPages = meta.TotalItems == 0
            ? 0
            : (int)(((long)meta.TotalItems + meta.Limit - 1) / meta.Limit);
        if (meta.TotalPages != expectedTotalPages)
        {
            throw new LangfuseException(
                $"Langfuse {responseDescription} reported {meta.TotalPages} pages for {meta.TotalItems} items at page size {meta.Limit}.");
        }

        var expectedItemCount = meta.TotalPages == 0 || expectedPage > meta.TotalPages
            ? 0
            : expectedPage < meta.TotalPages
                ? meta.Limit
                : meta.TotalItems - ((meta.TotalPages - 1) * meta.Limit);
        if (itemCount != expectedItemCount)
        {
            throw new LangfuseException(
                $"Langfuse {responseDescription} returned {itemCount} items while pagination required {expectedItemCount}.");
        }
    }

    private static void ValidatePage(int page)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                page,
                "Langfuse page numbers must be positive.");
        }
    }

    private static void ValidatePageSize(int pageSize)
    {
        if (pageSize is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Langfuse page size must be from 1 through {MaxPageSize}.");
        }
    }
}
