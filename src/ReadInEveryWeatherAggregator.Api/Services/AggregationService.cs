using System.Diagnostics;
using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Providers;

namespace ReadInEveryWeatherAggregator.Api.Services;

public interface IAggregationService
{
    IReadOnlyList<(string Name, string Category)> GetProviders();

    Task<AggregationResponse> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// The main logic: picks which APIs to call, calls them all at the same time,
/// uses saved data when possible or when an API fails, records timings, and merges the results.
/// </summary>
public sealed class AggregationService(
    IEnumerable<IExternalApiProvider> providers,
    ProviderCache cache,
    IStatisticsService statistics,
    TimeProvider timeProvider,
    ILogger<AggregationService> logger) : IAggregationService
{
    private readonly IReadOnlyList<IExternalApiProvider> _providers = providers.ToList();

    public IReadOnlyList<(string Name, string Category)> GetProviders() =>
        _providers.Select(p => (p.Name, p.Category)).ToList();

    public async Task<AggregationResponse> AggregateAsync(AggregationQuery query, CancellationToken cancellationToken)
    {
        var sources = query.GetSources();
        var categories = query.GetCategories();
        var selected = _providers
            .Where(p => sources.Count == 0 || sources.Contains(p.Name))
            .Where(p => categories.Count == 0 || categories.Contains(p.Category))
            .ToList();

        var request = new ProviderRequest(query.Query, query.City);
        var results = await Task.WhenAll(selected.Select(p => FetchProviderAsync(p, request, cancellationToken)));

        var items = ItemFilter.Apply(results.SelectMany(r => r.Items), query);

        return new AggregationResponse(
            timeProvider.GetUtcNow(),
            items.Count,
            results.Select(r => r.Status).ToList(),
            items);
    }

    /// <summary>
    /// Gets data for one API. Order: recent saved data, then a real call, then older saved data, then nothing.
    /// It never throws (unless the caller cancelled), so one failing API cannot break the whole response.
    /// </summary>
    private async Task<ProviderResult> FetchProviderAsync(IExternalApiProvider provider, ProviderRequest request, CancellationToken cancellationToken)
    {
        var key = ProviderCache.BuildKey(provider.Name, request);

        if (cache.TryGetFresh(key, out var cached))
        {
            return new ProviderResult(cached, new SourceStatus(provider.Name, SourceState.Cached, cached.Count, null, null));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Short failures were already retried by the HttpClient
            var items = await provider.FetchAsync(request, cancellationToken);
            stopwatch.Stop();

            statistics.Record(provider.Name, stopwatch.Elapsed, success: true);
            cache.Store(key, items);
            return new ProviderResult(items, new SourceStatus(provider.Name, SourceState.Live, items.Count, stopwatch.ElapsedMilliseconds, null));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            statistics.Record(provider.Name, stopwatch.Elapsed, success: false);
            logger.LogWarning(ex, "Provider {Provider} failed after {ElapsedMs} ms", provider.Name, stopwatch.ElapsedMilliseconds);

            if (cache.TryGetFallback(key, out var stale))
            {
                return new ProviderResult(stale, new SourceStatus(provider.Name, SourceState.Fallback, stale.Count, stopwatch.ElapsedMilliseconds, ex.Message));
            }

            return new ProviderResult([], new SourceStatus(provider.Name, SourceState.Failed, 0, stopwatch.ElapsedMilliseconds, ex.Message));
        }
    }

    private sealed record ProviderResult(IReadOnlyList<AggregatedItem> Items, SourceStatus Status);
}
