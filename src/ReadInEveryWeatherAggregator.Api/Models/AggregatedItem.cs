namespace ReadInEveryWeatherAggregator.Api.Models;

/// <summary>
/// Every API's result is converted to this same shape,
/// so items from different sources can be filtered and sorted together.
/// </summary>
public sealed record AggregatedItem
{
    //Which API the item came from (e.g. "News").
    public required string Source { get; init; }

    //Category of the item (e.g. "weather", "news", "books").
    public required string Category { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Url { get; init; }

    // the item refers to (forecast day, publish date, last update)
    public DateTimeOffset? Date { get; init; }
}
