using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Services;

namespace ReadInEveryWeatherAggregator.Tests.Unit;

public class ItemFilterTests
{
    private static readonly AggregatedItem[] Items =
    [
        new() { Source = "News", Category = "news", Title = "beta", Date = Day(3) },
        new() { Source = "Books", Category = "books", Title = "Alpha", Date = Day(1) },
        new() { Source = "Weather", Category = "weather", Title = "gamma", Date = Day(2) },
        new() { Source = "News", Category = "news", Title = "delta", Date = null }
    ];

    private static DateTimeOffset Day(int d) => new(2025, 1, d, 0, 0, 0, TimeSpan.Zero);

    private static List<string> Titles(AggregationQuery q) => ItemFilter.Apply(Items, q).Select(i => i.Title).ToList();

    [Fact]
    public void FiltersByCategory_CaseInsensitive_MultipleValues() =>
        Assert.Equal(["beta", "Alpha", "delta"], Titles(new AggregationQuery { Category = "NEWS, books" }));

    [Fact]
    public void FiltersByDateRange_AndExcludesUndatedItems() =>
        Assert.Equal(["gamma", "Alpha"], Titles(new AggregationQuery { From = Day(1), To = Day(2) }));

    [Fact]
    public void SortsByDateDescending_ByDefault_UndatedLast() =>
        Assert.Equal(["beta", "gamma", "Alpha", "delta"], Titles(new AggregationQuery()));

    [Fact]
    public void SortsByDateAscending_UndatedStillLast() =>
        Assert.Equal(["Alpha", "gamma", "beta", "delta"], Titles(new AggregationQuery { Order = SortOrder.Asc }));

    [Fact]
    public void SortsByTitle_Ascending_CaseInsensitive() =>
        Assert.Equal(["Alpha", "beta", "delta", "gamma"], Titles(new AggregationQuery { SortBy = SortField.Title, Order = SortOrder.Asc }));

    [Fact]
    public void SortsBySource_ThenTitle() =>
        Assert.Equal(["Alpha", "beta", "delta", "gamma"], Titles(new AggregationQuery { SortBy = SortField.Source, Order = SortOrder.Asc }));

    [Fact]
    public void AppliesLimit() =>
        Assert.Equal(2, ItemFilter.Apply(Items, new AggregationQuery { Limit = 2 }).Count);
}
