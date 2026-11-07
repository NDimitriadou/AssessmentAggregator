using System.Text.Json.Serialization;

namespace ReadInEveryWeatherAggregator.Api.Providers.News;

internal sealed record NewsSearchResponse(List<NewsHit>? Hits);

internal sealed record NewsHit(
    string? Title,
    string? Url,
    string? Author,
    int? Points,
    [property: JsonPropertyName("num_comments")] int? NumComments,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("objectID")] string? ObjectId);
