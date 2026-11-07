using System.Collections.Concurrent;
using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Options;
using Microsoft.Extensions.Options;

namespace ReadInEveryWeatherAggregator.Api.Services;

public interface IStatisticsService
{
    void Record(string apiName, TimeSpan duration, bool success);

    StatisticsResponse GetStatistics();

    IReadOnlyList<ApiPerformanceSummary> GetPerformanceSummaries(TimeSpan window);
}

/// <summary>
/// Keeps call statistics in memory. One instance is shared by all requests.
/// Each API has its own counters with its own lock, so many requests can record at the same time
/// safely, and requests for different APIs never wait for each other.
/// </summary>
public sealed class StatisticsService(IOptions<StatisticsOptions> options, TimeProvider timeProvider) : IStatisticsService
{
    private readonly StatisticsOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, ApiAccumulator> _apis = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string apiName, TimeSpan duration, bool success)
    {
        var accumulator = _apis.GetOrAdd(apiName, _ => new ApiAccumulator());
        var now = timeProvider.GetUtcNow();
        var retentionStart = now.AddMinutes(-_options.SampleRetentionMinutes);
        accumulator.Add(duration.TotalMilliseconds, success, GetBucketIndex(duration.TotalMilliseconds), now, retentionStart);
    }

    public StatisticsResponse GetStatistics()
    {
        var bucketNames = GetBucketDefinitions();
        var apis = _apis
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv =>
            {
                var snapshot = kv.Value.Snapshot();
                var buckets = bucketNames
                    .Select((b, i) => new PerformanceBucket(
                        b.Name,
                        b.Range,
                        snapshot.BucketCounts[i],
                        Average(snapshot.BucketTotalMs[i], snapshot.BucketCounts[i])))
                    .ToList();
                return new ApiStatistics(kv.Key, snapshot.Total, snapshot.Failed, Average(snapshot.TotalMs, snapshot.Total), buckets);
            })
            .ToList();

        return new StatisticsResponse(timeProvider.GetUtcNow(), apis);
    }

    public IReadOnlyList<ApiPerformanceSummary> GetPerformanceSummaries(TimeSpan window)
    {
        var windowStart = timeProvider.GetUtcNow() - window;
        return _apis
            .Select(kv =>
            {
                var snapshot = kv.Value.Snapshot();
                var recent = snapshot.Samples.Where(s => s.Timestamp >= windowStart).ToList();
                return new ApiPerformanceSummary(
                    kv.Key,
                    Average(snapshot.TotalMs, snapshot.Total),
                    recent.Count == 0 ? 0 : Math.Round(recent.Average(s => s.DurationMs), 2),
                    recent.Count);
            })
            .ToList();
    }

    // 0 = fast, 1 = average, 2 = slow
    private int GetBucketIndex(double ms) =>
        ms < _options.FastThresholdMs ? 0 : ms <= _options.SlowThresholdMs ? 1 : 2;

    private (string Name, string Range)[] GetBucketDefinitions() =>
    [
        ("fast", $"< {_options.FastThresholdMs} ms"),
        ("average", $"{_options.FastThresholdMs}-{_options.SlowThresholdMs} ms"),
        ("slow", $"> {_options.SlowThresholdMs} ms")
    ];

    private static double Average(double total, long count) => count == 0 ? 0 : Math.Round(total / count, 2);

    private sealed record Sample(DateTimeOffset Timestamp, double DurationMs);

    private sealed record AccumulatorSnapshot(
        long Total, long Failed, double TotalMs, long[] BucketCounts, double[] BucketTotalMs, Sample[] Samples);

    /// <summary>The counters for one API. Always use <see cref="_lock"/> when reading or changing them.</summary>
    private sealed class ApiAccumulator
    {
        private readonly object _lock = new();
        private readonly long[] _bucketCounts = new long[3];
        private readonly double[] _bucketTotalMs = new double[3];
        private readonly Queue<Sample> _samples = new();
        private long _total;
        private long _failed;
        private double _totalMs;

        public void Add(double ms, bool success, int bucket, DateTimeOffset now, DateTimeOffset retentionStart)
        {
            lock (_lock)
            {
                _total++;
                _totalMs += ms;
                if (!success) _failed++;
                _bucketCounts[bucket]++;
                _bucketTotalMs[bucket] += ms;

                // Drop old timings so memory does not keep growing
                _samples.Enqueue(new Sample(now, ms));
                while (_samples.TryPeek(out var oldest) && oldest.Timestamp < retentionStart)
                {
                    _samples.Dequeue();
                }
            }
        }

        public AccumulatorSnapshot Snapshot()
        {
            lock (_lock)
            {
                return new AccumulatorSnapshot(
                    _total, _failed, _totalMs,
                    (long[])_bucketCounts.Clone(), (double[])_bucketTotalMs.Clone(), _samples.ToArray());
            }
        }
    }
}
