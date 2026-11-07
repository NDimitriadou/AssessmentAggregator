namespace ReadInEveryWeatherAggregator.Api.Models;

// What GET /api/statistics returns.
public sealed record StatisticsResponse(DateTimeOffset GeneratedAt, IReadOnlyList<ApiStatistics> Apis);

// Statistics for one external API.
public sealed record ApiStatistics(
    string Name,
    long TotalRequests,
    long FailedRequests,
    double AverageResponseTimeMs,
    IReadOnlyList<PerformanceBucket> Buckets);

// Calls in one speed group (fast, average or slow).
public sealed record PerformanceBucket(
    string Name,
    string Range,
    long RequestCount,
    double AverageResponseTimeMs);

// One API's average time overall and in the last few minutes. Used to spot slowdowns.
public sealed record ApiPerformanceSummary(
    string Name,
    double OverallAverageMs,
    double RecentAverageMs,
    int RecentRequestCount);

// An API that has become much slower than usual.
public sealed record PerformanceAnomaly(
    string Name,
    double OverallAverageMs,
    double RecentAverageMs,
    double IncreasePercent);
