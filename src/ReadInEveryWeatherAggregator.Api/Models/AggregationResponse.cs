namespace ReadInEveryWeatherAggregator.Api.Models;

// Where a source's data came from in this response
public enum SourceState
{
    // Just fetched from the API.
    Live,
    // Taken from the cache; the data is recent.
    Cached,
    // The API failed, so older saved data was returned instead.
    Fallback,
    // The API failed and there was no saved data to return.
    Failed
}

//Status of each source, so the caller can see if some data is old or missing.
public sealed record SourceStatus(
    string Name,
    SourceState State,
    int ItemCount,
    long? ResponseTimeMs,
    string? Error);

// What GET /api/aggregation returns.
public sealed record AggregationResponse(
    DateTimeOffset GeneratedAt,
    int TotalItems,
    IReadOnlyList<SourceStatus> Sources,
    IReadOnlyList<AggregatedItem> Items);

// What each provider needs to call its API (only the search word and the city).
public sealed record ProviderRequest(string Query, string City);
