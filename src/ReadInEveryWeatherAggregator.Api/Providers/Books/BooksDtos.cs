using System.Text.Json.Serialization;

namespace ReadInEveryWeatherAggregator.Api.Providers.Books;

internal sealed record BooksSearchResponse(List<BookDoc>? Docs);

internal sealed record BookDoc(
    string? Key,
    string? Title,
    [property: JsonPropertyName("author_name")] List<string>? AuthorName,
    [property: JsonPropertyName("first_publish_year")] int? FirstPublishYear,
    [property: JsonPropertyName("edition_count")] int? EditionCount);
