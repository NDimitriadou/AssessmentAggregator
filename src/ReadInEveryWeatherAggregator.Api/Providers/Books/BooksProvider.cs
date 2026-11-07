using System.Net.Http.Json;
using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Options;
using Microsoft.Extensions.Options;

namespace ReadInEveryWeatherAggregator.Api.Providers.Books;

/// <summary>
/// Searches books with the Open Library API (https://openlibrary.org/dev/docs/api/search). No API key needed.
/// </summary>
public sealed class BooksProvider(HttpClient httpClient, IOptionsMonitor<ExternalApiOptions> options) : IBooksProvider
{
    public const string ProviderName = "Books";

    // Ask only for the fields we use, so the response is smaller
    private const string Fields = "key,title,author_name,first_publish_year,edition_count";

    public string Name => ProviderName;
    public string Category => "books";

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Get(Name);

        var url = $"{settings.BaseUrl.TrimEnd('/')}/search.json?limit={settings.MaxItems}" +
                  $"&fields={Fields}&q={Uri.EscapeDataString(request.Query)}";
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        // Open Library asks clients to identify themselves via User-Agent
        message.Headers.UserAgent.ParseAdd("ReadInEveryWeatherAggregator/1.0");

        // If the call failed, throw. The aggregator will then return saved data instead
        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BooksSearchResponse>(cancellationToken);
        var docs = body?.Docs ?? [];

        return docs
            .Where(d => !string.IsNullOrWhiteSpace(d.Title))
            .Select(d => new AggregatedItem
            {
                Source = Name,
                Category = Category,
                Title = d.Title!,
                Description = DescribeBook(d),
                Url = d.Key is null ? null : $"{settings.BaseUrl.TrimEnd('/')}{d.Key}",
                // We only know the year, so use 1 January of that year
                Date = d.FirstPublishYear is int year and >= 1 and <= 9999
                    ? new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    : null
            })
            .ToList();
    }

    // Builds text like: "by Author A, Author B · first published 2004 · 3 editions"
    private static string DescribeBook(BookDoc d)
    {
        var parts = new List<string>();
        if (d.AuthorName is { Count: > 0 }) parts.Add($"by {string.Join(", ", d.AuthorName.Take(3))}");
        if (d.FirstPublishYear is not null) parts.Add($"first published {d.FirstPublishYear}");
        if (d.EditionCount is > 0) parts.Add($"{d.EditionCount} edition{(d.EditionCount == 1 ? "" : "s")}");
        return string.Join(" · ", parts);
    }
}
