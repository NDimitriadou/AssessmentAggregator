using System.Text.Json.Serialization;

namespace ReadInEveryWeatherAggregator.Api.Providers.Weather;

internal sealed record GeocodingResponse(List<GeoLocation>? Results);

internal sealed record GeoLocation(string Name, string? Country, double Latitude, double Longitude);

internal sealed record ForecastResponse(DailyForecast? Daily);

internal sealed record DailyForecast(
    List<string>? Time,
    [property: JsonPropertyName("temperature_2m_max")] List<double?>? TemperatureMax,
    [property: JsonPropertyName("temperature_2m_min")] List<double?>? TemperatureMin,
    [property: JsonPropertyName("precipitation_sum")] List<double?>? Precipitation,
    [property: JsonPropertyName("weathercode")] List<int?>? WeatherCode);
