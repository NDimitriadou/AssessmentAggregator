namespace ReadInEveryWeatherAggregator.Api.Options;

// Settings for one external API, read from "ExternalApis:{Name}" in appsettings.json.
public sealed class ExternalApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    // API key, for APIs that need one. None of the current APIs do.
    public string? ApiKey { get; set; }

    // How many items to ask the API for (for Weather: how many days).
    public int MaxItems { get; set; } = 20;

    // Weather only: the service that turns a city name into coordinates.
    public string? GeocodingUrl { get; set; }
}

public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";
    public int MaxRetryAttempts { get; set; } = 2;
    public int RetryBaseDelayMs { get; set; } = 200;
    public int AttemptTimeoutSeconds { get; set; } = 5;
    public int TotalTimeoutSeconds { get; set; } = 15;
}

public sealed class CachingOptions
{
    public const string SectionName = "Caching";

    // How long saved data counts as recent. During this time the API is not called again.
    public int FreshSeconds { get; set; } = 120;

    // How long old data is kept, to return it when the API fails
    public int FallbackMinutes { get; set; } = 60;
}

public sealed class StatisticsOptions
{
    public const string SectionName = "Statistics";
    public int FastThresholdMs { get; set; } = 100;
    public int SlowThresholdMs { get; set; } = 200;

    // How long each call's timing is kept, for checking the last few minutes.
    public int SampleRetentionMinutes { get; set; } = 15;
}

public sealed class AnomalyDetectionOptions
{
    public const string SectionName = "AnomalyDetection";
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int WindowMinutes { get; set; } = 5;

    // Warn when the recent average time is this many percent higher than the overall average.
    public double ThresholdPercent { get; set; } = 50;

    // How many recent calls an API needs before it is checked.
    public int MinRecentSamples { get; set; } = 3;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "ReadInEveryWeatherAggregator";
    public string Audience { get; set; } = "ReadInEveryWeatherAggregator.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    public string DemoUsername { get; set; } = "demo";
    public string DemoPassword { get; set; } = string.Empty;
}
