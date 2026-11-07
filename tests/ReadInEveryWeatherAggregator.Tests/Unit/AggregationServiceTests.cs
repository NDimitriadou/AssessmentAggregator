using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Options;
using ReadInEveryWeatherAggregator.Api.Services;
using ReadInEveryWeatherAggregator.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ReadInEveryWeatherAggregator.Tests.Unit;

public class AggregationServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly StatisticsService _statistics;
    private readonly ProviderCache _cache;

    public AggregationServiceTests()
    {
        _statistics = new StatisticsService(MsOptions.Create(new StatisticsOptions()), _time);
        _cache = new ProviderCache(
            new MemoryCache(new MemoryCacheOptions()),
            MsOptions.Create(new CachingOptions { FreshSeconds = 60, FallbackMinutes = 30 }),
            _time);
    }

    private AggregationService CreateService(params FakeProvider[] providers) =>
        new(providers, _cache, _statistics, _time, NullLogger<AggregationService>.Instance);

    [Fact]
    public async Task Aggregates_ItemsFromAllProviders()
    {
        var service = CreateService(
            FakeProvider.Returning("A", "news", 2),
            FakeProvider.Returning("B", "books", 3));

        var result = await service.AggregateAsync(new AggregationQuery(), CancellationToken.None);

        Assert.Equal(5, result.TotalItems);
        Assert.All(result.Sources, s => Assert.Equal(SourceState.Live, s.State));
    }

    [Fact]
    public async Task CallsProviders_InParallel()
    {
        // Each provider waits until all three have started. This only works if they run at the same time
        const int providerCount = 3;
        var started = 0;
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<IReadOnlyList<AggregatedItem>> Behavior(ProviderRequest _, CancellationToken __)
        {
            if (Interlocked.Increment(ref started) == providerCount) allStarted.SetResult();
            await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return [];
        }

        var service = CreateService(
            new FakeProvider("A", "x", Behavior),
            new FakeProvider("B", "x", Behavior),
            new FakeProvider("C", "x", Behavior));

        var result = await service.AggregateAsync(new AggregationQuery(), CancellationToken.None);

        Assert.All(result.Sources, s => Assert.Equal(SourceState.Live, s.State));
    }

    [Fact]
    public async Task SecondCall_IsServedFromCache()
    {
        var provider = FakeProvider.Returning("A", "news");
        var service = CreateService(provider);

        await service.AggregateAsync(new AggregationQuery(), CancellationToken.None);
        var second = await service.AggregateAsync(new AggregationQuery(), CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(SourceState.Cached, Assert.Single(second.Sources).State);
        Assert.Equal(2, second.TotalItems);
    }

    [Fact]
    public async Task DifferentQuery_UsesDifferentCacheEntry()
    {
        var provider = FakeProvider.Returning("A", "news");
        var service = CreateService(provider);

        await service.AggregateAsync(new AggregationQuery { Query = "dotnet" }, CancellationToken.None);
        await service.AggregateAsync(new AggregationQuery { Query = "rust" }, CancellationToken.None);

        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task FailingProvider_WithoutCache_IsReportedAsFailed_OthersStillReturned()
    {
        var service = CreateService(
            FakeProvider.Returning("A", "news", 2),
            FakeProvider.Failing("B", "books"));

        var result = await service.AggregateAsync(new AggregationQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalItems);
        var failed = result.Sources.Single(s => s.Name == "B");
        Assert.Equal(SourceState.Failed, failed.State);
        Assert.Equal("Service unavailable", failed.Error);
    }

    [Fact]
    public async Task FailingProvider_WithStaleCache_ReturnsFallbackData()
    {
        var provider = FakeProvider.Returning("A", "news", 3);
        var service = CreateService(provider);
        await service.AggregateAsync(new AggregationQuery(), CancellationToken.None);

        // Saved data is now old and the API fails
        _time.Advance(TimeSpan.FromMinutes(5));
        provider.Behavior = (_, _) => throw new HttpRequestException("down");

        var result = await service.AggregateAsync(new AggregationQuery(), CancellationToken.None);

        var status = Assert.Single(result.Sources);
        Assert.Equal(SourceState.Fallback, status.State);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task OnlySelectedSourcesAndCategories_AreCalled()
    {
        var news = FakeProvider.Returning("News", "news");
        var books = FakeProvider.Returning("Books", "books");
        var weather = FakeProvider.Returning("Weather", "weather");
        var service = CreateService(news, books, weather);

        await service.AggregateAsync(new AggregationQuery { Sources = "news,books", Category = "books" }, CancellationToken.None);

        Assert.Equal(0, news.CallCount);    // excluded by category
        Assert.Equal(1, books.CallCount);
        Assert.Equal(0, weather.CallCount); // excluded by sources
    }

    [Fact]
    public async Task RecordsStatistics_ForLiveCallsOnly()
    {
        var service = CreateService(FakeProvider.Returning("A", "news"), FakeProvider.Failing("B", "books"));

        await service.AggregateAsync(new AggregationQuery(), CancellationToken.None);
        await service.AggregateAsync(new AggregationQuery(), CancellationToken.None); // A cached, B fails again

        var stats = _statistics.GetStatistics().Apis.ToDictionary(a => a.Name);
        Assert.Equal(1, stats["A"].TotalRequests);
        Assert.Equal(2, stats["B"].TotalRequests);
        Assert.Equal(2, stats["B"].FailedRequests);
    }

    [Fact]
    public async Task ClientCancellation_IsPropagated()
    {
        using var cts = new CancellationTokenSource();
        var service = CreateService(new FakeProvider("A", "x", async (_, ct) =>
        {
            await cts.CancelAsync();
            ct.ThrowIfCancellationRequested();
            return [];
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AggregateAsync(new AggregationQuery(), cts.Token));
    }
}
