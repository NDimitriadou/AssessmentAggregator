using ReadInEveryWeatherAggregator.Api.Options;
using ReadInEveryWeatherAggregator.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ReadInEveryWeatherAggregator.Tests.Unit;

/// <summary>A warning is expected when the last 5 minutes are more than 50% slower than the overall average.</summary>
public class PerformanceAnomalyDetectorTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly StatisticsService _statistics;
    private readonly PerformanceAnomalyDetector _detector;

    public PerformanceAnomalyDetectorTests()
    {
        _statistics = new StatisticsService(MsOptions.Create(new StatisticsOptions { SampleRetentionMinutes = 60 }), _time);
        _detector = new PerformanceAnomalyDetector(
            _statistics,
            MsOptions.Create(new AnomalyDetectionOptions { WindowMinutes = 5, ThresholdPercent = 50, MinRecentSamples = 3 }),
            NullLogger<PerformanceAnomalyDetector>.Instance);
    }

    private void Record(string api, int count, int ms)
    {
        for (var i = 0; i < count; i++) _statistics.Record(api, TimeSpan.FromMilliseconds(ms), true);
    }

    [Fact]
    public void DetectsAnomaly_WhenRecentAverageIsMuchSlower()
    {
        Record("News", 20, 100);                 // history: fast
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("News", 5, 600);                  // last 5 min: slow

        var anomaly = Assert.Single(_detector.Analyze());

        Assert.Equal("News", anomaly.Name);
        Assert.Equal(600, anomaly.RecentAverageMs);
        Assert.True(anomaly.IncreasePercent > 50);
    }

    [Fact]
    public void NoAnomaly_WhenPerformanceIsStable()
    {
        Record("News", 20, 100);
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("News", 5, 120);

        Assert.Empty(_detector.Analyze());
    }

    [Fact]
    public void NoAnomaly_WhenNotEnoughRecentSamples()
    {
        Record("News", 20, 100);
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("News", 2, 1000); // too few recent calls to check

        Assert.Empty(_detector.Analyze());
    }

    [Fact]
    public void OnlyTheSlowApi_IsReported()
    {
        Record("News", 10, 100);
        Record("Books", 10, 100);
        _time.Advance(TimeSpan.FromMinutes(10));
        Record("News", 5, 100);
        Record("Books", 5, 900);

        Assert.Equal("Books", Assert.Single(_detector.Analyze()).Name);
    }
}
