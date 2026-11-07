using System.Globalization;
using System.Net.Http.Json;
using ReadInEveryWeatherAggregator.Api.Models;
using ReadInEveryWeatherAggregator.Api.Options;
using Microsoft.Extensions.Options;

namespace ReadInEveryWeatherAggregator.Api.Providers.Weather;

/// <summary>
/// Daily weather forecast from Open-Meteo (https://open-meteo.com). Free, no API key needed.
/// Makes two calls: first finds the city's coordinates, then gets the forecast for them.
/// </summary>
public sealed class WeatherProvider(HttpClient httpClient, IOptionsMonitor<ExternalApiOptions> options) : IWeatherProvider
{
    public const string ProviderName = "Weather";

    public string Name => ProviderName;
    public string Category => "weather";

    public async Task<IReadOnlyList<AggregatedItem>> FetchAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Get(Name);

        var geoUrl = $"{settings.GeocodingUrl?.TrimEnd('/')}/v1/search?count=1&name={Uri.EscapeDataString(request.City)}";
        var geo = await httpClient.GetFromJsonAsync<GeocodingResponse>(geoUrl, cancellationToken);
        var location = geo?.Results?.FirstOrDefault();
        if (location is null)
        {
            return []; // City not found: not an error, we just return no weather
        }

        var days = Math.Clamp(settings.MaxItems, 1, 16);
        var forecastUrl = string.Create(CultureInfo.InvariantCulture,
            $"{settings.BaseUrl.TrimEnd('/')}/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}" +
            $"&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,weathercode&timezone=auto&forecast_days={days}");
        var forecast = await httpClient.GetFromJsonAsync<ForecastResponse>(forecastUrl, cancellationToken);
        var daily = forecast?.Daily;
        if (daily?.Time is null)
        {
            return [];
        }

        var items = new List<AggregatedItem>(daily.Time.Count);
        for (var i = 0; i < daily.Time.Count; i++)
        {
            var code = ValueAt(daily.WeatherCode, i);
            items.Add(new AggregatedItem
            {
                Source = Name,
                Category = Category,
                Title = string.Create(CultureInfo.InvariantCulture,
                    $"{location.Name}: {DescribeWeatherCode(code)}, {ValueAt(daily.TemperatureMin, i):0.#}°C – {ValueAt(daily.TemperatureMax, i):0.#}°C"),
                Description = string.Create(CultureInfo.InvariantCulture,
                    $"{location.Name}, {location.Country}. Precipitation: {ValueAt(daily.Precipitation, i):0.#} mm"),
                Url = "https://open-meteo.com/",
                Date = DateTimeOffset.Parse(daily.Time[i], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            });
        }

        return items;
    }

    private static T? ValueAt<T>(IReadOnlyList<T>? values, int index) =>
        values is not null && index < values.Count ? values[index] : default;

    // Turns Open-Meteo's weather code numbers into words
    internal static string DescribeWeatherCode(int? code) => code switch
    {
        0 => "Clear sky",
        1 or 2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        >= 51 and <= 57 => "Drizzle",
        >= 61 and <= 67 => "Rain",
        >= 71 and <= 77 => "Snow",
        >= 80 and <= 82 => "Rain showers",
        85 or 86 => "Snow showers",
        >= 95 => "Thunderstorm",
        _ => "Unknown"
    };
}
