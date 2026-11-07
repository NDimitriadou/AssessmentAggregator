# ReadInEveryWeather Aggregator

A small ASP.NET Core API that brings weather forecasts, news, and books together in one place.

The idea is simple: provide a city and a topic, and the application collects relevant information from three external services.
Instead of making separate requests yourself, you get one response that you can filter and sort.

This project was built as an API aggregation exercise, with an emphasis on clean design, asynchronous requests, reliability, and testability.

## What does it connect to

| Information | Provider | What it returns | API key needed |
|---|---|---|---|
| Weather | [Open-Meteo](https://open-meteo.com/) | Weather forecasts for the selected city | No |
| News | [Hacker News (Algolia Search)](https://hn.algolia.com/api) | Stories related to the search term | No |
| Books | [Open Library](https://openlibrary.org/developers/api) | Books matching the search term | No |

The application does not require external API keys. It can be run locally after cloning the repository.

> **A small data detail:** Open Library often provides only the first publication year, not a full date.
> To keep date sorting consistent, the application represents this as January 1 of that year.
> This is a normalization choice, not the exact publication date.

## Getting started

### Requirements

- .NET 8 SDK
- A terminal (or an IDE such as Visual Studio)

### Run

From the repository root, run:

```bash
dotnet restore
dotnet run --project src/ReadInEveryWeatherAggregator.Api --launch-profile http
```

With the supplied HTTP launch profile, the API is available at `http://localhost:5053`.
In the Development environment, you can explore its endpoints through Swagger UI:

```
http://localhost:5053/swagger
```

To run the tests:

```bash
dotnet test
```

### Try it in Swagger

The API uses JWT authentication, including for the aggregation and statistics endpoints.
For local development, the example credentials are in `appsettings.Development.json`.

1. Open Swagger UI.
2. Call `POST /api/auth/token` with the demo username and password.
3. Copy the `accessToken` from the response.
4. Click **Authorize** and enter the token.
5. Call `GET /api/aggregation` or `GET /api/statistics`.

For the provided development configuration, the demo credentials are `demo` / `demo123`.
They are for local testing only and should not be used in a deployed application.

### Or try it using curl

The following example is for Bash and uses `jq` to extract the JWT:

```bash
# Get a development token
TOKEN=$(curl -s -X POST http://localhost:5053/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"demo","password":"demo123"}' | jq -r .accessToken)

# Fetch weather, news, and books
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5053/api/aggregation?query=dotnet&city=Athens&sortBy=date&limit=10"

# See how the external APIs are performing
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5053/api/statistics"
```

## API endpoints

All endpoints except `POST /api/auth/token` require a valid Bearer token.

### `POST /api/auth/token`

Returns a JWT to use with the other endpoints.

Request:

```json
{
  "username": "demo",
  "password": "demo123"
}
```

Example response (200):

```json
{
  "accessToken": "eyJhbGciOi...",
  "tokenType": "Bearer",
  "expiresAt": "2026-11-05T13:00:00+00:00"
}
```

Invalid credentials return `401` with a ProblemDetails response.

### `GET /api/aggregation`

This is the main endpoint. It asks the selected providers for data, merges their results into a common item format,
and applies any requested filters or sorting.

Example:

```
GET /api/aggregation?query=dotnet&city=Athens&sortBy=date&limit=10
```

| Parameter | Default | What it does |
|---|---|---|
| `query` | `dotnet` | Topic used to search news and books |
| `city` | `Athens` | City used for the weather forecast |
| `sources` | All | Include specific providers, for example `News,Books` |
| `category` | All | Include `weather`, `news`, and/or `books` |
| `from`, `to` | None | Filter items by an ISO date range |
| `sortBy` | `date` | Sort by `date`, `title`, or `source` |
| `order` | `desc` | Choose `asc` or `desc` |
| `limit` | `50` | Maximum number of items (1–200) |

`sources` and `category` selection is case-insensitive. Providers excluded by these parameters are not contacted, avoiding unnecessary external requests.

Example response (shortened and illustrative):

```json
{
  "generatedAt": "2026-11-05T11:40:11Z",
  "totalItems": 1,
  "sources": [
    { "name": "News", "state": "Live", "itemCount": 20, "responseTimeMs": 600, "error": null },
    { "name": "Books", "state": "Fallback", "itemCount": 20, "responseTimeMs": 15000, "error": "External API unavailable" },
    { "name": "Weather", "state": "Cached", "itemCount": 7, "responseTimeMs": null, "error": null }
  ],
  "items": [
    {
      "source": "Books",
      "category": "books",
      "title": "Example Book",
      "description": "Example book description",
      "url": "https://openlibrary.org/works/example",
      "date": "2001-01-01T00:00:00+00:00"
    }
  ]
}
```

The response tells you not just what was returned, but also how each provider performed:

| State | Meaning |
|---|---|
| `Live` | Data came from an external API call |
| `Cached` | Fresh cached data was available, so no new external call was necessary |
| `Fallback` | The provider failed, but an older successful result was available |
| `Failed` | The provider failed and there was no cached result to return |

Invalid parameters (such as an unsupported sort field, an invalid date range, or a limit outside 1–200) return `400` with validation details. Missing or invalid authentication returns `401`.

### `GET /api/aggregation/sources`

Lists the available providers and their categories. This is useful for clients that want to discover supported sources instead of hardcoding them.

```json
[
  { "name": "Weather", "category": "weather" },
  { "name": "News", "category": "news" },
  { "name": "Books", "category": "books" }
]
```

### `GET /api/statistics`

Shows how often each external API has been called, how many calls failed, and how long the requests took on average. Calls are also grouped into three performance buckets:

- **Fast:** under 100 ms
- **Average:** 100–200 ms
- **Slow:** over 200 ms

These thresholds are configurable. Cache hits do not count as external API calls.

Example response:

```json
{
  "generatedAt": "2026-11-05T11:40:11Z",
  "apis": [
    {
      "name": "News",
      "totalRequests": 4,
      "failedRequests": 1,
      "averageResponseTimeMs": 167.5,
      "buckets": [
        { "name": "fast", "range": "< 100 ms", "requestCount": 2, "averageResponseTimeMs": 60 },
        { "name": "average", "range": "100-200 ms", "requestCount": 1, "averageResponseTimeMs": 150 },
        { "name": "slow", "range": "> 200 ms", "requestCount": 1, "averageResponseTimeMs": 400 }
      ]
    }
  ]
}
```

## How it works

The application keeps controllers relatively small. The main work happens in the aggregation service and the individual API providers.

```
HTTP request
    |
    v
AggregationController
    |
    v
AggregationService
    |-- Weather provider --> Open-Meteo
    |-- News provider ----> Hacker News / Algolia
    |-- Books provider ---> Open Library
    |
    |-- ProviderCache (fresh data and fallback)
    |-- ItemFilter (filter, sort, limit)
    |-- StatisticsService (request timings)
    v
Unified JSON response
```

For each aggregation request, the flow is:

1. Validate the request parameters.
2. Determine which providers should run.
3. Start the selected providers concurrently using `Task.WhenAll`.
4. Let each provider use fresh cache data, request live data, or fall back to older cached data if needed.
5. Merge successful results, apply filters and sorting, and return one response.

Running the providers concurrently is particularly helpful when external APIs have different response times. One slow or unavailable provider should not prevent the others from contributing useful data.

### Adding another provider

The design is intended to make integrations easy to extend. A new provider follows the same pattern as the existing ones:

1. Add `Providers/<Name>/` with its own interface, implementation, and external-API DTOs.
2. Implement `IExternalApiProvider`, including the provider name, category, and `FetchAsync` mapping to `AggregatedItem`.
3. Add an `ExternalApis:<Name>` configuration section.
4. Register it in the dependency injection setup:

```csharp
.AddExternalApiProvider<IMyNewProvider, MyNewProvider>("MyNew", config)
```

The shared aggregation, caching, resilience, and statistics infrastructure then applies to the new provider. Each provider can also be resolved through its specific interface when needed.

## Reliability and performance

External APIs are not always fast or available. This project handles that in several ways.

**Retries and timeouts.** HTTP calls use `Microsoft.Extensions.Http.Resilience` (built on Polly v8). Transient errors such as 5xx, 408, 429, and network failures can trigger retries with exponential backoff and jitter. Per-attempt and overall timeouts limit how long requests can run. A circuit breaker avoids repeatedly calling a provider that is consistently failing.

**Caching and fallback.** `IMemoryCache` keeps successful provider responses. By default, cached data is fresh for 120 seconds. It can be retained for up to 60 minutes for fallback when a live call fails. Cache entries are based on provider inputs such as query and city; filtering and sorting happen afterward so different views can reuse the same data.

**Isolated failures.** If one provider fails, other providers can still return results. The response includes a status for each provider, making partial results visible to the client. Unexpected application errors are logged and returned as safe ProblemDetails responses. Client cancellations are respected rather than treated as provider failures.

**Thread-safe statistics.** Statistics are kept in memory using a singleton `StatisticsService`. A `ConcurrentDictionary` holds per-provider accumulators, each protected by its own lock so counts, timing totals, and buckets remain consistent under concurrent requests. Recent timing samples are retained for a configurable window; aggregate counters remain available for the lifetime of the process.

## Optional features included

**JWT authentication.** The application includes a demo token endpoint. Tokens are HMAC-SHA256 signed, and the API validates their issuer, audience, lifetime, and signing key. Demo credentials are intended only for local development.

**Performance anomaly monitoring.** A background service periodically checks recent API response times. With the default settings, it compares the last five minutes against the overall average and logs a warning if recent performance is more than 50% slower, provided there are enough samples.

## Configuration

Settings are defined in `appsettings.json`, with development-only settings in `appsettings.Development.json`.

| Setting | Default | Purpose |
|---|---|---|
| `ExternalApis:<Name>:BaseUrl` | Per provider | External service URL |
| `ExternalApis:<Name>:MaxItems` | 20 (weather: 7 days) | Limits the data fetched |
| `ExternalApis:<Name>:ApiKey` | Not needed by current providers | Reserved for integrations that require a key |
| `ExternalApis:<Name>:GeocodingUrl` | Weather-specific | Resolves a city for weather requests |
| `Resilience:MaxRetryAttempts` | 2 | Maximum retry attempts |
| `Resilience:RetryBaseDelayMs` | 200 | Starting retry delay |
| `Resilience:AttemptTimeoutSeconds` | 5 | Timeout for an individual attempt |
| `Resilience:TotalTimeoutSeconds` | 15 | Overall request timeout |
| `Caching:FreshSeconds` | 120 | How long cached data is considered fresh |
| `Caching:FallbackMinutes` | 60 | How long old data may be kept for fallback |
| `Statistics:FastThresholdMs` | 100 | Fast/average boundary |
| `Statistics:SlowThresholdMs` | 200 | Average/slow boundary |
| `Statistics:SampleRetentionMinutes` | 15 | Recent timing sample retention |
| `AnomalyDetection:*` | Enabled, every 60 seconds | Monitoring window, threshold, and minimum sample count |
| `Jwt:*` | Development values in development settings | Token issuer, audience, lifetime, signing key, and demo credentials |

For non-development environments, the JWT signing key and demo password must be supplied securely, for example using environment variables. The signing key must contain at least 32 characters.

```bash
export Jwt__SigningKey="a-long-random-secret-of-at-least-32-chars"
export Jwt__DemoPassword="replace-this-with-a-secure-value"
```

> Do not commit real credentials or signing keys to source control.

## Tests

The test suite uses xUnit and covers both individual components and the HTTP endpoints. External services are replaced with fakes, so the tests do not rely on live API availability.

There are 56 tests covering:

| Area | Examples |
|---|---|
| Provider integrations | HTTP request construction, JSON mapping, invalid upstream responses |
| Dependency injection | Providers resolve through specific and shared interfaces |
| Aggregation | Parallel execution, merging, provider selection, partial failures, cancellation |
| Cache and fallback | Fresh cache, cache keys, stale-data fallback |
| Filtering | Categories, dates, sort directions, limits |
| Statistics | Averages, bucket boundaries, concurrent updates, retention |
| Monitoring | Anomaly detection, stable performance, minimum sample thresholds |
| Authentication | Token generation and invalid credentials |
| HTTP integration | Authentication, validation, response formats, statistics endpoints |

Concurrency tests include recording 40,000 values from eight threads and checking that no updates were lost. Time-dependent tests use .NET's `TimeProvider` and `FakeTimeProvider` so they can advance time without real delays.

Run the suite using:

```bash
dotnet test
```

## Project layout

```
src/ReadInEveryWeatherAggregator.Api/
  Controllers/           API endpoints
  Providers/             Weather, News, Books, and shared provider contract
  Services/              Aggregation, filtering, caching, statistics, auth, monitoring
  BackgroundServices/    Periodic performance monitoring
  Infrastructure/        Dependency injection and exception handling
  Models/                Request and response models
  Options/               Configuration models
  Program.cs             Application startup and registrations

tests/ReadInEveryWeatherAggregator.Tests/
  Unit/                  Component tests
  Integration/           HTTP pipeline tests
  Fakes/                 Fake providers and HTTP handlers

docs/
  ReadInEveryWeatherAggregator_Usage.pptx   A short presentation of what is done
```

## Final note

The purpose of the project is not to build a full reading or weather platform. It is to demonstrate how a .NET service can integrate multiple external APIs in a way that is extensible, concurrent, resilient, observable, and easy to test.
