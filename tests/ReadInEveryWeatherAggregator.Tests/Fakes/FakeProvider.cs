using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Providers;

namespace ReadInEveryWeatherAggregator.Tests.Fakes;

public sealed class FakeProvider(string name, string category, Func<ProviderRequest, CancellationToken, Task<IReadOnlyList<AggregatedItem>>> behavior)
    : IExternalApiProvider
{
    private int _callCount;

    public string Name => name;
    public string Category => category;
    public int CallCount => Volatile.Read(ref _callCount);

    // Lets a test change the behaviour between calls (e.g. succeed, then fail).
    public Func<ProviderRequest, CancellationToken, Task<IReadOnlyList<AggregatedItem>>> Behavior { get; set; } = behavior;

    public Task<IReadOnlyList<AggregatedItem>> FetchAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return Behavior(request, cancellationToken);
    }

    public static FakeProvider Returning(string name, string category, int count = 2) =>
        new(name, category, (_, _) => Task.FromResult(Items(name, category, count)));

    public static FakeProvider Failing(string name, string category) =>
        new(name, category, (_, _) => throw new HttpRequestException("Service unavailable"));

    public static IReadOnlyList<AggregatedItem> Items(string source, string category, int count) =>
        Enumerable.Range(0, count)
            .Select(i => new AggregatedItem
            {
                Source = source,
                Category = category,
                Title = $"{source} item {i}",
                Date = new DateTimeOffset(2025, 1, 1 + i, 0, 0, 0, TimeSpan.Zero)
            })
            .ToList();
}
