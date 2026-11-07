using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Options;
using Microsoft.Extensions.Options;

namespace ReadInEveryWeatherAggregator.Api.Services;

/// <summary>
/// Checks if an API has become slower: compares its average time in the last few minutes
/// with its overall average, and logs a warning if it is more than ThresholdPercent slower.
/// It is a separate class from the background service so it is easy to test.
/// </summary>
public sealed class PerformanceAnomalyDetector(
    IStatisticsService statistics,
    IOptions<AnomalyDetectionOptions> options,
    ILogger<PerformanceAnomalyDetector> logger)
{
    private readonly AnomalyDetectionOptions _options = options.Value;

    public IReadOnlyList<PerformanceAnomaly> Analyze()
    {
        var anomalies = new List<PerformanceAnomaly>();
        var summaries = statistics.GetPerformanceSummaries(TimeSpan.FromMinutes(_options.WindowMinutes));

        foreach (var s in summaries)
        {
            if (s.RecentRequestCount < _options.MinRecentSamples || s.OverallAverageMs <= 0)
            {
                continue;
            }

            var increasePercent = (s.RecentAverageMs - s.OverallAverageMs) / s.OverallAverageMs * 100;
            if (increasePercent > _options.ThresholdPercent)
            {
                var anomaly = new PerformanceAnomaly(s.Name, s.OverallAverageMs, s.RecentAverageMs, Math.Round(increasePercent, 1));
                anomalies.Add(anomaly);
                logger.LogWarning(
                    "Performance anomaly: {Api} averaged {RecentMs} ms over the last {Window} min vs {OverallMs} ms overall (+{Increase}%)",
                    anomaly.Name, anomaly.RecentAverageMs, _options.WindowMinutes, anomaly.OverallAverageMs, anomaly.IncreasePercent);
            }
        }

        return anomalies;
    }
}
