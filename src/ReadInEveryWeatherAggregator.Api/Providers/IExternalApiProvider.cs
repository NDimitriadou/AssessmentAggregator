using ReadInEveryWeatherAggregator.Api.Models;

namespace ReadInEveryWeatherAggregator.Api.Providers;

/// <summary>
/// What every external API class must have.
/// Each API also has its own interface built on this one (e.g. IWeatherProvider).
/// To add a new API: make a folder with its interface, class and DTOs, then add
/// <c>.AddExternalApiProvider&lt;IMyProvider, MyProvider&gt;(name, configuration)</c> in Program.cs.
/// </summary>
public interface IExternalApiProvider
{
    /// <summary>The API's name. Its settings in appsettings.json use the same name under "ExternalApis".</summary>
    string Name { get; }

    string Category { get; }

    /// <summary>Calls the API and converts the result to <see cref="AggregatedItem"/>s.
    /// If the call fails it throws; the aggregation service decides what to do then.</summary>
    Task<IReadOnlyList<AggregatedItem>> FetchAsync(ProviderRequest request, CancellationToken cancellationToken);
}
