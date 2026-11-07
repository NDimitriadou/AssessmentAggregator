using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ReadInEveryWeatherAggregator.Api.Services;

/// <summary>
/// Saves each API's results in memory.
/// For a short time the saved data is used instead of calling the API again.
/// After that it is kept a while longer, but only used if the API fails.
/// </summary>
public sealed class ProviderCache(IMemoryCache cache, IOptions<CachingOptions> options, TimeProvider timeProvider)
{
    private readonly CachingOptions _options = options.Value;

    private sealed record Entry(IReadOnlyList<AggregatedItem> Items, DateTimeOffset StoredAt);

    /// <summary>The key is the API name plus the search word and city, because those change the result.</summary>
    public static string BuildKey(string provider, ProviderRequest request) =>
        $"provider:{provider}:{request.Query.Trim().ToLowerInvariant()}:{request.City.Trim().ToLowerInvariant()}";

    public bool TryGetFresh(string key, out IReadOnlyList<AggregatedItem> items)
    {
        if (cache.TryGetValue(key, out Entry? entry) && entry is not null &&
            timeProvider.GetUtcNow() - entry.StoredAt < TimeSpan.FromSeconds(_options.FreshSeconds))
        {
            items = entry.Items;
            return true;
        }

        items = [];
        return false;
    }

    public bool TryGetFallback(string key, out IReadOnlyList<AggregatedItem> items)
    {
        if (cache.TryGetValue(key, out Entry? entry) && entry is not null)
        {
            items = entry.Items;
            return true;
        }

        items = [];
        return false;
    }

    public void Store(string key, IReadOnlyList<AggregatedItem> items) =>
        cache.Set(key, new Entry(items, timeProvider.GetUtcNow()), TimeSpan.FromMinutes(_options.FallbackMinutes));
}
