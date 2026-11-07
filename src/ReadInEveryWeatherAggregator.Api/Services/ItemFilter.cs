using ReadInEveryWeatherAggregator.Api.Models;

namespace ReadInEveryWeatherAggregator.Api.Services;

public static class ItemFilter
{
    public static IReadOnlyList<AggregatedItem> Apply(IEnumerable<AggregatedItem> items, AggregationQuery query)
    {
        var categories = query.GetCategories();
        if (categories.Count > 0)
        {
            items = items.Where(i => categories.Contains(i.Category));
        }

        // If the caller asks for dates, items without a date are left out
        if (query.From is { } from)
        {
            items = items.Where(i => i.Date >= from);
        }

        if (query.To is { } to)
        {
            items = items.Where(i => i.Date <= to);
        }

        var descending = query.Order == SortOrder.Desc;
        IOrderedEnumerable<AggregatedItem> sorted = query.SortBy switch
        {
            SortField.Title => OrderByDirection(items, i => i.Title, descending, StringComparer.OrdinalIgnoreCase),
            SortField.Source => OrderByDirection(items, i => i.Source, descending, StringComparer.OrdinalIgnoreCase),
            // Items without a date always go at the end
            _ => items.OrderBy(i => i.Date is null).ThenByDirection(i => i.Date, descending)
        };

        return sorted
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .Take(query.Limit)
            .ToList();
    }

    private static IOrderedEnumerable<T> OrderByDirection<T, TKey>(IEnumerable<T> source, Func<T, TKey> key, bool descending, IComparer<TKey>? comparer = null) =>
        descending ? source.OrderByDescending(key, comparer) : source.OrderBy(key, comparer);

    private static IOrderedEnumerable<T> ThenByDirection<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> key, bool descending) =>
        descending ? source.ThenByDescending(key) : source.ThenBy(key);
}
