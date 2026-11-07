using System.Net.Http.Json;
using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Options;
using Microsoft.Extensions.Options;

namespace ReadInEveryWeatherAggregator.Api.Providers.News;

/// <summary>
/// Searches tech news on Hacker News, through the Algolia API (https://hn.algolia.com/api). No API key needed.
/// </summary>
public sealed class NewsProvider(HttpClient httpClient, IOptionsMonitor<ExternalApiOptions> options) : INewsProvider
{
    public const string ProviderName = "News";

    public string Name => ProviderName;
    public string Category => "news";

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Get(Name);

        var url = $"{settings.BaseUrl.TrimEnd('/')}/api/v1/search?tags=story" +
                  $"&hitsPerPage={settings.MaxItems}&query={Uri.EscapeDataString(request.Query)}";
        var response = await httpClient.GetFromJsonAsync<NewsSearchResponse>(url, cancellationToken);
        var hits = response?.Hits ?? [];

        return hits
            .Where(h => !string.IsNullOrWhiteSpace(h.Title))
            .Select(h => new AggregatedItem
            {
                Source = Name,
                Category = Category,
                Title = h.Title!,
                Description = $"by {h.Author} · {h.Points ?? 0} points · {h.NumComments ?? 0} comments",
                // Some posts have no link, only text. Link to the Hacker News page instead
                Url = string.IsNullOrWhiteSpace(h.Url) ? $"https://news.ycombinator.com/item?id={h.ObjectId}" : h.Url,
                Date = h.CreatedAt
            })
            .ToList();
    }
}
