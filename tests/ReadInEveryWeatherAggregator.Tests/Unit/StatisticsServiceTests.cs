using ReadInEveryWeatherAggregator.Api.Options;
using ReadInEveryWeatherAggregator.Api.Services;
using Microsoft.Extensions.Time.Testing;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ReadInEveryWeatherAggregator.Tests.Unit;

public class StatisticsServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private StatisticsService CreateService() =>
        new(MsOptions.Create(new StatisticsOptions { FastThresholdMs = 100, SlowThresholdMs = 200, SampleRetentionMinutes = 15 }), _time);

    [Fact]
    public void GroupsRequests_IntoPerformanceBuckets()
    {
        var service = CreateService();
        service.Record("News", TimeSpan.FromMilliseconds(50), true);   // fast
        service.Record("News", TimeSpan.FromMilliseconds(70), true);   // fast
        service.Record("News", TimeSpan.FromMilliseconds(150), true);  // average
        service.Record("News", TimeSpan.FromMilliseconds(400), false); // slow

        var api = Assert.Single(service.GetStatistics().Apis);

        Assert.Equal(4, api.TotalRequests);
        Assert.Equal(1, api.FailedRequests);
        Assert.Equal(167.5, api.AverageResponseTimeMs);
        var buckets = api.Buckets.ToDictionary(b => b.Name);
        Assert.Equal((2, 60.0), (buckets["fast"].RequestCount, buckets["fast"].AverageResponseTimeMs));
        Assert.Equal((1, 150.0), (buckets["average"].RequestCount, buckets["average"].AverageResponseTimeMs));
        Assert.Equal((1, 400.0), (buckets["slow"].RequestCount, buckets["slow"].AverageResponseTimeMs));
    }

    [Theory]
    [InlineData(99, "fast")]
    [InlineData(100, "average")]
    [InlineData(200, "average")]
    [InlineData(201, "slow")]
    public void BucketBoundaries(int ms, string expectedBucket)
    {
        var service = CreateService();
        service.Record("A", TimeSpan.FromMilliseconds(ms), true);

        var bucket = service.GetStatistics().Apis[0].Buckets.Single(b => b.RequestCount == 1);
        Assert.Equal(expectedBucket, bucket.Name);
    }

    [Fact]
    public void KeepsSeparateStatistics_PerApi()
    {
        var service = CreateService();
        service.Record("A", TimeSpan.FromMilliseconds(10), true);
        service.Record("B", TimeSpan.FromMilliseconds(20), true);
        service.Record("b", TimeSpan.FromMilliseconds(40), true); // names are case-insensitive

        var apis = service.GetStatistics().Apis;
        Assert.Equal(2, apis.Count);
        Assert.Equal(30, apis.Single(a => a.Name.Equals("B", StringComparison.OrdinalIgnoreCase)).AverageResponseTimeMs);
    }

    [Fact]
    public async Task ConcurrentRecording_LosesNoUpdates()
    {
        var service = CreateService();
        const int perThread = 5_000;
        string[] apis = ["A", "B", "C"];

        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                service.Record(apis[(t + i) % apis.Length], TimeSpan.FromMilliseconds(i % 300), true);
            }
        })));

        var stats = service.GetStatistics().Apis;
        Assert.Equal(8 * perThread, stats.Sum(a => a.TotalRequests));
        Assert.All(stats, a => Assert.Equal(a.TotalRequests, a.Buckets.Sum(b => b.RequestCount)));
    }

    [Fact]
    public void PerformanceSummary_SeparatesRecentWindow_FromOverallAverage()
    {
        var service = CreateService();
        service.Record("A", TimeSpan.FromMilliseconds(100), true);
        service.Record("A", TimeSpan.FromMilliseconds(100), true);
        _time.Advance(TimeSpan.FromMinutes(10));
        service.Record("A", TimeSpan.FromMilliseconds(400), true);

        var summary = Assert.Single(service.GetPerformanceSummaries(TimeSpan.FromMinutes(5)));

        Assert.Equal(200, summary.OverallAverageMs);
        Assert.Equal(400, summary.RecentAverageMs);
        Assert.Equal(1, summary.RecentRequestCount);
    }

    [Fact]
    public void OldSamples_AreTrimmed_ButTotalsAreKept()
    {
        var service = CreateService();
        service.Record("A", TimeSpan.FromMilliseconds(100), true);
        _time.Advance(TimeSpan.FromMinutes(20)); // beyond 15 min retention
        service.Record("A", TimeSpan.FromMilliseconds(100), true);

        var summary = Assert.Single(service.GetPerformanceSummaries(TimeSpan.FromHours(1)));

        Assert.Equal(1, summary.RecentRequestCount);                         // the old call is no longer counted as recent
        Assert.Equal(2, service.GetStatistics().Apis[0].TotalRequests);      // but it is still in the totals
    }
}
