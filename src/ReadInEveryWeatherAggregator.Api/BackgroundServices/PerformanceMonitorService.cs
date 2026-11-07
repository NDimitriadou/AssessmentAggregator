using ReadInEveryWeatherAggregator.Api.Options;
using ReadInEveryWeatherAggregator.Api.Services;
using Microsoft.Extensions.Options;

namespace ReadInEveryWeatherAggregator.Api.BackgroundServices;

public sealed class PerformanceMonitorService(
    PerformanceAnomalyDetector detector,
    IOptions<AnomalyDetectionOptions> options,
    ILogger<PerformanceMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Performance anomaly detection is disabled");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false) is true)
        {
            try
            {
                detector.Analyze();
            }
            catch (Exception ex)
            {
                // If one check fails, log it and keep checking
                logger.LogError(ex, "Performance analysis failed");
            }
        }
    }
}
